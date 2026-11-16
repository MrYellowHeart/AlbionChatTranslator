using AlbionChatTranslator.Core.Translation;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static AlbionChatTranslator.Tests.ServiceHelpers;

namespace AlbionChatTranslator.Tests;

public class TranslationServiceTests
{
    private static readonly ChatLanguageDetector Detector = new();

    [Fact]
    public async Task Own_language_short_lines_and_links_never_leave_the_pc()
    {
        var provider = new FakeProvider(t => ("X", "es"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        Assert.Equal(TranslationOutcome.Skipped, (await Submit(svc, "gg")).Outcome);
        Assert.Equal(TranslationOutcome.Skipped, (await Submit(svc, "￿T8_BAG￿ 8.3")).Outcome);
        Assert.Equal(TranslationOutcome.SameLanguage, (await Submit(svc, "anyone want to run a dungeon tonight")).Outcome);
        Assert.Equal(TranslationOutcome.SameLanguage, (await Submit(svc, "lf healer for hg")).Outcome);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task Only_masked_text_is_sent_and_terms_come_back()
    {
        var provider = new FakeProvider(t => (t.Replace("busco grupo para", "looking for group for").Replace("en", "in").Replace("ahora mismo", "right now"), "es"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var r = await Submit(svc, "busco grupo para Hellgate en Martlock ahora mismo ￿T8_BAG￿");

        Assert.Equal(TranslationOutcome.Translated, r.Outcome);
        Assert.Equal(("es", "Fake"), (r.SourceLanguage, r.By));
        Assert.Equal("looking for group for Hellgate in Martlock right now T8_BAG", r.Text);
        var sent = Assert.Single(provider.Sent);
        Assert.DoesNotContain("Martlock", sent);
        Assert.DoesNotContain("T8_BAG", sent);
    }

    [Fact]
    public async Task Lines_arriving_together_share_requests_grouped_by_language()
    {
        var provider = new FakeProvider(t => ("T:" + t, "x"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var results = await SubmitTogether(svc,
            "vamos a la mazmorra corrupta, necesitamos un sanador",
            "alguien sabe donde esta el mercado negro",
            "кто идет в данж сегодня вечером");

        Assert.All(results, r => Assert.Equal(TranslationOutcome.Translated, r.Outcome));
        // Two Spanish lines in one request, the Russian line in another: 2 requests for 3 lines.
        Assert.Equal(2, provider.Calls.Count);
        Assert.Contains(provider.Calls, c => c.Count == 2 && c.All(i => i.SourceHint == "es"));
        Assert.Contains(provider.Calls, c => c.Count == 1 && c[0].SourceHint == "ru");
    }

    [Fact]
    public async Task Repeats_are_answered_from_the_cache()
    {
        var provider = new FakeProvider(t => ("translated: " + t, "pt"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var first = await Submit(svc, "alguem vende bolsa barata em Lymhurst");
        var second = await Submit(svc, "alguem vende bolsa barata em Lymhurst");
        Assert.Equal(first, second);
        Assert.Single(provider.Sent);
    }

    [Fact]
    public async Task Only_lines_that_left_the_pc_are_counted()
    {
        int sent = 0;
        var local = new FakeProvider(i => new ProviderResult(ProviderOutcome.Translated, "x", "es", By: "Offline", LeftDevice: false), "Offline", online: false);
        await using var svc = new TranslationService(Detector, local) { TargetLanguage = "en" };
        svc.Sent += () => Interlocked.Increment(ref sent);

        await Submit(svc, "vamos a la mazmorra corrupta, necesitamos un sanador");
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Service_detection_of_the_readers_language_is_respected()
    {
        var provider = new FakeProvider(t => ("same", "en"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };
        Assert.Equal(TranslationOutcome.SameLanguage, (await Submit(svc, "eae galera bora fazer dg")).Outcome);
    }

    [Fact]
    public async Task Lost_placeholders_fall_back_to_plain_translation()
    {
        var provider = new FakeProvider(t => (t.Contains('[') ? "broken" : "plain: " + t, "es"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var r = await Submit(svc, "vamos todos juntos a Lymhurst esta noche amigos");
        Assert.Equal("plain: vamos todos juntos a Lymhurst esta noche amigos", r.Text);
    }
}

public class ChainProviderTests
{
    private static TranslationItem Item(string t) => new(t, "es");

    [Fact]
    public async Task Later_translators_only_get_what_is_still_untranslated()
    {
        // Primary translates only lines containing "a".
        var primary = new FakeProvider(i => i.Text.Contains('a')
            ? new ProviderResult(ProviderOutcome.Translated, "P:" + i.Text, "es", By: "P", LeftDevice: true)
            : FakeProvider.Busy("P"), "P");
        var backup = new FakeProvider(i => new ProviderResult(ProviderOutcome.Translated, "B:" + i.Text, "es", By: "B"), "B", online: false);
        var chain = new ChainProvider([primary, backup]);

        var r = await chain.TranslateAsync([Item("a1"), Item("b2"), Item("a3")], "en", TestContext.Current.CancellationToken);

        Assert.Equal(["P:a1", "B:b2", "P:a3"], r.Select(x => x.Text));
        Assert.Equal(["b2"], Assert.Single(backup.Calls).Select(i => i.Text));
        Assert.True(r[1].LeftDevice); // it was offered to the online primary first
    }

    [Fact]
    public async Task When_everything_fails_the_first_reason_is_reported()
    {
        var chain = new ChainProvider([new FakeProvider(_ => FakeProvider.Busy("Google"), "Google"), new FakeProvider(_ => new ProviderResult(ProviderOutcome.Failed, Message: "no model"), "Offline")]);
        var r = Assert.Single(await chain.TranslateAsync([Item("x")], "en", TestContext.Current.CancellationToken));
        Assert.Equal((ProviderOutcome.Busy, "Google busy"), (r.Outcome, r.Message));
    }
}

public class GoogleProviderTests
{
    private sealed class Recorder(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Request, string Body)> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request, body));
            return respond(request, body);
        }
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Answers like Google: one sentence per input line, newline kept at the end of each but the last.</summary>
    private static HttpResponseMessage EchoUpper(string body)
    {
        var q = Uri.UnescapeDataString(body["q=".Length..].Replace('+', ' '));
        var lines = q.Split('\n');
        var sentences = new JsonArray(lines.Select((l, i) => (JsonNode)new JsonObject { ["trans"] = l.ToUpperInvariant() + (i < lines.Length - 1 ? "\n" : "") }).ToArray());
        return Json(new JsonObject { ["sentences"] = sentences, ["src"] = "es" }.ToJsonString());
    }

    [Fact]
    public async Task Several_lines_go_in_one_POST_body_never_in_the_url()
    {
        var http = new Recorder((_, body) => EchoUpper(body));
        using var google = new GoogleProvider(new HttpClient(http));

        var r = await google.TranslateAsync([new("hola amigos secretos", "es"), new("buenas noches", "es")], "zh", TestContext.Current.CancellationToken);

        Assert.Equal(["HOLA AMIGOS SECRETOS", "BUENAS NOCHES"], r.Select(x => x.Text));
        Assert.All(r, x => Assert.Equal(("es", "Google", true), (x.SourceLanguage, x.By, x.LeftDevice)));
        var (request, body) = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("translate.googleapis.com", request.RequestUri!.Host);
        Assert.DoesNotContain("secretos", request.RequestUri.ToString());
        Assert.Contains("tl=zh-CN", request.RequestUri.Query);
        Assert.Equal("q=hola+amigos+secretos%0Abuenas+noches", body);
        Assert.False(request.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task If_google_merges_lines_each_line_is_retried_alone()
    {
        var http = new Recorder((_, body) => body.Contains("%0A")
            ? Json("""{"sentences":[{"trans":"ONE MERGED LINE"}],"src":"es"}""")
            : EchoUpper(body));
        using var google = new GoogleProvider(new HttpClient(http));

        var r = await google.TranslateAsync([new("uno", "es"), new("dos", "es")], "en", TestContext.Current.CancellationToken);
        Assert.Equal(["UNO", "DOS"], r.Select(x => x.Text));
        Assert.Equal(3, http.Requests.Count);
    }

    [Fact]
    public async Task After_google_pushes_back_it_steps_aside_without_touching_the_network()
    {
        var http = new Recorder((_, _) => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var google = new GoogleProvider(new HttpClient(http));

        var first = await google.TranslateAsync([new("hola", "es")], "en", TestContext.Current.CancellationToken);
        var second = await google.TranslateAsync([new("hola", "es")], "en", TestContext.Current.CancellationToken);

        Assert.Equal(ProviderOutcome.Busy, first[0].Outcome);
        Assert.True(first[0].LeftDevice);
        Assert.Equal(ProviderOutcome.Busy, second[0].Outcome);
        Assert.False(second[0].LeftDevice);
        Assert.Single(http.Requests);
        Assert.NotNull(google.PausedUntil);
    }

    [Fact]
    public void Empty_or_odd_responses_are_failures_not_crashes()
    {
        Assert.Equal(ProviderOutcome.Failed, GoogleProvider.Parse(JsonDocument.Parse("{}")).Outcome);
        Assert.Equal(ProviderOutcome.Failed, GoogleProvider.Parse(JsonDocument.Parse("""{"sentences":[{"translit":"x"}]}""")).Outcome);
    }

    [Theory]
    [InlineData(null, "auto")]
    [InlineData("zh", "zh-CN")]
    [InlineData("PT-BR", "pt")]
    [InlineData("he", "iw")]
    public void Language_codes_map_to_googles(string? ours, string theirs) =>
        Assert.Equal(theirs, GoogleProvider.GoogleCode(ours));
}

public class MicrosoftProviderTests
{
    [Fact]
    public void Responses_map_back_to_each_line()
    {
        var json = JsonNode.Parse("""
            [ { "detectedLanguage": { "language": "es", "score": 1.0 }, "translations": [ { "text": "Hello", "to": "en" } ] },
              { "detectedLanguage": { "language": "pt", "score": 0.9 }, "translations": [ { "text": "Good night", "to": "en" } ] } ]
            """)!.AsArray();
        var r = MicrosoftProvider.ParseResponse(json, 2);
        Assert.Equal([("Hello", "es"), ("Good night", "pt")], r.Select(x => (x.Text, x.SourceLanguage)));
        Assert.All(r, x => Assert.Equal("Microsoft", x.By));
        Assert.Equal(ProviderOutcome.Failed, MicrosoftProvider.ParseResponse(json, 3)[2].Outcome);
    }

    [Fact]
    public async Task Key_and_region_go_in_headers_and_text_in_the_body()
    {
        HttpRequestMessage? seen = null;
        string body = "";
        var handler = new DelegateHandler(async (req, ct) =>
        {
            seen = req;
            body = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"detectedLanguage":{"language":"es"},"translations":[{"text":"Hi"}]}]""", Encoding.UTF8, "application/json"),
            };
        });
        using var ms = new MicrosoftProvider("secret-key", "westeurope", new HttpClient(handler));

        var r = await ms.TranslateAsync([new TranslationItem("hola", "es")], "zh", TestContext.Current.CancellationToken);

        Assert.Equal("Hi", r[0].Text);
        Assert.Equal("secret-key", seen!.Headers.GetValues("Ocp-Apim-Subscription-Key").Single());
        Assert.Equal("westeurope", seen.Headers.GetValues("Ocp-Apim-Subscription-Region").Single());
        Assert.Contains("to=zh-Hans", seen.RequestUri!.Query);
        Assert.DoesNotContain("hola", seen.RequestUri.ToString());
        Assert.Contains("\"hola\"", body);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => f(request, ct);
    }
}

/// <summary>Talks to the real Google endpoint. Opt-in: set ACT_LIVE_GOOGLE=1.</summary>
public class LiveGoogleTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Real_chat_lines_translate_in_one_shared_request()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable("ACT_LIVE_GOOGLE") != "1", "Set ACT_LIVE_GOOGLE=1 to call Google.");
        using var google = new GoogleProvider();
        int sent = 0;
        await using var svc = new TranslationService(new ChatLanguageDetector(), google) { TargetLanguage = "en" };
        svc.Sent += () => Interlocked.Increment(ref sent);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = await SubmitTogether(svc,
            "busco grupo para Hellgate en Martlock ahora mismo",
            "cuidado, hay gankers en la entrada de Caerleon",
            "vamos a la mazmorra corrupta, necesitamos un sanador");
        output.WriteLine($"{sw.ElapsedMilliseconds} ms for {results.Length} lines, {sent} lines counted as sent");
        foreach (var r in results) output.WriteLine($"[{r.Outcome}/{r.SourceLanguage}/{r.By}] {r.Text}");
        Assert.All(results, r => Assert.Equal(TranslationOutcome.Translated, r.Outcome));
    }
}
