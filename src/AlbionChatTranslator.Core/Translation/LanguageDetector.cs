using Lingua;
using System.Text.RegularExpressions;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Local, offline language identification (Lingua) tuned for game chat: short lines,
/// English abbreviations ("lf healer", "gg wp", "wtb") and misspellings.
/// </summary>
public sealed partial class ChatLanguageDetector
{
    /// <summary>Below this top score we would rather show the original than guess.</summary>
    public const double MinConfidence = 0.25;

    /// <summary>If the reader's language scores at least this share of the winner, assume it is theirs.</summary>
    public const double ReaderBias = 0.6;

    // English that statistical detectors mistake for Danish, Indonesian, Estonian... on short lines.
    // Only words that are not also everyday words in the other supported languages
    // (so no "a", "in", "to", "on", "no", "me", "is", "die", "min", "solo").
    private static readonly HashSet<string> EnglishChat = new(StringComparer.OrdinalIgnoreCase)
    {
        // chat shorthand
        "lf", "lfg", "lfm", "lfp", "wtb", "wts", "wtt", "pst", "pm", "dm", "gg", "wp", "ggwp", "ty", "thx", "tyvm", "np",
        "brb", "afk", "omw", "idk", "imo", "lol", "lmao", "rofl", "pls", "plz", "ppl", "ok", "okay", "kk", "nvm", "rn",
        "u", "ur", "r", "im", "dont", "cant", "wont", "didnt", "isnt", "thats", "whats", "ill", "ive", "youre",
        // game words
        "tank", "healer", "heal", "heals", "dps", "rdps", "mdps", "supp", "support", "zerg", "gank", "ganker", "gankers",
        "loot", "inv", "invite", "party", "guild", "mount", "silver", "fame", "dungeon", "dungeons", "group", "gear",
        "bag", "bags", "market", "price", "cheap", "lag", "laggy", "lagging", "min", "mins", "sec", "secs", "sell", "selling", "buy", "buying", "trade", "trading", "craft",
        "crafting", "gathering", "farm", "farming", "join", "run", "runs", "spot", "spots", "recruiting",
        // everyday English function words
        "the", "and", "for", "you", "your", "with", "anyone", "anybody", "someone", "need", "needs", "want", "wants",
        "thanks", "please", "who", "what", "where", "when", "why", "how", "are", "now", "there", "this", "that",
        "have", "has", "just", "can", "will", "would", "should", "could", "some", "any", "all", "get", "got",
        "going", "come", "coming", "good", "bad", "nice", "really", "very", "much", "many", "more", "here", "today",
        "tonight", "tomorrow", "yes", "yeah", "yep", "nope", "of", "at", "it", "its", "we", "they", "he", "she",
        "our", "their", "them", "from", "about", "out", "up", "if", "or", "but", "not", "be", "been", "was", "were",
        "do", "does", "did", "know", "think", "looking", "help", "hello", "hi", "hey", "guys", "everyone",
    };

    // Languages rarely seen in Albion chat that low-accuracy detection over-guesses on short
    // lines ("chi viene" -> Finnish). They still win when the evidence is clear.
    private static readonly Dictionary<string, double> Prior = new()
    {
        ["cs"] = 0.35, ["da"] = 0.35, ["et"] = 0.35, ["fi"] = 0.35, ["hu"] = 0.35,
        ["id"] = 0.35, ["nl"] = 0.35, ["ro"] = 0.35, ["sv"] = 0.35, ["vi"] = 0.35,
    };

    private const int MinForeignLetters = 6;

    private readonly LanguageDetector _detector;

    public ChatLanguageDetector(string? modelsDirectory = null)
    {
        // Low-accuracy mode: ~90 MB instead of ~900 MB, which matters next to a running game.
        // Its weaker spots on short lines are covered by the English and reader-language checks.
        var builder = LanguageDetectorBuilder
            .FromLanguages(Languages.All.Select(l => l.Lingua).ToArray())
            .WithLowAccuracyMode();
        if (modelsDirectory is not null)
            builder = builder.WithLanguageModelsDirectory(modelsDirectory);
        _detector = builder.Build();
    }

    /// <summary>Detector scores with the Albion prior applied, best first.</summary>
    private List<(string Code, double Score)> Weighted(string text) =>
        _detector.ComputeLanguageConfidenceValues(text)
            .Select(kv => Languages.Find(kv.Key)?.Code is { } code ? (Code: code, Score: kv.Value * Prior.GetValueOrDefault(code, 1.0)) : default)
            .Where(x => x.Code is not null)
            .OrderByDescending(x => x.Score)
            .ToList();

    /// <summary>Loads the detection models now (takes a few seconds) instead of on the first chat line.</summary>
    public void WarmUp() => _detector.ComputeLanguageConfidenceValues("warm up the language models");

    [GeneratedRegex(@"\p{L}+")]
    private static partial Regex WordRegex();

    /// <summary>
    /// ISO 639-1 code of the text's language, or null when unsure.
    /// <paramref name="readerLanguage"/> wins close calls so the reader's own language is not "translated".
    /// </summary>
    public string? Detect(string text, string? readerLanguage = null) => DetectCandidates(text, readerLanguage).FirstOrDefault();

    /// <summary>
    /// Best guess first, followed by languages that scored almost as well (close calls such as
    /// Spanish vs Portuguese).
    /// Empty when unsure. If the reader's own language is a close call, it is the only result.
    /// </summary>
    public IReadOnlyList<string> DetectCandidates(string text, string? readerLanguage = null)
    {
        var words = WordRegex().Matches(text).Select(m => m.Value).ToList();
        if (words.Count == 0) return [];

        // Mostly English chat shorthand: call it English regardless of what the n-grams say.
        int slang = words.Count(w => EnglishChat.Contains(w));
        bool nonLatin = words.Any(w => w.Any(c => c > 'ɏ'));
        if (slang * 2 > words.Count && !nonLatin)
            return [Languages.English];

        // Plain English sentences: let the whole line speak before any words are stripped.
        if (!nonLatin && slang > 0)
        {
            var full = Weighted(text);
            if (full.Count > 0 && full.Any(x => x.Code == Languages.English && x.Score >= full[0].Score * ReaderBias))
                return [Languages.English];
        }

        // Judge the language on what is left after English loanwords ("kto idzie na dungeon").
        var foreign = words.Where(w => !EnglishChat.Contains(w)).ToList();
        if (foreign.Sum(w => w.Length) < MinForeignLetters && !nonLatin)
            return slang > 0 ? [Languages.English] : [];

        var weighted = Weighted(string.Join(' ', foreign));
        if (weighted.Count == 0) return [];

        double total = weighted.Sum(x => x.Score);
        double top = total > 0 ? weighted[0].Score / total : 0;
        if (top < MinConfidence) return [];

        var close = weighted.TakeWhile(x => x.Score >= weighted[0].Score * ReaderBias).Select(x => x.Code).ToList();

        var reader = Languages.Find(readerLanguage)?.Code;
        if (reader is not null && close.Contains(reader)) return [reader];
        return close;
    }
}
