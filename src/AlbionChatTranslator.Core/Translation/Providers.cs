using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlbionChatTranslator.Core.Translation;

public enum ProviderOutcome { Translated, Busy, Failed }

/// <summary>Result for one line.</summary>
/// <param name="SourceLanguage">Language the provider detected (ISO 639-1), when it reports one.</param>
/// <param name="By">Which provider produced it ("Google", "Offline"...).</param>
/// <param name="LeftDevice">True if the text was transmitted to an online service.</param>
public sealed record ProviderResult(
    ProviderOutcome Outcome,
    string? Text = null,
    string? SourceLanguage = null,
    string? Message = null,
    string? By = null,
    bool LeftDevice = false);

/// <summary>One line to translate. <see cref="SourceHint"/> is the local guess of its language, if any.</summary>
public sealed record TranslationItem(string Text, string? SourceHint);

/// <summary>Something that turns short texts into another language.</summary>
public interface ITranslationProvider : IDisposable
{
    string Name { get; }

    /// <summary>True for providers that send text off the PC.</summary>
    bool IsOnline { get; }

    /// <summary>Translates several lines; returns exactly one result per item, in order.</summary>
    Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct);
}

internal static class ProviderResults
{
    public static IReadOnlyList<ProviderResult> All(int count, ProviderResult r) => Enumerable.Repeat(r, count).ToList();
}

/// <summary>
/// Tries each provider in order for whatever is still untranslated: your own key first,
/// then Google, then the offline model. Lines another provider already translated are never
/// re-sent. Neither provider is owned; the caller disposes them.
/// </summary>
public sealed class ChainProvider(IReadOnlyList<ITranslationProvider> providers) : ITranslationProvider
{
    public IReadOnlyList<ITranslationProvider> Providers { get; } = providers;
    public string Name => string.Join(" → ", Providers.Select(p => p.Name));
    public bool IsOnline => Providers.Any(p => p.IsOnline);

    public async Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct)
    {
        var results = new ProviderResult?[items.Count];
        var firstFailure = new ProviderResult?[items.Count];
        var leftDevice = new bool[items.Count];

        foreach (var provider in Providers)
        {
            var pending = Enumerable.Range(0, items.Count).Where(i => results[i] is null).ToList();
            if (pending.Count == 0) break;

            var answers = await provider.TranslateAsync(pending.Select(i => items[i]).ToList(), target, ct).ConfigureAwait(false);
            for (int k = 0; k < pending.Count; k++)
            {
                int i = pending[k];
                var a = answers[k];
                leftDevice[i] |= a.LeftDevice;
                if (a.Outcome == ProviderOutcome.Translated) results[i] = a;
                else firstFailure[i] ??= a;
            }
        }

        return Enumerable.Range(0, items.Count)
            .Select(i => (results[i] ?? firstFailure[i] ?? new ProviderResult(ProviderOutcome.Failed, Message: "No translator available"))
                with { LeftDevice = leftDevice[i] })
            .ToList();
    }

    public void Dispose() { }
}

/// <summary>
/// Google Translate through the keyless endpoint Google's own web widgets use. Best quality
/// and every language, but unofficial and rate-limited per IP address, so this provider:
/// sends several lines in one request, keeps a minimum gap between requests, widens that
/// gap after Google pushes back, and answers "busy" immediately (so the next provider takes
/// over) instead of making chat wait.
///
/// Privacy: only the message text is sent, in an HTTPS POST body (never in the URL), with no
/// cookies, no account, and no sender name, channel or game data.
/// </summary>
public sealed class GoogleProvider : ITranslationProvider
{
    private static readonly Uri Endpoint = new("https://translate.googleapis.com/translate_a/single");

