using System.Collections.Concurrent;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Last-resort translator: OPUS-MT models running on the user's own CPU. Used only when the
/// online services cannot answer, so translation never stops. Nothing leaves the PC.
///
/// Language packs (~110 MB each, pinned and SHA-256 checked) download in the background the
/// first time they are needed. Loaded models are freed after a few idle minutes so the app
/// goes back to its normal memory use.
/// </summary>
public sealed class OfflineProvider : ITranslationProvider
{
    private const int MaxLoadedModels = 2;
    public static readonly TimeSpan IdleUnload = TimeSpan.FromMinutes(3);

    private readonly ModelCatalog _catalog;
    private readonly ModelStore _store;
    private readonly Lock _modelsLock = new();
    private readonly LinkedList<(string Id, MarianModel Model)> _loaded = new();
    private readonly ConcurrentDictionary<string, Lazy<Task>> _downloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _idleTimer;
    private DateTimeOffset _lastUse = DateTimeOffset.MinValue;

    public OfflineProvider(ModelCatalog catalog, ModelStore store)
    {
        _catalog = catalog;
        _store = store;
        _idleTimer = new Timer(_ => UnloadIfIdle(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public string Name => "Offline";
    public bool IsOnline => false;

    /// <summary>Download missing language packs automatically when they are first needed.</summary>
    public bool AutoDownload { get; set; } = true;

    public event Action<string>? DownloadStarted;
    /// <summary>(pack id, null on success or an error message).</summary>
    public event Action<string, string?>? DownloadFinished;

    public ModelCatalog Catalog => _catalog;
    public ModelStore Store => _store;
    public int LoadedModelCount { get { lock (_modelsLock) return _loaded.Count; } }

    public async Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct)
    {
        var results = new List<ProviderResult>(items.Count);
        foreach (var item in items)
            results.Add(await TranslateOneAsync(item, target, waitForDownload: false, ct).ConfigureAwait(false));
        return results;
    }

    /// <summary>For replies: waits for a missing pack instead of answering "busy".</summary>
    public Task<ProviderResult> TranslateWaitingAsync(TranslationItem item, string target, CancellationToken ct) =>
        TranslateOneAsync(item, target, waitForDownload: true, ct);

    private async Task<ProviderResult> TranslateOneAsync(TranslationItem item, string target, bool waitForDownload, CancellationToken ct)
    {
        if (item.SourceHint is not { } source)
            return Fail("Language not recognised for offline translation");

        var route = _catalog.Route(source, target);
        if (route is null)
            return Fail($"No offline model for {Languages.NameOf(source)} → {Languages.NameOf(target)}");
        if (route.Count == 0)
            return new ProviderResult(ProviderOutcome.Translated, item.Text, source, By: Name);

        var missing = route.Where(s => !_store.IsInstalled(s.Pack)).Select(s => s.Pack).ToList();
        if (missing.Count > 0)
        {
            if (!AutoDownload && !waitForDownload)
                return Fail($"Offline {Languages.NameOf(source)} pack not downloaded");

            var downloads = missing.Select(StartDownload).ToList();
            if (!waitForDownload)
                return new ProviderResult(ProviderOutcome.Busy, Message: $"Downloading offline {Languages.NameOf(source)} pack…", By: Name);
            try { await Task.WhenAll(downloads).WaitAsync(ct).ConfigureAwait(false); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return Fail($"Offline pack download failed: {e.Message}");
            }
        }

        try
        {
            return await Task.Run(() =>
            {
                string current = item.Text;
                foreach (var step in route)
                {
                    ct.ThrowIfCancellationRequested();
                    current = GetModel(step.Pack).Translate(current, step.TargetPrefix);
                }
                _lastUse = DateTimeOffset.UtcNow;
                return new ProviderResult(ProviderOutcome.Translated, current, source, By: Name);
            }, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Fail($"Offline translation failed: {e.Message}");
        }
    }

    private ProviderResult Fail(string message) => new(ProviderOutcome.Failed, Message: message, By: Name);

    /// <summary>
    /// Downloads packs ahead of time (the languages the user said they see in chat), one after
    /// another. Failures are reported through <see cref="DownloadFinished"/> and do not stop the rest.
    /// Returns how many packs are installed afterwards.
    /// </summary>
    public async Task<int> PrefetchAsync(IReadOnlyList<ModelPack> packs, CancellationToken ct = default)
    {
        int ready = 0;
        foreach (var pack in packs)
        {
            ct.ThrowIfCancellationRequested();
            if (_store.IsInstalled(pack)) { ready++; continue; }
            try
            {
                await StartDownload(pack).WaitAsync(ct).ConfigureAwait(false);
                ready++;
            }
            catch (Exception e) when (e is not OperationCanceledException) { }
        }
        return ready;
    }

    private Task StartDownload(ModelPack pack) =>
        _downloads.GetOrAdd(pack.Id, _ => new Lazy<Task>(() => RunDownloadAsync(pack))).Value;

    private async Task RunDownloadAsync(ModelPack pack)
    {
        await Task.Yield(); // make sure the dictionary entry exists before it can be removed
        DownloadStarted?.Invoke(pack.Id);
        try
        {
            await _store.EnsureInstalledAsync(pack).ConfigureAwait(false);
            DownloadFinished?.Invoke(pack.Id, null);
        }
        catch (Exception e)
        {
            DownloadFinished?.Invoke(pack.Id, e.Message);
            throw;
        }
        finally
        {
            _downloads.TryRemove(pack.Id, out _);
        }
    }

    private MarianModel GetModel(ModelPack pack)
    {
        lock (_modelsLock)
        {
            for (var node = _loaded.First; node is not null; node = node.Next)
            {
                if (node.Value.Id != pack.Id) continue;
                _loaded.Remove(node);
                _loaded.AddFirst(node);
                return node.Value.Model;
            }

            var model = new MarianModel(_store.PackDirectory(pack));
            _loaded.AddFirst((pack.Id, model));
            while (_loaded.Count > MaxLoadedModels)
            {
                // Translate() holds the model's own lock, so disposal waits for in-flight work.
                var evicted = _loaded.Last!.Value.Model;
                _loaded.RemoveLast();
                evicted.Dispose();
            }
            return model;
        }
    }

    /// <summary>Frees every loaded model after a few minutes without use.</summary>
    internal void UnloadIfIdle(bool force = false)
    {
        if (!force && DateTimeOffset.UtcNow - _lastUse < IdleUnload) return;
        List<MarianModel> toFree;
        lock (_modelsLock)
        {
            if (_loaded.Count == 0) return;
            toFree = _loaded.Select(l => l.Model).ToList();
            _loaded.Clear();
        }
        foreach (var m in toFree) m.Dispose();
        GC.Collect();
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        UnloadIfIdle(force: true);
    }
}
