using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace AlbionChatTranslator.Core.Translation;

public enum TranslationOutcome
{
    /// <summary>Text was translated; <see cref="TranslationResult.Text"/> holds it.</summary>
    Translated,
    /// <summary>Already in a language the reader understands.</summary>
    SameLanguage,
    /// <summary>Too short or only links/numbers: show as-is.</summary>
    Skipped,
    /// <summary>Every translator was busy for now; shown untranslated.</summary>
    Busy,
    Failed,
}

/// <param name="By">Which translator produced it ("Google", "Microsoft", "DeepL", "Offline").</param>
public sealed record TranslationResult(
    TranslationOutcome Outcome,
    string? Text = null,
    string? SourceLanguage = null,
    string? Message = null,
    string? By = null)
{
    public static readonly TranslationResult Skip = new(TranslationOutcome.Skipped);

    [JsonIgnore] public DateTimeOffset CachedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Decides what is worth translating and sends it in small groups. Most chat never leaves the
/// PC: short lines, item links and anything already in the reader's languages are filtered
/// locally, and repeated messages are answered from a cache. Lines that arrive close together
/// share one request, which keeps well under online services' rate limits.
/// </summary>
public sealed class TranslationService : IAsyncDisposable
{
    private const int QueueCapacity = 64;
    private const int CacheCapacity = 2000;
    private const int MaxBatchLines = 12;
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(400);

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

    /// <summary>If set, replies wait for a missing offline pack instead of giving up.</summary>
    public OfflineProvider? Offline { get; set; }

    public ITranslationProvider Provider
    {
        get => _provider;
        set
        {
            _provider = value;
            _cache.Clear();
        }
    }

    /// <summary>Raised once per line that actually left the PC (for the on-screen counter).</summary>
    public event Action? Sent;

    private bool Understands(string? lang, string target) =>
        lang is not null && (lang == Languages.Normalize(target) || ReadLanguages.Contains(lang));

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

        var mine = await Task.Run(() => _detector.Detect(p.Text, TargetLanguage), ct).ConfigureAwait(false) ?? TargetLanguage;
        var masked = WithPlainEnglish(p, mine);
        var item = new TranslationItem(masked.Text, mine);

        var r = (await _provider.TranslateAsync([item], toLanguage, ct).ConfigureAwait(false))[0];
        Count(r);
        if (r.Outcome != ProviderOutcome.Translated && Offline is { } offline)
            r = await offline.TranslateWaitingAsync(item, toLanguage, ct).ConfigureAwait(false);

        var result = await FinishAsync(text, masked, r, toLanguage, mine, ct).ConfigureAwait(false);
        return result.Outcome == TranslationOutcome.SameLanguage ? result with { Text = text } : result;
    }

    private async Task WorkAsync()
    {
        var reader = _queue.Reader;
        var ct = _cts.Token;
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                var batch = await CollectBatchAsync(reader, ct).ConfigureAwait(false);
                await ProcessBatchAsync(batch, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Takes what is queued, then waits briefly for lines arriving right behind it.</summary>
    private static async Task<List<Job>> CollectBatchAsync(ChannelReader<Job> reader, CancellationToken ct)
    {
        var batch = new List<Job>();
        var deadline = DateTime.UtcNow + BatchWindow;
        while (batch.Count < MaxBatchLines)
        {
            if (reader.TryRead(out var job))
            {
                batch.Add(job);
                continue;
            }
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) break;
            var more = reader.WaitToReadAsync(ct).AsTask();
            if (await Task.WhenAny(more, Task.Delay(left, ct)).ConfigureAwait(false) != more || !await more.ConfigureAwait(false)) break;
        }
        return batch;
    }

    private async Task ProcessBatchAsync(List<Job> batch, CancellationToken ct)
    {
        var pending = new List<(Job Job, ProtectedText Masked, string? Hint)>();
        foreach (var job in batch)
        {
            if (_cache.TryGet(job.CacheKey, out var cached))
            {
                Deliver(job, cached);
                continue;
            }
            // Privacy filter: lines in a language the reader understands are never sent anywhere.
            var local = _detector.Detect(job.Protected.Text, job.Target);
            if (Understands(local, job.Target))
            {
                var same = new TranslationResult(TranslationOutcome.SameLanguage, SourceLanguage: local);
                _cache.Set(job.CacheKey, same);
                Deliver(job, same);
                continue;
            }
            pending.Add((job, WithPlainEnglish(job.Protected, local), local));
        }

        // One request per (target, likely language): the translator detects one source language per request.
        foreach (var group in pending.GroupBy(x => (x.Job.Target, x.Hint)))
        {
            var lines = group.ToList();
            IReadOnlyList<ProviderResult> answers;
            try
            {
                answers = await _provider.TranslateAsync(lines.Select(l => new TranslationItem(l.Masked.Text, l.Hint)).ToList(), group.Key.Target, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                answers = ProviderResults.All(lines.Count, new ProviderResult(ProviderOutcome.Failed, Message: e.Message));
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var (job, masked, hint) = lines[i];
                Count(answers[i]);
                var result = await FinishAsync(job.Text, masked, answers[i], job.Target, hint, ct).ConfigureAwait(false);
                if (result.Outcome is TranslationOutcome.Translated or TranslationOutcome.SameLanguage && job.Target == TargetLanguage)
                    _cache.Set(job.CacheKey, result);
                Deliver(job, result);
            }
        }
    }

    /// <summary>Turns a provider answer into what the reader sees, putting Albion terms back.</summary>
    private async Task<TranslationResult> FinishAsync(string original, ProtectedText masked, ProviderResult r, string target, string? hint, CancellationToken ct)
    {
        if (r.Outcome != ProviderOutcome.Translated || r.Text is null)
        {
            return new TranslationResult(r.Outcome == ProviderOutcome.Busy ? TranslationOutcome.Busy : TranslationOutcome.Failed,
                SourceLanguage: hint, Message: r.Message, By: r.By);
        }

        var src = r.SourceLanguage ?? hint;
        if (Understands(src, target))
            return new TranslationResult(TranslationOutcome.SameLanguage, SourceLanguage: src, By: r.By);

        var restored = Glossary.Restore(r.Text, masked);
        var by = r.By;
        if (restored is null)
        {
            // The translator mangled a placeholder: translate the plain text instead, so the
            // reader still gets a translation, just without term protection.
            var plain = new TranslationItem(Glossary.CleanForDisplay(original), src);
            var retry = (await _provider.TranslateAsync([plain], target, ct).ConfigureAwait(false))[0];
            Count(retry);
            restored = retry.Outcome == ProviderOutcome.Translated ? retry.Text : null;
            by = retry.By ?? by;
        }

        return restored is null
            ? new TranslationResult(TranslationOutcome.Failed, SourceLanguage: src, Message: "Translation failed", By: by)
            : new TranslationResult(TranslationOutcome.Translated, restored, src, By: by);
    }

    private void Count(ProviderResult r)
    {
        if (r.LeftDevice) Sent?.Invoke();
    }

    private static void Deliver(Job job, TranslationResult result)
    {
        try { job.Callback(result); }
        catch (Exception e) { System.Diagnostics.Debug.WriteLine($"[Translation] callback failed: {e}"); }
    }

    /// <summary>
    /// Chat shorthand becomes plain words before translation ("lf healer pls", "vc vem?", "ntp").
    /// Lines whose language is unknown are treated as English shorthand, the most common case.
    /// </summary>
    private static ProtectedText WithPlainEnglish(ProtectedText p, string? detected) =>
        p with { Text = ChatSlang.Expand(p.Text, detected ?? Languages.English) };

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
            .Select(e => new CacheEntry(e.Key, e.Value.Text!, e.Value.SourceLanguage, e.Value.CachedAt, e.Value.By))
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
                _cache.Set(e.Key, new TranslationResult(TranslationOutcome.Translated, e.Text, e.Source, By: e.By) { CachedAt = e.SavedAt });
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

    internal sealed record CacheEntry(string Key, string Text, string? Source, DateTimeOffset SavedAt, string? By = null);

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
