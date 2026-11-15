using System.Threading.Channels;

namespace AlbionChatTranslator.Core.Translation;

public enum TranslationOutcome
{
    /// <summary>Text was translated; <see cref="TranslationResult.Text"/> holds it.</summary>
    Translated,
    /// <summary>Already in the reader's language.</summary>
    SameLanguage,
    /// <summary>Too short, only links/numbers, or language unclear: show as-is.</summary>
    Skipped,
    /// <summary>The needed language pack is downloading; later lines will translate.</summary>
    Downloading,
    Unsupported,
    Failed,
}

public sealed record TranslationResult(TranslationOutcome Outcome, string? Text = null, string? SourceLanguage = null, string? Message = null)
{
    public static readonly TranslationResult Skip = new(TranslationOutcome.Skipped);
}

/// <summary>
/// Decides what is worth translating and does it on one background worker, so a busy
/// chat can never pile up CPU work. Most lines never reach a model: they are filtered,
/// already in the reader's language, or answered from the cache.
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
            dropped => dropped.Callback(new TranslationResult(TranslationOutcome.Skipped, Message: "Chat too busy — shown untranslated")));
        _worker = Task.Run(WorkAsync);
    }

    /// <summary>The reader's language (ISO 639-1).</summary>
    public string TargetLanguage { get; set; } = Languages.English;

    /// <summary>Lines with fewer letters than this are shown as-is ("gg", "ok", "ty").</summary>
    public int MinLetters { get; set; } = 4;

    public ITranslationProvider Provider
    {
        get => _provider;
        set
        {
            _provider = value;
            _cache.Clear();
        }
    }

    /// <summary>Queues an incoming chat line. <paramref name="callback"/> runs on a worker thread.</summary>
    public void Submit(string text, Action<TranslationResult> callback)
    {
        var quick = Precheck(text, out var protectedText, out var cacheKey);
        if (quick is not null)
        {
            callback(quick);
            return;
        }
        if (!_queue.Writer.TryWrite(new Job(text, protectedText!, TargetLanguage, cacheKey!, callback)))
            callback(TranslationResult.Skip);
    }

    /// <summary>Translates text the user typed into <paramref name="toLanguage"/>, downloading packs if needed.</summary>
    public async Task<TranslationResult> TranslateReplyAsync(string text, string toLanguage, CancellationToken ct = default)
    {
        var p = Glossary.Protect(text);
        if (!Glossary.HasTranslatableContent(p.Text))
            return new TranslationResult(TranslationOutcome.SameLanguage, text);

        var source = await Task.Run(() => _detector.Detect(p.Text, TargetLanguage), ct).ConfigureAwait(false) ?? TargetLanguage;
        if (Languages.Normalize(source) == Languages.Normalize(toLanguage))
            return new TranslationResult(TranslationOutcome.SameLanguage, text, source);

        return await RunAsync(text, p, source, toLanguage, waitForDownloads: true, ct).ConfigureAwait(false);
    }

    /// <summary>Cheap checks done on the caller's thread. Returns a result if no work is needed.</summary>
    private TranslationResult? Precheck(string text, out ProtectedText? protectedText, out string? cacheKey)
    {
        protectedText = null;
        cacheKey = null;

        var p = Glossary.Protect(text);
        if (!Glossary.HasTranslatableContent(p.Text)) return TranslationResult.Skip;
        if (p.Text.Count(char.IsLetter) < MinLetters) return TranslationResult.Skip;

        string target = TargetLanguage;
        cacheKey = target + "\u0001" + text;
        if (_cache.TryGet(cacheKey, out var cached)) return cached;

        protectedText = p;
        return null;
    }

    /// <summary>Language detection plus the model call; runs on the worker.</summary>
    private async Task<TranslationResult> ProcessAsync(Job job, CancellationToken ct)
    {
        var candidates = _detector.DetectCandidates(job.Protected.Text, job.Target);
        if (candidates.Count == 0) return TranslationResult.Skip;

        // On a close call (e.g. Spanish vs Portuguese) use a language whose pack is already
        // installed rather than downloading another one for a near-identical guess.
        var provider = _provider;
        var detected = candidates.FirstOrDefault(c => c == Languages.Normalize(job.Target))
                       ?? candidates.FirstOrDefault(c => provider.IsReady(c, job.Target))
                       ?? candidates[0];
        if (detected == Languages.Normalize(job.Target))
        {
            var same = new TranslationResult(TranslationOutcome.SameLanguage, SourceLanguage: detected);
            _cache.Set(job.CacheKey, same);
            return same;
        }
        return await RunAsync(job.Text, job.Protected, detected, job.Target, waitForDownloads: false, ct).ConfigureAwait(false);
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
                    if (result.Outcome == TranslationOutcome.Translated && job.Target == TargetLanguage)
                        _cache.Set(job.CacheKey, result);
                }

                try { job.Callback(result); }
                catch (Exception e) { System.Diagnostics.Debug.WriteLine($"[Translation] callback failed: {e}"); }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<TranslationResult> RunAsync(string original, ProtectedText p, string source, string target, bool waitForDownloads, CancellationToken ct)
    {
        try
        {
            var provider = _provider;
            var r = await provider.TranslateAsync(p.Text, source, target, waitForDownloads, ct).ConfigureAwait(false);
            if (r.Outcome != ProviderOutcome.Translated || r.Text is null)
            {
                return new TranslationResult(r.Outcome switch
                {
                    ProviderOutcome.Downloading => TranslationOutcome.Downloading,
                    ProviderOutcome.Unsupported => TranslationOutcome.Unsupported,
                    _ => TranslationOutcome.Failed,
                }, SourceLanguage: source, Message: r.Message);
            }

            var restored = Glossary.Restore(r.Text, p);
            if (restored is null)
            {
                // The model mangled a placeholder. Translate the plain text instead so the
                // reader still gets a translation, just without term protection.
                var plain = Glossary.CleanForDisplay(original);
                var retry = await provider.TranslateAsync(plain, source, target, waitForDownloads, ct).ConfigureAwait(false);
                restored = retry.Outcome == ProviderOutcome.Translated ? retry.Text : null;
            }

            return restored is null
                ? new TranslationResult(TranslationOutcome.Failed, SourceLanguage: source, Message: "Translation failed")
                : new TranslationResult(TranslationOutcome.Translated, restored, source);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            return new TranslationResult(TranslationOutcome.Failed, SourceLanguage: source, Message: e.Message);
        }
    }

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

    public void Clear()
    {
        lock (_lock)
        {
            _map.Clear();
            _order.Clear();
        }
    }
}
