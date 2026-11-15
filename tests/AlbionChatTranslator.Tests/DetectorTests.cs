using AlbionChatTranslator.Core.Translation;

namespace AlbionChatTranslator.Tests;

/// <summary>Realistic chat lines. "-" means "unsure" (shown untranslated), which is always acceptable for English.</summary>
public class DetectorTests
{
    private static readonly ChatLanguageDetector Detector = new();

    public static TheoryData<string> EnglishLines =>
    [
        "selling bags cheap in the market", "anyone want to do some dungeons tonight", "lf healer for hg", "gg wp",
        "brb 5 min", "need 2 dps for hce", "wts t8 mount pst", "where is the black market", "my internet is so bad today",
        "who wants to go fame farming", "caerleon is so laggy", "lets go boys", "recruiting active players for zvz", "ok",
        "hello everyone", "im new to the game", "tim guild viet nam", "saya cari guild",
    ];

    [Theory]
    [MemberData(nameof(EnglishLines))]
    public void English_chat_is_never_sent_to_a_translator(string line)
    {
        var detected = Detector.Detect(Glossary.Protect(line).Text, "en");
        Assert.True(detected is null or "en", $"'{line}' was detected as {detected}");
    }

    private static readonly (string Text, string Expected)[] Foreign =
    [
        ("cuidado, hay gankers en la entrada de Caerleon", "es|pt"), ("alguem vende bolsa barata", "pt"), ("oi tudo bem", "pt"),
        ("guten abend zusammen", "de"), ("привет всем", "ru"), ("kto idzie na dungeon", "pl"), ("busco gremio pvp activo", "es|pt"),
        ("bom dia pessoal", "pt"), ("vamos a la mazmorra necesitamos un sanador", "es"), ("quelqu'un pour un donjon", "fr"),
        ("chi viene in dungeon", "it"), ("mam pytanie o gildie", "pl"), ("ich suche eine gilde", "de"), ("jemand lust auf gruppe", "de"),
        ("hola que tal amigos", "es"), ("je cherche une guilde francaise", "fr"), ("alguien sabe donde esta el mercado negro", "es"),
        ("eae galera bora fazer dg", "pt"), ("кто идет в данж", "ru"), ("hepsi gelsin", "tr"), ("türk lonca arıyorum", "tr"),
        ("找公会", "zh"), ("길드 구해요", "ko"), ("ギルド募集", "ja"), ("ik zoek een guild", "nl|de"),
    ];

    [Fact]
    public void Foreign_chat_is_mostly_identified()
    {
        var wrong = Foreign
            .Select(f => (f.Text, f.Expected, Got: Detector.Detect(Glossary.Protect(f.Text).Text, "en") ?? "-"))
            .Where(r => !r.Expected.Split('|').Contains(r.Got))
            .ToList();
        // Known misses on very short or slangy lines are acceptable; a drop below 85% is not.
        Assert.True(wrong.Count <= Foreign.Length * 15 / 100,
            "Misidentified: " + string.Join("; ", wrong.Select(w => $"{w.Text} -> {w.Got}")));
    }

    [Fact]
    public void Readers_own_language_wins_close_calls()
    {
        // Spanish vs Portuguese is close on this line; a Spanish reader should not get it "translated".
        Assert.Equal("es", Detector.Detect("hola que tal amigos", "es"));
        Assert.Equal("pt", Detector.Detect("alguem vende bolsa barata", "pt"));
    }
}
