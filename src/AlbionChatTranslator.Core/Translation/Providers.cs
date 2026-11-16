using System.Net;
using System.Text.Json;

namespace AlbionChatTranslator.Core.Translation;

public enum ProviderOutcome { Translated, Busy, Failed }

/// <param name="SourceLanguage">Language the provider detected (ISO 639-1), when it reports one.</param>
public sealed record ProviderResult(ProviderOutcome Outcome, string? Text = null, string? SourceLanguage = null, string? Message = null);

/// <summary>Something that turns text into another language.</summary>
public interface ITranslationProvider : IDisposable
{
    string Name { get; }

    /// <param name="source">ISO 639-1 code, or null to let the provider detect it.</param>
    Task<ProviderResult> TranslateAsync(string text, string? source, string target, CancellationToken ct);
}

/// <summary>
/// Google Translate through the same keyless endpoint Google's own web widgets use.
/// Best quality and every language, with nothing to install or sign up for.
///
/// Privacy: only the message text is sent, in an HTTPS POST body (never in the URL), with
/// no cookies, no account, no sender name, channel or game data. Item links and Albion
/// terms are already replaced by placeholders before they get here.
/// </summary>
public sealed class GoogleProvider : ITranslationProvider
{
    private static readonly Uri Endpoint = new("https://translate.googleapis.com/translate_a/single");
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly Lock _lock = new();
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;
    private TimeSpan _backoff = MinBackoff;

    public GoogleProvider(HttpClient? http = null, TimeProvider? clock = null)
    {
        _http = http ?? CreateClient();
        _clock = clock ?? TimeProvider.System;
    }

    public string Name => "Google";

    public async Task<ProviderResult> TranslateAsync(string text, string? source, string target, CancellationToken ct)
    {
        lock (_lock)
        {
            var wait = _pausedUntil - _clock.GetUtcNow();
            if (wait > TimeSpan.Zero)
                return new ProviderResult(ProviderOutcome.Busy, Message: $"Google asked us to slow down; retrying in {Math.Ceiling(wait.TotalSeconds)} s");
        }

        var query = $"client=gtx&dt=t&dj=1&sl={GoogleCode(source)}&tl={GoogleCode(target)}";
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
            return new ProviderResult(ProviderOutcome.Failed, Message: $"No connection to Google Translate ({e.Message})");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ProviderResult(ProviderOutcome.Failed, Message: "Google Translate timed out");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.Forbidden)
            {
                var pause = Pause(response.Headers.RetryAfter?.Delta);
                return new ProviderResult(ProviderOutcome.Busy, Message: $"Google asked us to slow down; retrying in {Math.Ceiling(pause.TotalSeconds)} s");
            }
            if (!response.IsSuccessStatusCode)
                return new ProviderResult(ProviderOutcome.Failed, Message: $"Google Translate error {(int)response.StatusCode}");

            await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var parsed = Parse(await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false));
            lock (_lock) _backoff = MinBackoff;
            return parsed;
        }
    }

    internal static ProviderResult Parse(JsonDocument doc)
    {
        using (doc)
        {
            var root = doc.RootElement;
            var text = string.Concat(root.TryGetProperty("sentences", out var sentences)
                ? sentences.EnumerateArray()
                    .Where(s => s.TryGetProperty("trans", out _))
                    .Select(s => s.GetProperty("trans").GetString())
                : []);
            string? src = root.TryGetProperty("src", out var s) ? Languages.Normalize(s.GetString() ?? "") : null;
            return text.Length == 0
                ? new ProviderResult(ProviderOutcome.Failed, Message: "Google returned no translation")
                : new ProviderResult(ProviderOutcome.Translated, text, src);
        }
    }

    private TimeSpan Pause(TimeSpan? retryAfter)
    {
        lock (_lock)
        {
            var pause = retryAfter is { } r && r > _backoff ? r : _backoff;
            if (pause > MaxBackoff) pause = MaxBackoff;
            _pausedUntil = _clock.GetUtcNow() + pause;
            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
            return pause;
        }
    }

    /// <summary>Our ISO 639-1 codes to Google's; null means "detect".</summary>
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

    public void Dispose() => _http.Dispose();
}

/// <summary>Optional: DeepL with the user's own API key.</summary>
public sealed class DeepLProvider : ITranslationProvider
{
    private readonly DeepL.Translator _client;

    public DeepLProvider(string apiKey) => _client = new DeepL.Translator(apiKey);

    public string Name => "DeepL";

    public async Task<ProviderResult> TranslateAsync(string text, string? source, string target, CancellationToken ct)
    {
        try
        {
            var result = await _client.TranslateTextAsync(text, source is null ? null : Languages.Normalize(source).ToUpperInvariant(),
                DeepLTarget(target), new DeepL.TextTranslateOptions { PreserveFormatting = true }, ct).ConfigureAwait(false);
            return new ProviderResult(ProviderOutcome.Translated, result.Text, Languages.Normalize(result.DetectedSourceLanguageCode));
        }
        catch (DeepL.QuotaExceededException)
        {
            return new ProviderResult(ProviderOutcome.Failed, Message: "DeepL monthly quota used up");
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
