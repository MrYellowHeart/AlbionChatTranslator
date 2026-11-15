using LinguaLanguage = Lingua.Language;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>A language the app can detect and (at least one way) translate.</summary>
/// <param name="Code">ISO 639-1 code, lower case (e.g. "pt").</param>
public sealed record LanguageInfo(string Code, string Name, LinguaLanguage Lingua);

public static class Languages
{
    public const string English = "en";

    public static readonly IReadOnlyList<LanguageInfo> All =
    [
        new("ar", "Arabic", LinguaLanguage.Arabic),
        new("cs", "Czech", LinguaLanguage.Czech),
        new("da", "Danish", LinguaLanguage.Danish),
        new("de", "German", LinguaLanguage.German),
        new("en", "English", LinguaLanguage.English),
        new("es", "Spanish", LinguaLanguage.Spanish),
        new("et", "Estonian", LinguaLanguage.Estonian),
        new("fi", "Finnish", LinguaLanguage.Finnish),
        new("fr", "French", LinguaLanguage.French),
        new("hi", "Hindi", LinguaLanguage.Hindi),
        new("hu", "Hungarian", LinguaLanguage.Hungarian),
        new("id", "Indonesian", LinguaLanguage.Indonesian),
        new("it", "Italian", LinguaLanguage.Italian),
        new("ja", "Japanese", LinguaLanguage.Japanese),
        new("ko", "Korean", LinguaLanguage.Korean),
        new("nl", "Dutch", LinguaLanguage.Dutch),
        new("pl", "Polish", LinguaLanguage.Polish),
        new("pt", "Portuguese", LinguaLanguage.Portuguese),
        new("ro", "Romanian", LinguaLanguage.Romanian),
        new("ru", "Russian", LinguaLanguage.Russian),
        new("sv", "Swedish", LinguaLanguage.Swedish),
        new("th", "Thai", LinguaLanguage.Thai),
        new("tr", "Turkish", LinguaLanguage.Turkish),
        new("uk", "Ukrainian", LinguaLanguage.Ukrainian),
        new("vi", "Vietnamese", LinguaLanguage.Vietnamese),
        new("zh", "Chinese", LinguaLanguage.Chinese),
    ];

    private static readonly Dictionary<string, LanguageInfo> ByCode =
        All.ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<LinguaLanguage, LanguageInfo> ByLingua =
        All.ToDictionary(l => l.Lingua);

    public static LanguageInfo? Find(string? code) =>
        code is not null && ByCode.TryGetValue(Normalize(code), out var l) ? l : null;

    public static LanguageInfo? Find(LinguaLanguage lingua) =>
        ByLingua.TryGetValue(lingua, out var l) ? l : null;

    public static string NameOf(string code) => Find(code)?.Name ?? code.ToUpperInvariant();

    /// <summary>"EN-US", "pt-BR", "zh_Hans" → "en", "pt", "zh".</summary>
    public static string Normalize(string code)
    {
        var s = code.Trim();
        int cut = s.IndexOfAny(['-', '_']);
        return (cut > 0 ? s[..cut] : s).ToLowerInvariant();
    }
}
