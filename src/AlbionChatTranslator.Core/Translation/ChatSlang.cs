using System.Text.RegularExpressions;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>
/// Turns English gamer shorthand into plain English before it is translated, so
/// "lf healer for hg pls" reaches the translator as "looking for healer for HG please"
/// instead of being guessed at ("lf" -> "Se"). Only used on text that is English,
/// because several of these are real words elsewhere ("np", "ty" in Polish).
/// </summary>
public static partial class ChatSlang
{
    private static readonly Dictionary<string, string> Expansions = new(StringComparer.OrdinalIgnoreCase)
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

    [GeneratedRegex(@"(?<![\p{L}\p{N}'])[\p{L}']+(?![\p{L}\p{N}'])")]
    private static partial Regex WordRegex();

    /// <summary>Expands shorthand in English text. Leaves everything else untouched.</summary>
    public static string Expand(string englishText) =>
        WordRegex().Replace(englishText, m => Expansions.TryGetValue(m.Value, out var full) ? full : m.Value);
}