    /// <summary>Normal gap between requests. Several lines share one request.</summary>
    public static readonly TimeSpan BaseInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);
    /// <summary>Longest a line may wait for its turn before the next provider is used instead.</summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(7);
    private const int MaxBatchChars = 1800;

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _lock = new();
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;
    private TimeSpan _interval = BaseInterval;
    private TimeSpan _backoff = MinBackoff;

    public GoogleProvider(HttpClient? http = null, TimeProvider? clock = null)
    {
        _http = http ?? CreateClient();
        _clock = clock ?? TimeProvider.System;
    }

    public string Name => "Google";
    public bool IsOnline => true;

    /// <summary>When Google will be tried again after pushing back, or null if it is available.</summary>
    public DateTimeOffset? PausedUntil
    {
        get { lock (_lock) return _pausedUntil > _clock.GetUtcNow() ? _pausedUntil : null; }
    }

    public async Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct)
    {
        if (items.Count == 0) return [];

        var results = new List<ProviderResult>(items.Count);
        foreach (var chunk in Chunk(items))
            results.AddRange(await TranslateChunkAsync(chunk, target, ct).ConfigureAwait(false));
        return results;
    }

    private static IEnumerable<List<TranslationItem>> Chunk(IReadOnlyList<TranslationItem> items)
    {
        var chunk = new List<TranslationItem>();
        int chars = 0;
        foreach (var item in items)
        {
            if (chunk.Count > 0 && chars + item.Text.Length > MaxBatchChars)
            {
                yield return chunk;
                chunk = [];
                chars = 0;
            }
            chunk.Add(item);
            chars += item.Text.Length + 1;
        }
        if (chunk.Count > 0) yield return chunk;
    }

    private async Task<IReadOnlyList<ProviderResult>> TranslateChunkAsync(List<TranslationItem> items, string target, CancellationToken ct)
    {
        // One request at a time, spaced out. If our turn is too far away, step aside.
        if (!await _gate.WaitAsync(MaxWait, ct).ConfigureAwait(false))
            return ProviderResults.All(items.Count, Busy("Google is busy"));
        try
        {
            TimeSpan wait;
            lock (_lock)
            {
                var now = _clock.GetUtcNow();
                if (_pausedUntil > now)
                    return ProviderResults.All(items.Count, Busy($"Google asked us to slow down; trying again in {Math.Ceiling((_pausedUntil - now).TotalSeconds)} s"));
                wait = _nextAllowed - now;
            }
            if (wait > MaxWait) return ProviderResults.All(items.Count, Busy("Google is busy"));
            if (wait > TimeSpan.Zero) await Task.Delay(wait, _clock, ct).ConfigureAwait(false);

            var outcome = await SendAsync(string.Join('\n', items.Select(i => i.Text)), target, ct).ConfigureAwait(false);
            lock (_lock) _nextAllowed = _clock.GetUtcNow() + _interval;

            if (outcome.Outcome != ProviderOutcome.Translated)
                return ProviderResults.All(items.Count, outcome);

            var lines = outcome.Text!.Split('\n');
            if (lines.Length == items.Count)
                return lines.Select(l => outcome with { Text = l.Trim() }).ToList();

            // Google merged or split lines; fall back to one request per line for this group.
            var single = new List<ProviderResult>(items.Count);
            foreach (var item in items)
            {
                var r = await SendAsync(item.Text, target, ct).ConfigureAwait(false);
                single.Add(r);
                if (r.Outcome == ProviderOutcome.Busy) break;
            }
            while (single.Count < items.Count) single.Add(Busy("Google is busy"));
            return single;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProviderResult Busy(string message) => new(ProviderOutcome.Busy, Message: message, By: "Google");

    private async Task<ProviderResult> SendAsync(string text, string target, CancellationToken ct)
    {
        var query = $"client=gtx&dt=t&dj=1&sl=auto&tl={GoogleCode(target)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, new UriBuilder(Endpoint) { Query = query }.Uri)
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("q", text)]),
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            return new ProviderResult(ProviderOutcome.Failed, Message: $"No connection to Google Translate ({e.Message})", By: Name);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ProviderResult(ProviderOutcome.Failed, Message: "Google Translate timed out", By: Name, LeftDevice: true);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.Forbidden)
            {
                var pause = PushedBack(response.Headers.RetryAfter?.Delta);
                return Busy($"Google asked us to slow down; trying again in {Math.Ceiling(pause.TotalSeconds)} s") with { LeftDevice = true };
            }
            if (!response.IsSuccessStatusCode)
                return new ProviderResult(ProviderOutcome.Failed, Message: $"Google Translate error {(int)response.StatusCode}", By: Name, LeftDevice: true);

            await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var parsed = Parse(await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false));
            lock (_lock)
            {
                // Things are fine: drift back towards the normal pace.
                _backoff = MinBackoff;
                _interval = TimeSpan.FromTicks(Math.Max(BaseInterval.Ticks, (long)(_interval.Ticks * 0.9)));
            }
            return parsed with { LeftDevice = true };
        }
    }

    internal static ProviderResult Parse(JsonDocument doc)
    {
        using (doc)
        {
            var root = doc.RootElement;
            var text = string.Concat(root.TryGetProperty("sentences", out var sentences) && sentences.ValueKind == JsonValueKind.Array
                ? sentences.EnumerateArray()
                    .Where(s => s.TryGetProperty("trans", out _))
                    .Select(s => s.GetProperty("trans").GetString())
                : []);
            string? src = root.TryGetProperty("src", out var s) ? Languages.Normalize(s.GetString() ?? "") : null;
            return text.Length == 0
                ? new ProviderResult(ProviderOutcome.Failed, Message: "Google returned no translation", By: "Google")
                : new ProviderResult(ProviderOutcome.Translated, text, src, By: "Google");
        }
    }

    /// <summary>Google pushed back: pause, and keep a wider gap between requests afterwards.</summary>
    private TimeSpan PushedBack(TimeSpan? retryAfter)
    {
        lock (_lock)
        {
            var pause = retryAfter is { } r && r > _backoff ? r : _backoff;
            if (pause > MaxBackoff) pause = MaxBackoff;
            _pausedUntil = _clock.GetUtcNow() + pause;
            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
            _interval = TimeSpan.FromTicks(Math.Min(_interval.Ticks * 2, MaxInterval.Ticks));
            return pause;
        }
    }

    /// <summary>Our ISO 639-1 codes to Google's.</summary>
    internal static string GoogleCode(string? code) => code is null ? "auto" : Languages.Normalize(code) switch
    {
        "zh" => "zh-CN",
        "he" => "iw",
        var c => c,
    };

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,          // nothing that could link requests to a person
            AllowAutoRedirect = false,   // only ever talk to translate.googleapis.com
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        // A generic browser string: no app name, version or anything else that identifies the user.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        return client;
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}

