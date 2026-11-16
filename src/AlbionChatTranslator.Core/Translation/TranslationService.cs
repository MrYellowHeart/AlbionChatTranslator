using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace AlbionChatTranslator.Core.Translation;

public enum TranslationOutcome
{
    /// <summary>Text was translated; <see cref="TranslationResult.Text"/> holds it.</summary>
    Translated,
    /// <summary>Already in the reader's language.</summary>
    SameLanguage,
    /// <summary>Too short or only links/numbers: show as-is.</summary>
    Skipped,
    /// <summary>The service asked us to slow down; shown untranslated.</summary>
    Busy,
    Failed,
}

public sealed record TranslationResult(TranslationOutcome Outcome, string? Text = null, string? SourceLanguage = null, string? Message = null)
{
    [JsonIgnore] public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;

    public static readonly TranslationResult Skip = new(TranslationOutcome.Skipped);
}

/// <summary>
/// Decides what is worth translating and sends it one line at a time. Most chat never
/// leaves the PC: short lines, item links and anything already in the reader's language
/// are filtered locally, and repeated messages are answered from a cache.
/// </summary>
public sealed class TranslationService : IAsyncDisposable
{
    private const int QueueCapacity = 64;
    private const int CacheCapacity = 2000;

    private readonly ChatLanguageDetector _detector;
    private readonly Channel<Job> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly LruCache<string, TranslationResult> _cache = new(CacheCapacity);
    private volatile ITranslationProvider _provider;

    public TranslationService(ChatLanguageDetector detector, ITranslationProvider provider)
    {
        _detector = detector;
        _provider = provider;
        _queue = Channel.CreateBounded<Job>(
            new BoundedChannelOptions(QueueCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true },
            dropped => dropped.Callback(new TranslationResult(TranslationOutcome.Skipped, Message: "Chat too busy, shown untranslated")));
        _worker = Task.Run(WorkAsync);
    }

    /// <summary>The reader's language (ISO 639-1).</summary>
    public string TargetLanguage { get; set; } = Languages.English;

    /// <summary>Lines with fewer letters than this are shown as-is ("gg", "ok", "ty").</summary>
    public int MinLetters { get; set; } = 4;

    /// <summary>Other languages the reader understands: never translated, never sent.</summary>
    public IReadOnlySet<string> ReadLanguages { get; set; } = new HashSet<string>();

    private bool Understands(string? lang, string target) =>
        lang is not null && (lang == Languages.Normalize(target) || ReadLanguages.Contains(lang));

    public ITranslationProvider Provider
    {
        get => _provider;
        set
        {
            _provider = value;
            _cache.Clear();
        }
    }

    /// <summary>Raised whenever text is about to leave the PC (for the on-screen counter).</summary>
    public event Action? Sent;

    /// <summary>Queues an incoming chat line. <paramref name="callback"/> runs on a worker thread.</summary>
    public void Submit(string text, Action<TranslationResult> callback)
    {
        var p = Glossary.Protect(text);
        if (!Glossary.HasTranslatableContent(p.Text) || p.Text.Count(char.IsLetter) < MinLetters)
        {
            callback(TranslationResult.Skip);
            return;
        }

        string target = TargetLanguage;
        string cacheKey = target + "\u0001" + text;
        if (_cache.TryGet(cacheKey, out var cached))
        {
            callback(cached);
            return;
        }

        if (!_queue.Writer.TryWrite(new Job(text, p, target, cacheKey, callback)))
            callback(TranslationResult.Skip);
    }

    /// <summary>Translates text the user typed into <paramref name="toLanguage"/>.</summary>
    public async Task<TranslationResult> TranslateReplyAsync(string text, string toLanguage, CancellationToken ct = default)
    {
        var p = Glossary.Protect(text);
        if (!Glossary.HasTranslatableContent(p.Text))
            return new TranslationResult(TranslationOutcome.SameLanguage, text);

        var mine = await Task.Run(() => _detector.Detect(p.Text, TargetLanguage), ct).ConfigureAwait(false);
        var result = await RunAsync(text, WithPlainEnglish(p, mine), toLanguage, ct).ConfigureAwait(false);
        return result.Outcome == TranslationOutcome.SameLanguage ? result with { Text = text } : result;
    }

    private async Task WorkAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                TranslationResult result;
                if (_cache.TryGet(job.CacheKey, out var cached))
                {
                    result = cached;
                }
                else
                {
                    result = await ProcessAsync(job, _cts.Token).ConfigureAwait(false);
                    if (result.Outcome is TranslationOutcome.Translated or TranslationOutcome.SameLanguage && job.Target == TargetLanguage)
                        _cache.Set(job.CacheKey, result);
                }

