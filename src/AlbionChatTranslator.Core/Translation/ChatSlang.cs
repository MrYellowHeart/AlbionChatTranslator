using System.Text.RegularExpressions;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Turns chat shorthand into plain words before translation, so the translator sees
/// "looking for healer for HG please" instead of guessing at "lf healer for hg pls"
/// ("lf" -> "Se"). Offline models are quite literal, which makes this matter even more.
///
/// Each list is only applied to text detected as that language, because the same short
/// forms mean different things elsewhere ("np", "ty" are Polish words; "q" is Portuguese
/// and Spanish shorthand for "que").
/// </summary>
public static partial class ChatSlang
{
    private static readonly Dictionary<string, string> English = new(StringComparer.OrdinalIgnoreCase)
    {
        // looking for / trading
        ["lf"] = "looking for", ["lfg"] = "looking for group", ["lfm"] = "looking for more players",
        ["lfp"] = "looking for players", ["lfr"] = "looking for raid",
        ["wtb"] = "want to buy", ["wts"] = "want to sell", ["wtt"] = "want to trade",
        ["pst"] = "please send me a private message", ["pm"] = "private message", ["dm"] = "direct message",
        ["obo"] = "or best offer", ["pc"] = "price check",
        // politeness and reactions
        ["pls"] = "please", ["plz"] = "please", ["thx"] = "thanks", ["tyvm"] = "thank you very much",
        ["np"] = "no problem", ["nvm"] = "never mind", ["gl"] = "good luck", ["hf"] = "have fun",
        ["gg"] = "good game", ["wp"] = "well played", ["ggwp"] = "good game, well played",
        ["gz"] = "congratulations", ["grats"] = "congratulations", ["gratz"] = "congratulations",
        ["lol"] = "haha", ["lmao"] = "haha", ["rofl"] = "haha", ["omg"] = "oh my god", ["wtf"] = "what the hell",
        // status
        ["brb"] = "be right back", ["afk"] = "away from keyboard", ["omw"] = "on my way", ["otw"] = "on the way",
        ["rn"] = "right now", ["asap"] = "as soon as possible", ["atm"] = "at the moment", ["tmr"] = "tomorrow",
        ["tmrw"] = "tomorrow", ["idk"] = "I don't know", ["imo"] = "in my opinion", ["btw"] = "by the way",
        ["ofc"] = "of course", ["ppl"] = "people", ["msg"] = "message", ["ur"] = "your", ["u"] = "you",
        ["r"] = "are", ["im"] = "I'm", ["dont"] = "don't", ["cant"] = "can't", ["wont"] = "won't", ["thats"] = "that's",
        // group play
        ["inv"] = "invite", ["invs"] = "invites", ["pt"] = "group", ["grp"] = "group", ["party"] = "group",
        ["rez"] = "resurrect", ["res"] = "resurrect", ["dg"] = "dungeon", ["dgs"] = "dungeons", ["dung"] = "dungeon",
        ["mob"] = "monster", ["mobs"] = "monsters", ["exp"] = "experience", ["xp"] = "experience",
        ["heals"] = "healing", ["supp"] = "support", ["ip"] = "item power",
    };

