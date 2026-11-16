using AlbionChatTranslator.Core.Translation;
using System.Net;

namespace AlbionChatTranslator.Tests;

public class CatalogTests
{
    private readonly ModelCatalog _catalog = ModelCatalog.LoadEmbedded();

    [Fact]
    public void Every_pack_is_pinned_and_hashed()
    {
        Assert.NotEmpty(_catalog.Packs);
        foreach (var pack in _catalog.Packs)
        {
            Assert.Matches("^[0-9a-f]{40}$", pack.Revision);
            Assert.Equal(6, pack.Files.Count);
            Assert.All(pack.Files, f => Assert.Matches("^[0-9a-f]{64}$", f.Sha256));
        }
    }

    [Fact]
    public void Routes_pivot_through_english()
    {
        Assert.Empty(_catalog.Route("es", "es")!);
        Assert.Equal(["es-en"], _catalog.Route("es", "en")!.Select(s => s.Pack.Id));
        Assert.Equal(["ROMANCE-en", "en-de"], _catalog.Route("pt", "de")!.Select(s => s.Pack.Id));

        var toPt = Assert.Single(_catalog.Route("en", "pt")!);
        Assert.Equal((">>pt_br<<", "en-ROMANCE"), (toPt.TargetPrefix, toPt.Pack.Id));

        Assert.Null(_catalog.Route("en", "ko")); // no English->Korean model exists
    }

    [Fact]
    public void Every_language_can_be_read_and_targets_are_listed()
    {
        Assert.All(Languages.All, l => Assert.True(_catalog.CanTranslateFrom(l.Code), l.Code));
        Assert.Contains(_catalog.TargetLanguages, l => l.Code == "pt");
    }
}

public sealed class ModelStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "act-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private sealed class FakeHttp(Func<Uri, byte[]> body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body(request.RequestUri!)) });
        }
    }

    private static ModelPack Pack(byte[] content) => new("xx-en", "Xenova/opus-mt-xx-en", new string('a', 40),
        [new ModelFile("config.json", content.Length, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)))]);

    [Fact]
    public async Task Downloads_verifies_and_reuses()
    {
        var content = "{\"ok\":true}"u8.ToArray();
        var http = new FakeHttp(_ => content);
        var store = new ModelStore(_dir, new HttpClient(http));
        var pack = Pack(content);

        var dir = await store.EnsureInstalledAsync(pack, TestContext.Current.CancellationToken);
        Assert.True(store.IsInstalled(pack));
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(dir, "config.json"), TestContext.Current.CancellationToken));
        Assert.Equal("https://huggingface.co/Xenova/opus-mt-xx-en/resolve/" + new string('a', 40) + "/config.json", http.Requests.Single().ToString());

        await store.EnsureInstalledAsync(pack, TestContext.Current.CancellationToken);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task Tampered_files_are_rejected_and_deleted()
    {
        var store = new ModelStore(_dir, new HttpClient(new FakeHttp(_ => "{\"evil\":1}"u8.ToArray())));
        var pack = Pack("{\"ok\":true}"u8.ToArray());

        await Assert.ThrowsAnyAsync<InvalidDataException>(() => store.EnsureInstalledAsync(pack, TestContext.Current.CancellationToken));
        Assert.False(store.IsInstalled(pack));
        Assert.Empty(Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Paths_cannot_escape_the_pack_folder() =>
        Assert.Throws<InvalidDataException>(() => ModelStore.SafeCombine(_dir, "../../evil.dll"));
}


/// <summary>
/// Real offline models. They download ~230 MB once, so they only run when ACT_MODEL_DIR points
/// at a folder to keep them in.
/// </summary>
public class RealOfflineTests(ITestOutputHelper output)
{
    private static readonly string? ModelDir = Environment.GetEnvironmentVariable("ACT_MODEL_DIR");

    [Fact]
    public async Task When_google_is_busy_the_offline_model_takes_over()
    {
        Assert.SkipWhen(ModelDir is null, "Set ACT_MODEL_DIR to run real-model tests.");
        var catalog = ModelCatalog.LoadEmbedded();
        var store = new ModelStore(ModelDir!);
        await store.EnsureInstalledAsync(catalog.Packs.Single(p => p.Id == "es-en"), TestContext.Current.CancellationToken);
        using var offline = new OfflineProvider(catalog, store) { AutoDownload = false };
        var busyGoogle = new FakeProvider(_ => FakeProvider.Busy("Google"), "Google");
        int sent = 0;
        await using var svc = new TranslationService(new ChatLanguageDetector(), new ChainProvider([busyGoogle, offline])) { TargetLanguage = "en" };
        svc.Sent += () => Interlocked.Increment(ref sent);

        var r = await ServiceHelpers.Submit(svc, "busco grupo para Hellgate en Martlock ahora mismo");
        output.WriteLine($"[{r.Outcome}/{r.SourceLanguage}/{r.By}] {r.Text}");

        Assert.Equal(TranslationOutcome.Translated, r.Outcome);
        Assert.Equal("Offline", r.By);
        Assert.Contains("Martlock", r.Text);
        Assert.Equal(1, offline.LoadedModelCount);
        offline.UnloadIfIdle(force: true);
        Assert.Equal(0, offline.LoadedModelCount);
    }
}
