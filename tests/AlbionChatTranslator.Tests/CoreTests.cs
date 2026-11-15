using AlbionChatTranslator.Core.Capture;
using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Ipc;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Net;

namespace AlbionChatTranslator.Tests;

public class GlossaryTests
{
    [Fact]
    public void Terms_tiers_and_item_links_round_trip()
    {
        const string text = "lf healer for HG in Caerleon, need t8.3 ￿T8_2H_HOLYSTAFF￿ gear";
        var p = Glossary.Protect(text);

        Assert.DoesNotContain("Caerleon", p.Text);
        Assert.DoesNotContain("￿", p.Text);
        Assert.Equal(["T8_2H_HOLYSTAFF", "HG", "Caerleon", "t8.3"], p.Originals);

        var restored = Glossary.Restore(p.Text, p);
        Assert.Equal("lf healer for HG in Caerleon, need t8.3 T8_2H_HOLYSTAFF gear", restored);
    }

    [Fact]
    public void Terms_inside_words_are_left_alone()
    {
        var p = Glossary.Protect("FSX is not FS and HGW is not HG");
        Assert.Equal(["FS", "HG"], p.Originals);
    }

    [Fact]
    public void Restore_returns_null_when_a_placeholder_is_lost_or_duplicated()
    {
        var p = Glossary.Protect("meet in Martlock and Lymhurst");
        Assert.Null(Glossary.Restore("treffen in [0]", p));
        Assert.Null(Glossary.Restore("[0] [0] [1]", p));
        Assert.Equal("Martlock Lymhurst", Glossary.Restore("[ 0 ] [1]", p));
    }

    [Theory]
    [InlineData("[0] [1]", false)]
    [InlineData("8.3 !!", false)]
    [InlineData("[0] vamos", true)]
    public void Detects_whether_anything_is_left_to_translate(string masked, bool expected) =>
        Assert.Equal(expected, Glossary.HasTranslatableContent(masked));
}

public class CaptureTests
{
    private static byte[] Udp(ushort src, ushort dst, byte[] payload, ushort fragment = 0)
    {
        var f = new byte[28 + payload.Length];
        f[0] = 0x45; f[9] = 17;
        f[6] = (byte)(fragment >> 8); f[7] = (byte)fragment;
        f[20] = (byte)(src >> 8); f[21] = (byte)src;
        f[22] = (byte)(dst >> 8); f[23] = (byte)dst;
        int len = 8 + payload.Length;
        f[24] = (byte)(len >> 8); f[25] = (byte)len;
        payload.CopyTo(f, 28);
        return f;
    }

    [Fact]
    public void Extracts_payload_for_albion_ports_only()
    {
        Assert.True(RawSocketCapture.TryExtractAlbionPayload(Udp(5056, 50000, [1, 2, 3]), out int start, out int len));
        Assert.Equal((28, 3), (start, len));
        Assert.True(RawSocketCapture.TryExtractAlbionPayload(Udp(50000, 4535, [9]), out _, out _));
        Assert.False(RawSocketCapture.TryExtractAlbionPayload(Udp(53, 50000, [1]), out _, out _));
    }

    [Fact]
    public void Rejects_tcp_truncated_and_non_first_fragments()
    {
        var tcp = Udp(5056, 1, [1]);
        tcp[9] = 6;
        Assert.False(RawSocketCapture.TryExtractAlbionPayload(tcp, out _, out _));
        Assert.False(RawSocketCapture.TryExtractAlbionPayload(Udp(5056, 1, [1])[..20], out _, out _));
        Assert.False(RawSocketCapture.TryExtractAlbionPayload(Udp(5056, 1, [1], fragment: 0x0010), out _, out _));
    }
}

public class PipeProtocolTests
{
    [Fact]
    public void Lines_round_trip()
    {
        var line = new ChatLine(DateTimeOffset.Parse("2026-11-15T10:00:00+00:00"), 31, ChannelKind.Say, "Bob", "hola");
        var json = PipeProtocol.Serialize(new PipeMessage { Type = PipeMessage.TypeLine, Line = line });
        var back = PipeProtocol.TryDeserialize(json);
        Assert.Equal(line, back?.Line);
        Assert.Contains("\"kind\":\"Say\"", json);
    }

