using AlbionChatTranslator.Core.Chat;
using StatisticsAnalysisTool.Network;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;

namespace AlbionChatTranslator.Tests;

public class RegionalSlangTests
{
    [Theory]
    [InlineData("vc vai pra hg?", "você vai pra hg?")]
    [InlineData("blz vlw tmj", "beleza valeu estamos juntos")]
    [InlineData("n sei oq fazer", "não sei o que fazer")]
    [InlineData("kkkkk boa", "haha boa")]
    [InlineData("bora pt", "bora grupo")]
    public void Portuguese_shorthand_is_expanded(string input, string expected) =>
        Assert.Equal(expected, ChatSlang.Expand(input, "pt"));

    [Theory]
    [InlineData("xq no vienes", "porque no vienes")]
    [InlineData("ntp amigo", "no te preocupes amigo")]
    [InlineData("jajajaja q bueno", "jaja que bueno")]
    [InlineData("busco pt", "busco grupo")]
    public void Spanish_shorthand_is_expanded(string input, string expected) =>
        Assert.Equal(expected, ChatSlang.Expand(input, "es"));

    [Fact]
    public void Shorthand_is_only_expanded_in_its_own_language()
    {
        // "q" and "vc" mean something else outside Portuguese/Spanish chat: leave them alone.
        Assert.Equal("q for hg", ChatSlang.Expand("q for hg", "en"));
        Assert.Equal("vc ok", ChatSlang.Expand("vc ok", "de"));
    }

    [Theory]
    [InlineData("eae galera bora fazer dg", "pt")]
    [InlineData("vc vem hj?", "pt")]
    [InlineData("ntp, xq no vienes", "es")]
    [InlineData("porfa ayuda jaja", "es")]
    public void Slang_identifies_the_language(string line, string expected) =>
        Assert.Equal(expected, new ChatLanguageDetector().Detect(Glossary.Protect(line).Text, "en"));
}

public class LanguagePlanTests
{
    private static readonly ModelCatalog Catalog = ModelCatalog.LoadEmbedded();

    [Fact]
    public void Americas_preselects_its_main_languages_except_the_readers_own()
    {
        Assert.Equal(["es", "pt"], ServerLanguages.Preset(GameServer.Americas, "en"));
        Assert.Equal(["en", "es"], ServerLanguages.Preset(GameServer.Americas, "pt"));
    }

    [Theory]
    [InlineData(GameServer.Europe)]
    [InlineData(GameServer.Asia)]
    [InlineData(GameServer.Unknown)]
    public void Nothing_is_assumed_for_other_servers(GameServer server) =>
        Assert.Empty(ServerLanguages.Preset(server, "en"));

    [Fact]
    public void Packs_cover_reading_and_replying()
    {
        var ids = Catalog.PacksFor("en", ["es", "pt"]).Select(p => p.Id).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["ROMANCE-en", "en-ROMANCE", "en-es", "es-en"], ids);
    }

    [Fact]
    public void Non_english_readers_pivot_through_english()
    {
        var ids = Catalog.PacksFor("pt", ["es"]).Select(p => p.Id).ToHashSet();
        Assert.Contains("es-en", ids);      // read Spanish: es→en→pt
        Assert.Contains("en-ROMANCE", ids);
        Assert.Contains("ROMANCE-en", ids); // reply in Spanish: pt→en→es
        Assert.Contains("en-es", ids);
        Assert.Empty(Catalog.PacksFor("en", ["en"]));
    }

    [Fact]
    public void Replying_offline_needs_a_model_into_that_language()
    {
        Assert.True(Catalog.CanReplyOffline("en", "pt"));
        Assert.False(Catalog.CanReplyOffline("en", "ko")); // there is no English→Korean pack
    }
}

public class TabDefaultsTests
{
    [Fact]
    public void Combat_tab_is_never_listed()
    {
        var s = new AppSettings();
        s.MergeTabs([new ChatTab("General", [23], "GENERAL"), new ChatTab("Combat", [36], "COMBAT")]);
        Assert.Equal(["General"], s.Tabs.Select(t => t.Name));
        Assert.True(s.Tabs[0].Enabled);

        // The user's choice on other tabs is kept.
        s.Tabs[0].Enabled = false;
        s.MergeTabs([new ChatTab("General", [23], "GENERAL"), new ChatTab("Combat", [36], "COMBAT")]);
        Assert.False(s.Tabs[0].Enabled);
    }