    private static readonly Dictionary<string, string> Portuguese = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vc"] = "você", ["vcs"] = "vocês", ["q"] = "que", ["oq"] = "o que", ["pq"] = "porque", ["pqp"] = "nossa",
        ["tb"] = "também", ["tbm"] = "também", ["blz"] = "beleza", ["vlw"] = "valeu", ["vlww"] = "valeu",
        ["mt"] = "muito", ["mto"] = "muito", ["mts"] = "muitos", ["n"] = "não", ["nao"] = "não", ["ñ"] = "não",
        ["cmg"] = "comigo", ["ctg"] = "contigo", ["msm"] = "mesmo", ["agr"] = "agora", ["hj"] = "hoje",
        ["obg"] = "obrigado", ["obgd"] = "obrigado", ["pfv"] = "por favor", ["pf"] = "por favor", ["pfvr"] = "por favor",
        ["td"] = "tudo", ["tds"] = "todos", ["ngm"] = "ninguém", ["qnd"] = "quando", ["qd"] = "quando",
        ["eae"] = "e aí", ["eai"] = "e aí", ["tmj"] = "estamos juntos", ["dnv"] = "de novo", ["sla"] = "sei lá",
        ["pt"] = "grupo", ["party"] = "grupo",
        ["kk"] = "haha", ["kkk"] = "haha", ["rs"] = "haha", ["rsrs"] = "haha",
    };

    private static readonly Dictionary<string, string> Spanish = new(StringComparer.OrdinalIgnoreCase)
    {
        ["q"] = "que", ["k"] = "que", ["xq"] = "porque", ["pq"] = "porque", ["porq"] = "porque",
        ["tb"] = "también", ["tmb"] = "también", ["ntp"] = "no te preocupes", ["dnd"] = "dónde",
        ["grax"] = "gracias", ["grx"] = "gracias", ["xfa"] = "por favor", ["porfa"] = "por favor", ["pf"] = "por favor",
        ["bn"] = "bien", ["salu2"] = "saludos", ["toy"] = "estoy", ["nd"] = "nada", ["ps"] = "pues",
        ["d"] = "de", ["xd"] = "jaja", ["pt"] = "grupo", ["party"] = "grupo",
        ["jaja"] = "jaja", ["jajaja"] = "jaja",
    };

    // Laughter of any length: kkkkkk, jajajaja, hahaha.
    [GeneratedRegex(@"^(?:k{2,}|(?:ja){2,}j?|(?:ha){2,}h?|(?:rs){2,})$", RegexOptions.IgnoreCase)]
    private static partial Regex LaughRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}'])[\p{L}'0-9]+(?![\p{L}\p{N}'])")]
    private static partial Regex WordRegex();

    /// <summary>Shorthand lists by language (only languages with a list are expanded).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ByLanguage { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [Languages.English] = English,
            ["pt"] = Portuguese,
            ["es"] = Spanish,
        };

    /// <summary>Expands English shorthand (kept for callers that only deal with English).</summary>
    public static string Expand(string englishText) => Expand(englishText, Languages.English);

    /// <summary>Expands shorthand of <paramref name="language"/>; other text is left untouched.</summary>
    public static string Expand(string text, string? language)
    {
        var lang = language is null ? Languages.English : Languages.Normalize(language);
        if (!ByLanguage.TryGetValue(lang, out var list)) return text;
        return WordRegex().Replace(text, m =>
        {
            if (list.TryGetValue(m.Value, out var full)) return full;
            if (lang != Languages.English && LaughRegex().IsMatch(m.Value)) return lang == "es" ? "jaja" : "haha";
            return m.Value;
        });
    }

    /// <summary>True if <paramref name="word"/> is shorthand that only makes sense in <paramref name="language"/>.</summary>
    public static bool IsDistinctive(string word, string language) =>
        Distinctive.TryGetValue(language, out var set) && (set.Contains(word) || language == "pt" && LaughRegex().IsMatch(word) && word.StartsWith('k'));

    // Shorthand that points to one language (used to help detection on slangy lines).
    private static readonly Dictionary<string, HashSet<string>> Distinctive = new()
    {
        ["pt"] = new(StringComparer.OrdinalIgnoreCase) { "vc", "vcs", "blz", "vlw", "eae", "eai", "tmj", "oq", "ngm", "cmg", "ctg", "hj", "agr", "obg", "obgd", "pfv", "dnv", "sla", "galera", "bora", "rsrs", "msm", "tbm", "mto", "nao" },
        ["es"] = new(StringComparer.OrdinalIgnoreCase) { "ntp", "xq", "grax", "grx", "porfa", "xfa", "salu2", "jaja", "jajaja", "toy", "tmb", "dnd", "porq" },
    };
}
