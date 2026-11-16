using AlbionChatTranslator.Core.Capture;
using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Ipc;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;

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

public sealed class ProviderMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "act-migrate-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Earlier_2_0_settings_load_with_no_key_service()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            "{ \"Version\": 2, \"TargetLanguage\": \"de\", \"Provider\": \"Offline\", \"AutoDownloadLanguagePacks\": true }");
        var s = AppSettings.Load(_dir);
        Assert.Equal((KeyService.None, "de"), (s.KeyService, s.TargetLanguage));
    }

    [Fact]
    public void V1_users_with_a_deepl_key_stay_on_deepl()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ \"ApiKey\": \"k:fx\" }");
        Assert.Equal(KeyService.DeepL, AppSettings.Load(_dir).KeyService);
        Directory.Delete(_dir, true);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ \"TargetLanguage\": \"FR\" }");
        Assert.Equal(KeyService.None, AppSettings.Load(_dir).KeyService);
    }
}