    [Fact]
    public void Upgrading_removes_a_saved_combat_tab()
    {
        var dir = Path.Combine(Path.GetTempPath(), "act-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                """{"Version":3,"TargetLanguage":"en","Tabs":[{"Name":"General","ChannelIds":[23],"Enabled":true,"GameKey":"GENERAL"},{"Name":"Combat","ChannelIds":[36],"Enabled":false,"GameKey":"COMBAT"}]}""");
            var s = AppSettings.Load(dir);
            Assert.Equal(["General"], s.Tabs.Select(t => t.Name));
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class EventShapeTests
{
    [Fact]
    public void Layout_is_logged_without_names_or_text()
    {
        var p = new Dictionary<byte, object>
        {
            [0] = 18,
            [1] = "SomePlayer",
            [2] = "hello there",
            [3] = (byte)5,
            [4] = new object[] { "@CHAT_CHANNEL_HELP", (short)7, "Guild Name" },
            [252] = (short)99,
        };
        var shape = EventShape.Describe(p, 1, 2);
        Assert.Equal("""[0]=int 18 [1]=text(10) [2]=text(11) [3]=byte 5 [4]=object[](3){"@CHAT_CHANNEL_HELP", short 7, text(10)}""", shape);
        Assert.DoesNotContain("SomePlayer", shape);
        Assert.DoesNotContain("Guild", shape);
    }

    [Fact]
    public void Channel_events_and_first_messages_reach_the_diagnostic_log()
    {
        var parser = new ChatEventParser();
        var notes = new List<string>();
        parser.Diagnostic += notes.Add;
        parser.Handle(new EventPacket((short)EventCodes.JoinedChatChannel, new Dictionary<byte, object> { [0] = 18, [1] = "@CHAT_CHANNEL_HELP" }));
        var msg = new Dictionary<byte, object> { [0] = 18, [1] = "Ana", [2] = "alguem ajuda" };
        parser.Handle(new EventPacket((short)EventCodes.ChatMessage, msg));
        parser.Handle(new EventPacket((short)EventCodes.ChatMessage, msg));

        Assert.Contains(notes, n => n.StartsWith("event JoinedChatChannel: [0]=int 18 [1]=\"@CHAT_CHANNEL_HELP\"", StringComparison.Ordinal));
        Assert.Single(notes, n => n.StartsWith("first ChatMessage on channel 18", StringComparison.Ordinal));
        Assert.DoesNotContain(notes, n => n.Contains("Ana") || n.Contains("alguem"));
    }
}

/// <summary>Real offline models with gamer slang and Albion terms (needs ACT_MODEL_DIR).</summary>
public class RealSlangTests(ITestOutputHelper output)
{
    private static readonly string? ModelDir = Environment.GetEnvironmentVariable("ACT_MODEL_DIR");

    [Theory]
    [InlineData("vc vai pra Hellgate hj? bora pt", "pt", new[] { "you", "Hellgate", "today" })]
    [InlineData("n tenho prata pra comprar a bolsa, vlw", "pt", new[] { "don't", "silver" })]
    [InlineData("xq no vienes a la Hellgate? ntp", "es", new[] { "why", "Hellgate" })]
    [InlineData("busco pt para Martlock porfa", "es", new[] { "group", "Martlock" })]
    public async Task Slang_and_terms_survive_offline_translation(string line, string lang, string[] mustContain)
    {
        Assert.SkipWhen(ModelDir is null, "Set ACT_MODEL_DIR to run real-model tests.");
        var catalog = ModelCatalog.LoadEmbedded();
        var store = new ModelStore(ModelDir!);
        foreach (var p in catalog.PacksFor("en", [lang]).Where(p => p.Id.EndsWith("-en", StringComparison.Ordinal)))
            await store.EnsureInstalledAsync(p, TestContext.Current.CancellationToken);
        using var offline = new OfflineProvider(catalog, store) { AutoDownload = false };
        await using var svc = new TranslationService(new ChatLanguageDetector(), new ChainProvider([offline])) { TargetLanguage = "en" };

        var r = await ServiceHelpers.Submit(svc, line);
        output.WriteLine($"[{r.Outcome}/{r.SourceLanguage}/{r.By}] {line} -> {r.Text}");

        Assert.Equal(TranslationOutcome.Translated, r.Outcome);
        Assert.Equal(lang, r.SourceLanguage);
        foreach (var word in mustContain)
            Assert.Contains(word, r.Text, StringComparison.OrdinalIgnoreCase);
    }
}