                try { job.Callback(result); }
                catch (Exception e) { System.Diagnostics.Debug.WriteLine($"[Translation] callback failed: {e}"); }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<TranslationResult> ProcessAsync(Job job, CancellationToken ct)
    {
        // Privacy filter: lines in a language the reader understands are never sent anywhere.
        var local = _detector.Detect(job.Protected.Text, job.Target);
        if (Understands(local, job.Target))
            return new TranslationResult(TranslationOutcome.SameLanguage, SourceLanguage: local);

        return await RunAsync(job.Text, WithPlainEnglish(job.Protected, local), job.Target, ct).ConfigureAwait(false);
    }

    private async Task<TranslationResult> RunAsync(string original, ProtectedText p, string target, CancellationToken ct)
    {
        try
        {
            var provider = _provider;
            Sent?.Invoke();
            // Let the service detect the source: it is far better at it than any local model.
            var r = await provider.TranslateAsync(p.Text, null, target, ct).ConfigureAwait(false);
            if (r.Outcome != ProviderOutcome.Translated || r.Text is null)
            {
                return new TranslationResult(r.Outcome == ProviderOutcome.Busy ? TranslationOutcome.Busy : TranslationOutcome.Failed,
                    Message: r.Message);
            }

            if (r.SourceLanguage is { } src && Understands(src, target))
                return new TranslationResult(TranslationOutcome.SameLanguage, SourceLanguage: src);

            var restored = Glossary.Restore(r.Text, p);
            if (restored is null)
            {
                // The service mangled a placeholder. Translate the plain text instead so the
                // reader still gets a translation, just without term protection.
                Sent?.Invoke();
                var retry = await provider.TranslateAsync(Glossary.CleanForDisplay(original), r.SourceLanguage, target, ct).ConfigureAwait(false);
                restored = retry.Outcome == ProviderOutcome.Translated ? retry.Text : null;
            }

            return restored is null
                ? new TranslationResult(TranslationOutcome.Failed, SourceLanguage: r.SourceLanguage, Message: "Translation failed")
                : new TranslationResult(TranslationOutcome.Translated, restored, r.SourceLanguage);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            return new TranslationResult(TranslationOutcome.Failed, Message: e.Message);
        }
    }

    /// <summary>English shorthand ("lf healer pls") becomes plain English before translation.</summary>
    private static ProtectedText WithPlainEnglish(ProtectedText p, string? detected) =>
        detected is null or Languages.English ? p with { Text = ChatSlang.Expand(p.Text) } : p;

    // ── Optional cache that survives restarts ────────────────────────────────

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// Writes recent translations to disk (opt-in: the file contains other players' messages).
    /// Entries older than 7 days are dropped.
    /// </summary>
    public void SaveCache(string path)
    {
        var now = DateTimeOffset.UtcNow;
        var entries = _cache.Snapshot()
            .Where(e => e.Value.Outcome == TranslationOutcome.Translated && now - e.Value.CachedAt < CacheLifetime)
            .Select(e => new CacheEntry(e.Key, e.Value.Text!, e.Value.SourceLanguage, e.Value.CachedAt))
            .ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(entries, CacheJsonContext.Default.ListCacheEntry));
    }

    public void LoadCache(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var entries = JsonSerializer.Deserialize(File.ReadAllText(path), CacheJsonContext.Default.ListCacheEntry) ?? [];
            var now = DateTimeOffset.UtcNow;
            foreach (var e in entries.Where(e => now - e.SavedAt < CacheLifetime).OrderBy(e => e.SavedAt))
                _cache.Set(e.Key, new TranslationResult(TranslationOutcome.Translated, e.Text, e.Source) { CachedAt = e.SavedAt });
        }
        catch (Exception e) when (e is JsonException or IOException) { }
    }

    public void ClearCache(string? path = null)
    {
        _cache.Clear();
        ClearCacheFile(path);
    }

    /// <summary>Deletes the on-disk cache (when the user turns the option off) but keeps memory.</summary>
    public void ClearCacheFile(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
    }

    internal sealed record CacheEntry(string Key, string Text, string? Source, DateTimeOffset SavedAt);

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _cts.CancelAsync();
        try { await _worker.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _cts.Dispose();
    }

    private sealed record Job(string Text, ProtectedText Protected, string Target, string CacheKey, Action<TranslationResult> Callback);
}

/// <summary>Small thread-safe least-recently-used cache.</summary>
internal sealed class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map = new();
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new();
    private readonly Lock _lock = new();

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
        }
        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing)) _order.Remove(existing);
            var node = _order.AddFirst((key, value));
            _map[key] = node;
            while (_map.Count > capacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
            }
        }
    }

    public List<KeyValuePair<TKey, TValue>> Snapshot()
    {
        lock (_lock) return _order.Select(n => new KeyValuePair<TKey, TValue>(n.Key, n.Value)).ToList();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _map.Clear();
            _order.Clear();
        }
    }
}

[JsonSerializable(typeof(List<TranslationService.CacheEntry>))]
internal sealed partial class CacheJsonContext : JsonSerializerContext;