    [Fact]
    public void Garbage_and_oversized_input_is_ignored()
    {
        Assert.Null(PipeProtocol.TryDeserialize("{not json"));
        Assert.Null(PipeProtocol.TryDeserialize(new string('a', PipeProtocol.MaxLineLength + 1)));
    }

    [Fact]
    public void Only_generated_pipe_names_are_accepted()
    {
        Assert.True(PipeProtocol.IsValidPipeName(PipeProtocol.NewPipeName()));
        Assert.False(PipeProtocol.IsValidPipeName("AlbionChatTranslator-..\\evil"));
        Assert.False(PipeProtocol.IsValidPipeName("other"));
    }
}

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

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "act-settings-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void V1_settings_migrate_and_the_plain_text_key_is_encrypted()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """
            { "ApiKey": "secret-key:fx", "TargetLanguage": "PT-BR", "BlockGlobalChannels": false,
              "Tabs": [ { "Name": "General", "ChannelIds": [31, 28], "Enabled": false } ] }
            """);

        var s = AppSettings.Load(_dir);
        Assert.Equal("pt", s.TargetLanguage);
        Assert.True(s.IncludeGlobalChannels);
        Assert.Equal("secret-key:fx", s.DeepLKey);
        Assert.False(Assert.Single(s.Tabs).Enabled);

        s.Save(_dir);
        var saved = File.ReadAllText(Path.Combine(_dir, "settings.json"));
        Assert.DoesNotContain("secret-key", saved);
        Assert.Equal("secret-key:fx", AppSettings.Load(_dir).DeepLKey);
    }

    [Fact]
    public void Merging_game_tabs_keeps_user_choices()
    {
        var s = new AppSettings { Tabs = [new TabSetting { Name = "Guild", ChannelIds = [24], Enabled = false }] };
        s.MergeTabs([new ChatTab("Guild", [24, 25]), new ChatTab("Trade", [8])]);
        Assert.Equal([("Guild", false), ("Trade", true)], s.Tabs.Select(t => (t.Name, t.Enabled)));
        Assert.Equal([24, 25], s.Tabs[0].ChannelIds);
    }
}

public class TranslationServiceTests
{
    private static readonly ChatLanguageDetector Detector = new();

    private sealed class FakeProvider(Func<string, string> translate) : ITranslationProvider
    {
        public int Calls;
        public string Name => "fake";
        public bool CanTranslate(string s, string t) => true;
        public bool IsReady(string s, string t) => true;

        public Task<ProviderResult> TranslateAsync(string text, string source, string target, bool wait, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new ProviderResult(ProviderOutcome.Translated, translate(text)));
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
    public async Task Short_lines_and_own_language_never_reach_the_model()
    {
        var provider = new FakeProvider(_ => "X");
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        Assert.Equal(TranslationOutcome.Skipped, (await Submit(svc, "gg")).Outcome);
        Assert.Equal(TranslationOutcome.Skipped, (await Submit(svc, "￿T8_BAG￿ 8.3")).Outcome);
        Assert.Equal(TranslationOutcome.SameLanguage, (await Submit(svc, "anyone want to run a dungeon tonight")).Outcome);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Foreign_lines_translate_once_then_come_from_cache_with_terms_restored()
    {
        var provider = new FakeProvider(t => t.Replace("busco grupo para", "looking for group for"));
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var first = await Submit(svc, "busco grupo para Hellgate en Martlock ahora mismo");
        var second = await Submit(svc, "busco grupo para Hellgate en Martlock ahora mismo");

        Assert.Equal(TranslationOutcome.Translated, first.Outcome);
        Assert.Equal("es", first.SourceLanguage);
        Assert.Equal("looking for group for Hellgate en Martlock ahora mismo", first.Text);
        Assert.Equal(first, second);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task Lost_placeholders_fall_back_to_plain_translation()
    {
        var provider = new FakeProvider(t => t.Contains('[') ? "broken" : "plain: " + t);
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var r = await Submit(svc, "vamos todos juntos a Lymhurst esta noche amigos");
        Assert.Equal("plain: vamos todos juntos a Lymhurst esta noche amigos", r.Text);
    }
}
