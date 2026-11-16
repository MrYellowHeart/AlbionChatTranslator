using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using static AlbionChatTranslator.Tests.ServiceHelpers;

namespace AlbionChatTranslator.Tests;

public class ChatSlangTests
{
    [Theory]
    [InlineData("lf healer for hg pls", "looking for healer for hg please")]
    [InlineData("wts bag, pst", "want to sell bag, please send me a private message")]
    [InlineData("inv me to party brb", "invite me to group be right back")]
    [InlineData("LF tank [0] rn", "looking for tank [0] right now")]
    [InlineData("selfie is not lf", "selfie is not looking for")]
    public void Shorthand_becomes_plain_english(string input, string expected) =>
        Assert.Equal(expected, ChatSlang.Expand(input));

    [Fact]
    public void Words_that_merely_contain_shorthand_are_untouched() =>
        Assert.Equal("wolf plsmith thxx", ChatSlang.Expand("wolf plsmith thxx"));
}

public class CustomTermTests
{
    [Fact]
    public void User_terms_are_masked_and_restored()
    {
        try
        {
            Glossary.SetCustomTerms(["Zorbalords", " Mega Guild "]);
            var p = Glossary.Protect("join Zorbalords or mega guild today");
            Assert.DoesNotContain("Zorbalords", p.Text);
            Assert.DoesNotContain("mega guild", p.Text);
            Assert.Equal("join Zorbalords or mega guild today", Glossary.Restore(p.Text, p));
        }
        finally
        {
            Glossary.SetCustomTerms([]);
        }
    }
}

public class PrivacyControlTests
{
    private static readonly ChatLanguageDetector Detector = new();

    private static FakeProvider Recording(string src) => new(t => ("T:" + t, src));

    [Fact]
    public async Task Languages_the_reader_understands_are_never_sent()
    {
        var provider = Recording("es");
        await using var svc = new TranslationService(Detector, provider)
        {
            TargetLanguage = "en",
            ReadLanguages = new HashSet<string> { "es" },
        };

        var r = await Submit(svc, "vamos a la mazmorra corrupta, necesitamos un sanador");
        Assert.Equal(TranslationOutcome.SameLanguage, r.Outcome);
        Assert.Empty(provider.Sent);
    }

    [Fact]
    public async Task Readers_extra_language_reported_by_the_service_is_shown_as_is()
    {
        var provider = Recording("pt");
        await using var svc = new TranslationService(Detector, provider)
        {
            TargetLanguage = "en",
            ReadLanguages = new HashSet<string> { "pt" },
        };
        // Short slangy Portuguese the local check is unsure about; the service says Portuguese.
        Assert.Equal(TranslationOutcome.SameLanguage, (await Submit(svc, "eae galera bora fazer dg")).Outcome);
    }

    [Fact]
    public async Task English_shorthand_is_expanded_before_sending_for_non_english_readers()
    {
        var provider = Recording("en");
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "pt" };

        await Submit(svc, "lf healer for hg pls");
        Assert.Equal("looking for healer for [0] please", Assert.Single(provider.Sent));
    }

    [Fact]
    public async Task Replies_in_english_shorthand_are_expanded_too()
    {
        var provider = Recording("en");
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        var r = await svc.TranslateReplyAsync("inv me to party pls", "pt", TestContext.Current.CancellationToken);
        Assert.Equal("invite me to group please", Assert.Single(provider.Sent));
        Assert.Equal("T:invite me to group please", r.Text);
    }

    [Fact]
    public async Task Non_english_text_is_not_expanded()
    {
        var provider = Recording("pl");
        await using var svc = new TranslationService(Detector, provider) { TargetLanguage = "en" };

        // "np" and "ty" are ordinary Polish words here.
        await Submit(svc, "ty idziesz np do lochu dzisiaj wieczorem");
        Assert.Equal("ty idziesz np do lochu dzisiaj wieczorem", Assert.Single(provider.Sent));
    }
}

public sealed class CacheAndSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "act-cache-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static FakeProvider Counting() => new(t => ("T:" + t, "es"));

    [Fact]
    public async Task Saved_translations_are_reused_after_a_restart_and_can_be_forgotten()
    {
        var path = Path.Combine(_dir, "cache.json");
        const string line = "vamos a la mazmorra corrupta, necesitamos un sanador";
        var detector = new ChatLanguageDetector();

        var first = Counting();
        await using (var svc = new TranslationService(detector, first) { TargetLanguage = "en" })
        {
            await Submit(svc, line);
            svc.SaveCache(path);
        }
        Assert.Single(first.Calls);

        var second = Counting();
        await using (var svc = new TranslationService(detector, second) { TargetLanguage = "en" })
        {
            svc.LoadCache(path);
            var r = await Submit(svc, line);
            Assert.Equal(TranslationOutcome.Translated, r.Outcome);
            Assert.Empty(second.Calls);

            svc.ClearCache(path);
            Assert.False(File.Exists(path));
        }
    }

    [Fact]
    public void Expired_entries_are_dropped()
    {
        var path = Path.Combine(_dir, "old.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, $$"""[{"Key":"en\u0001hola amigos","Text":"hi friends","Source":"es","SavedAt":"{{DateTimeOffset.UtcNow.AddDays(-8):O}}"}]""");
        var svc = new TranslationService(new ChatLanguageDetector(), Counting());
        svc.LoadCache(path);
        var hit = false;
        svc.Submit("hola amigos", r => hit = r.Text == "hi friends" && r.Outcome == TranslationOutcome.Translated);
        Assert.False(hit);
    }

    [Fact]
    public void New_settings_round_trip_with_an_unset_overlay_position()
    {
        var s = new AppSettings
        {
            ReadLanguages = ["es", "pt"],
            NeverSendChannels = [ChannelKind.Whisper],
            Mode = TranslateMode.OnClick,
            CustomTerms = ["Zorbalords"],
        };
        Assert.True(double.IsNaN(s.OverlayLeft));
        s.Save(_dir);

        var back = AppSettings.Load(_dir);
        Assert.Equal(["es", "pt"], back.ReadLanguages);
        Assert.Equal([ChannelKind.Whisper], back.NeverSendChannels);
        Assert.Equal(TranslateMode.OnClick, back.Mode);
        Assert.Equal(["Zorbalords"], back.CustomTerms);
        Assert.True(double.IsNaN(back.OverlayLeft));
        Assert.False(back.KeepCacheOnDisk);
        Assert.Contains("\"Whisper\"", File.ReadAllText(Path.Combine(_dir, "settings.json")));
    }
}
