using System.Collections.Concurrent;

namespace AlbionChatTranslator.Core.Translation;

public enum ProviderOutcome { Translated, Unsupported, Downloading, Failed }

public sealed record ProviderResult(ProviderOutcome Outcome, string? Text = null, string? Message = null);

/// <summary>Something that turns text in one language into another.</summary>
public interface ITranslationProvider : IDisposable
{
    string Name { get; }
    bool CanTranslate(string source, string target);

    /// <summary>True if it can translate right now without downloading anything.</summary>
    bool IsReady(string source, string target);
    Task<ProviderResult> TranslateAsync(string text, string source, string target, bool waitForDownloads, CancellationToken ct);
}

/// <summary>
/// The default: OPUS-MT models on the user's own CPU. No account, no key, and chat text
/// never leaves the PC. Missing language packs download once, in the background.
/// </summary>
public sealed class OfflineProvider : ITranslationProvider
{
    private const int MaxLoadedModels = 2; // ~150 MB each; enough for one pivot (X->en->Y)

    private readonly ModelCatalog _catalog;
    private readonly ModelStore _store;
    private readonly Lock _modelsLock = new();
    private readonly LinkedList<(string Id, MarianModel Model)> _loaded = new();
    private readonly ConcurrentDictionary<string, Lazy<Task>> _downloads = new(StringComparer.OrdinalIgnoreCase);

    public OfflineProvider(ModelCatalog catalog, ModelStore store)
    {
        _catalog = catalog;
        _store = store;
    }

    public string Name => "Offline";
    public bool AutoDownload { get; set; } = true;

    /// <summary>(pack id, null on success or an error message).</summary>
    public event Action<string, string?>? DownloadFinished;
    public event Action<string>? DownloadStarted;

    public bool CanTranslate(string source, string target) => _catalog.Route(source, target) is not null;

    public bool IsReady(string source, string target) =>
        _catalog.Route(source, target) is { } route && route.All(s => _store.IsInstalled(s.Pack));

    public async Task<ProviderResult> TranslateAsync(string text, string source, string target, bool waitForDownloads, CancellationToken ct)
    {
        var route = _catalog.Route(source, target);
        if (route is null)
            return new ProviderResult(ProviderOutcome.Unsupported, Message: $"No offline model for {Languages.NameOf(source)} → {Languages.NameOf(target)}");

        var missing = route.Where(s => !_store.IsInstalled(s.Pack)).Select(s => s.Pack).ToList();
        if (missing.Count > 0)
        {
            // Incoming chat downloads only when allowed; an explicit reply always may.
            if (!AutoDownload && !waitForDownloads)
                return new ProviderResult(ProviderOutcome.Unsupported, Message: $"{Languages.NameOf(source)} language pack not installed");

            var downloads = missing.Select(StartDownload).ToList();
            if (!waitForDownloads)
                return new ProviderResult(ProviderOutcome.Downloading, Message: "Downloading language pack…");

            try { await Task.WhenAll(downloads).WaitAsync(ct).ConfigureAwait(false); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                return new ProviderResult(ProviderOutcome.Failed, Message: $"Language pack download failed: {e.Message}");
            }
        }

        return await Task.Run(() =>
        {
            string current = text;
            foreach (var step in route)
            {
                ct.ThrowIfCancellationRequested();
                current = GetModel(step.Pack).Translate(current, step.TargetPrefix);
            }
            return new ProviderResult(ProviderOutcome.Translated, current);
        }, ct).ConfigureAwait(false);
    }

    private Task StartDownload(ModelPack pack) =>
        _downloads.GetOrAdd(pack.Id, _ => new Lazy<Task>(() => RunDownloadAsync(pack))).Value;

    private async Task RunDownloadAsync(ModelPack pack)
    {
        await Task.Yield(); // make sure the dictionary entry exists before we can remove it
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

    public void Dispose()
    {
        lock (_modelsLock)
        {
            foreach (var (_, model) in _loaded) model.Dispose();
            _loaded.Clear();
        }
    }
}

/// <summary>Optional: DeepL with the user's own API key. Sends chat text to DeepL.</summary>
public sealed class DeepLProvider : ITranslationProvider
{
    private readonly DeepL.Translator _client;

    public DeepLProvider(string apiKey) => _client = new DeepL.Translator(apiKey);

    public string Name => "DeepL";

    public bool CanTranslate(string source, string target) => true;

    public bool IsReady(string source, string target) => true;

    public async Task<ProviderResult> TranslateAsync(string text, string source, string target, bool waitForDownloads, CancellationToken ct)
    {
        try
        {
            var result = await _client.TranslateTextAsync(text, Languages.Normalize(source).ToUpperInvariant(), DeepLTarget(target),
                new DeepL.TextTranslateOptions { PreserveFormatting = true }, ct).ConfigureAwait(false);
            return new ProviderResult(ProviderOutcome.Translated, result.Text);
        }
        catch (DeepL.DeepLException e)
        {
            return new ProviderResult(ProviderOutcome.Failed, Message: $"DeepL: {e.Message}");
        }
    }

    // DeepL wants regional variants for a few target languages.
    internal static string DeepLTarget(string code) => Languages.Normalize(code) switch
    {
        "en" => "EN-US",
        "pt" => "PT-BR",
        "zh" => "ZH-HANS",
        var c => c.ToUpperInvariant(),
    };

    public void Dispose() => _client.Dispose();
}
