using AlbionChatTranslator.Core.Translation;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AlbionChatTranslator.Tests;

public class TranslationServiceTests
{
    private static readonly ChatLanguageDetector Detector = new();

    private sealed class FakeProvider(Func<string, (string Text, string Src)> translate) : ITranslationProvider
    {
        public readonly List<string> Sent = [];
        public string Name => "fake";

        public Task<ProviderResult> TranslateAsync(string text, string? source, string target, CancellationToken ct)
        {
            lock (Sent) Sent.Add(text);
            var (t, src) = translate(text);
            return Task.FromResult(new ProviderResult(ProviderOutcome.Translated, t, src));
        }

        public void Dispose() { }
    }

    private static async Task<TranslationResult> Submit(TranslationService svc, string text)
    {
        var tcs = new TaskCompletionSource<TranslationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        svc.Submit(text, tcs.SetResult);
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

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
        Assert.Equal("es", r.SourceLanguage);
        Assert.Equal("looking for group for Hellgate in Martlock right now T8_BAG", r.Text);
        var sent = Assert.Single(provider.Sent);
        Assert.DoesNotContain("Martlock", sent);
        Assert.DoesNotContain("T8_BAG", sent);
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
    public async Task Service_detection_of_the_readers_language_is_respected()
    {
        // The local check was unsure, Google says it is English: show it as-is.
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

public class GoogleProviderTests
{
    private sealed class Recorder(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Request, string Body)> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request, body));
            return respond(request);
        }
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private const string Sample = """{"sentences":[{"trans":"Hello ","orig":"Hola "},{"trans":"friends","orig":"amigos"}],"src":"es"}""";

    [Fact]
    public async Task Text_goes_in_a_POST_body_never_in_the_url()
    {
        var http = new Recorder(_ => Json(Sample));
        using var google = new GoogleProvider(new HttpClient(http));

        var r = await google.TranslateAsync("Hola amigos secretos", null, "zh", TestContext.Current.CancellationToken);

        Assert.Equal((ProviderOutcome.Translated, "Hello friends", "es"), (r.Outcome, r.Text, r.SourceLanguage));
        var (request, body) = Assert.Single(http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https", request.RequestUri!.Scheme);
        Assert.Equal("translate.googleapis.com", request.RequestUri.Host);
        Assert.DoesNotContain("secretos", request.RequestUri.ToString());
        Assert.Contains("sl=auto", request.RequestUri.Query);
        Assert.Contains("tl=zh-CN", request.RequestUri.Query);
        Assert.Equal("q=Hola+amigos+secretos", body);
        Assert.False(request.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task Rate_limiting_pauses_requests_instead_of_hammering()
    {
        var http = new Recorder(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var google = new GoogleProvider(new HttpClient(http));

        var first = await google.TranslateAsync("hola", null, "en", TestContext.Current.CancellationToken);
        var second = await google.TranslateAsync("hola", null, "en", TestContext.Current.CancellationToken);

        Assert.Equal(ProviderOutcome.Busy, first.Outcome);
        Assert.Equal(ProviderOutcome.Busy, second.Outcome);
        Assert.Single(http.Requests); // the second call never reached the network
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

/// <summary>Talks to the real Google endpoint. Opt-in: set ACT_LIVE_GOOGLE=1.</summary>
public class LiveGoogleTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Real_chat_lines_translate_with_terms_intact()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable("ACT_LIVE_GOOGLE") != "1", "Set ACT_LIVE_GOOGLE=1 to call Google.");
        using var google = new GoogleProvider();
        await using var svc = new TranslationService(new ChatLanguageDetector(), google) { TargetLanguage = "en" };

        string[] lines =
        [
            "busco grupo para Hellgate en Martlock ahora mismo",
            "alguem vende bolsa t6.1 barata em Lymhurst?",
            "кто идет в данж сегодня вечером",
            "cuidado, hay gankers en la entrada de Caerleon",
            "ich suche eine aktive Gilde für ZvZ",
        ];
        foreach (var line in lines)
        {
            var tcs = new TaskCompletionSource<TranslationResult>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            svc.Submit(line, tcs.SetResult);
            var r = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            output.WriteLine($"{sw.ElapsedMilliseconds,5} ms [{r.Outcome}/{r.SourceLanguage}] {line} => {r.Text}");
            Assert.Equal(TranslationOutcome.Translated, r.Outcome);
        }

        var reply = await svc.TranslateReplyAsync("I can heal, invite me to the party in Martlock", "pt", TestContext.Current.CancellationToken);
        output.WriteLine($"reply en->pt: {reply.Text}");
        Assert.Contains("Martlock", reply.Text);
    }
}