/// <summary>Microsoft Translator (Azure AI Translator) with the user's own key. Official API, generous free tier.</summary>
public sealed class MicrosoftProvider : ITranslationProvider
{
    private static readonly Uri Endpoint = new("https://api.cognitive.microsofttranslator.com/translate?api-version=3.0");
    private readonly HttpClient _http;
    private readonly string _key;
    private readonly string? _region;

    public MicrosoftProvider(string key, string? region, HttpClient? http = null)
    {
        _key = key;
        _region = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public string Name => "Microsoft";
    public bool IsOnline => true;

    public async Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}&to={MicrosoftCode(target)}")
        {
            Content = JsonContent.Create(items.Select(i => new { Text = i.Text }).ToArray()),
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
        if (_region is not null) request.Headers.Add("Ocp-Apim-Subscription-Region", _region);

        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Busy, Message: "Microsoft Translator limit reached", By: Name, LeftDevice: true));
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Failed, Message: "Microsoft Translator rejected the key (check key and region in Settings)", By: Name, LeftDevice: true));
            if (!response.IsSuccessStatusCode)
                return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Failed, Message: $"Microsoft Translator error {(int)response.StatusCode}", By: Name, LeftDevice: true));

            var json = await response.Content.ReadFromJsonAsync<JsonArray>(ct).ConfigureAwait(false);
            return ParseResponse(json, items.Count);
        }
        catch (HttpRequestException e)
        {
            return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Failed, Message: $"No connection to Microsoft Translator ({e.Message})", By: Name));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Failed, Message: "Microsoft Translator timed out", By: Name, LeftDevice: true));
        }
    }

    internal static IReadOnlyList<ProviderResult> ParseResponse(JsonArray? json, int count)
    {
        var results = new List<ProviderResult>(count);
        for (int i = 0; i < count; i++)
        {
            var entry = json is not null && i < json.Count ? json[i] : null;
            var text = entry?["translations"]?[0]?["text"]?.GetValue<string>();
            var src = entry?["detectedLanguage"]?["language"]?.GetValue<string>();
            results.Add(text is null
                ? new ProviderResult(ProviderOutcome.Failed, Message: "Microsoft Translator returned no translation", By: "Microsoft", LeftDevice: true)
                : new ProviderResult(ProviderOutcome.Translated, text, src is null ? null : Languages.Normalize(src), By: "Microsoft", LeftDevice: true));
        }
        return results;
    }

    internal static string MicrosoftCode(string code) => Languages.Normalize(code) switch
    {
        "zh" => "zh-Hans",
        "pt" => "pt",
        var c => c,
    };

    public void Dispose() => _http.Dispose();
}

/// <summary>DeepL with the user's own API key.</summary>
public sealed class DeepLProvider : ITranslationProvider
{
    private readonly DeepL.Translator _client;

    public DeepLProvider(string apiKey) => _client = new DeepL.Translator(apiKey);

    public string Name => "DeepL";
    public bool IsOnline => true;

    public async Task<IReadOnlyList<ProviderResult>> TranslateAsync(IReadOnlyList<TranslationItem> items, string target, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        try
        {
            var results = await _client.TranslateTextAsync(items.Select(i => i.Text), null, DeepLTarget(target),
                new DeepL.TextTranslateOptions { PreserveFormatting = true }, ct).ConfigureAwait(false);
            return results.Select(r => new ProviderResult(ProviderOutcome.Translated, r.Text, Languages.Normalize(r.DetectedSourceLanguageCode), By: Name, LeftDevice: true)).ToList();
        }
        catch (DeepL.QuotaExceededException)
        {
            return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Busy, Message: "DeepL monthly quota used up", By: Name, LeftDevice: true));
        }
        catch (DeepL.TooManyRequestsException)
        {
            return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Busy, Message: "DeepL is busy", By: Name, LeftDevice: true));
        }
        catch (DeepL.DeepLException e)
        {
            return ProviderResults.All(items.Count, new ProviderResult(ProviderOutcome.Failed, Message: $"DeepL: {e.Message}", By: Name, LeftDevice: true));
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
