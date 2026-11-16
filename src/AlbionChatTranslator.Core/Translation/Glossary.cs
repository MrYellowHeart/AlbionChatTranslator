using System.Text;
using System.Text.RegularExpressions;

namespace AlbionChatTranslator.Core.Translation;

/// <summary>Text with Albion terms swapped for placeholders, plus what to put back.</summary>
public sealed record ProtectedText(string Text, IReadOnlyList<string> Originals);

/// <summary>
/// Keeps Albion proper nouns, abbreviations, tiers and item links out of the translator's
/// hands: they are swapped for placeholders before translation and restored afterwards.
/// </summary>
public static partial class Glossary
{
    /// <summary>Albion item links are wrapped in U+FFFF in chat text.</summary>
    public const char ItemLinkDelimiter = '￿';

    private static readonly string[] Terms =
    [
        // Cities and their chat abbreviations
        "Fort Sterling", "Black Market", "Caerleon", "Bridgewatch", "Lymhurst", "Martlock", "Thetford", "Brecilien",
        "Breccy", "Brec", "Thet", "Lym", "BW", "FS", "ML", "BM", "Caer", "RoA", "HG", "CD",
        // Outlands / Roads
        "Roads of Avalon", "Bleachskull Desert", "Everwinter Peak", "Frostspring Volcano", "Iceburn Firth",
        "Floatshoal Floe", "Driftwood Hollow", "Deepwood Copse", "Timberscar Copse", "Timberslope Grove",
        "Westweald Thicket", "Sunkenbough Woods", "Thirstwater Steppe", "Slakesands Mesa", "Sunstrand Quicksands",
        "Sunfang Ravine", "Sunfang Wasteland", "Dry Basin Riverbed", "Dryvein Confluence", "Springsump Basin",
        "Runnelvein Sink", "Meltwater Bog", "Scuttle Sink Marsh", "River Copse Fount", "Razorrock Bank",
        "Razorrock Verge", "Gravemound Knoll", "Highstone Loch", "Munten Fell", "Murdergulch Cross",
        "Murdergulch Ravine", "Farshore Heath", "Willowshade Hills", "Willowshade Ice Marsh", "White Peak Tundra",
        "Steppe Cross", "Highland Cross", "Mountain Cross", "Forest Cross", "Swamp Cross",
        "Arthurs Rest", "Merlyns Rest", "Morganas Rest", "Avalon",
        // Factions and game systems
        "Morgana", "Heretics", "Heretic", "Keepers", "Keeper", "Undead",
        "Crystal Arena", "Crystal League", "Hellgate", "Corrupted Dungeon", "Corrupted", "Expedition",
        "Mists", "Abyssal", "Smugglers Den", "ZvZ", "HCE", "GvG", "PvP", "PvE", "KotC",
        "Caerleon Portal", "Thetford Portal", "Lymhurst Portal", "Bridgewatch Portal", "Martlock Portal",
        // Gaming loanwords players use untranslated in every language (models mangle them: "gankers" -> "bargainers")
        "gankers", "ganker", "ganking", "ganked", "gank", "zergs", "zerg", "dps", "rdps", "mdps", "aggro",
        "debuff", "buff", "nerf", "kiting", "kite", "KOS", "ZvZ",
    ];

    private static readonly Regex TermRegex = BuildTermRegex(Terms, RegexOptions.Compiled);

    // Words the user added in Settings (guild names, friends' names, local slang...).
    private static volatile Regex? _customRegex;

    /// <summary>Extra words that are never translated, e.g. guild or player names.</summary>
    public static void SetCustomTerms(IEnumerable<string> terms)
    {
        var clean = terms.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _customRegex = clean.Count == 0 ? null : BuildTermRegex(clean, RegexOptions.None);
    }

    private static Regex BuildTermRegex(IEnumerable<string> terms, RegexOptions extra) => new(
        string.Join("|", terms.OrderByDescending(t => t.Length)
            .Select(t => $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(t)}(?![\p{{L}}\p{{N}}])")),
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | extra);

    // Tiers and enchantments: "T8", "t4.1", "8.3", "@2"
    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:[Tt][1-8](?:\.[0-4])?|[1-8]\.[0-4]|@[1-4])(?![\p{L}\p{N}])")]
    private static partial Regex TierRegex();

    // Placeholder format: a bracketed number translators copy through verbatim.
    [GeneratedRegex(@"\[\s*(\d{1,2})\s*\]")]
    private static partial Regex PlaceholderRegex();

    public static string Placeholder(int index) => $"[{index}]";

    public static ProtectedText Protect(string text)
    {
        var originals = new List<string>();
        var sb = new StringBuilder(text.Length);

        // Item links first: everything between a pair of U+FFFF delimiters.
        var parts = text.Split(ItemLinkDelimiter);
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1 && i < parts.Length - 1)
            {
                sb.Append(Placeholder(originals.Count));
                originals.Add(parts[i]);
            }
            else
            {
                sb.Append(parts[i]);
            }
        }

        string masked = sb.ToString();
        if (_customRegex is { } custom) masked = custom.Replace(masked, m => Keep(m.Value));
        masked = TermRegex.Replace(masked, m => Keep(m.Value));
        masked = TierRegex().Replace(masked, m => Keep(m.Value));
        return new ProtectedText(masked, originals);

        string Keep(string value)
        {
            originals.Add(value);
            return Placeholder(originals.Count - 1);
        }
    }

    /// <summary>
    /// Puts the originals back. Returns null when the translator dropped or duplicated a
    /// placeholder; callers should then fall back rather than show a mangled line.
    /// </summary>
    public static string? Restore(string translated, ProtectedText source)
    {
        if (source.Originals.Count == 0) return translated;

        var seen = new int[source.Originals.Count];
        var restored = PlaceholderRegex().Replace(translated, m =>
        {
            int i = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (i >= source.Originals.Count) return m.Value;
            seen[i]++;
            return source.Originals[i];
        });
        return seen.All(c => c == 1) ? restored : null;
    }

    /// <summary>True if anything other than placeholders, digits, punctuation and spaces remains.</summary>
    public static bool HasTranslatableContent(string masked)
    {
        var rest = PlaceholderRegex().Replace(masked, " ");
        return rest.Any(char.IsLetter);
    }

    /// <summary>Strips item-link delimiters for display.</summary>
    public static string CleanForDisplay(string text) => text.Replace(ItemLinkDelimiter.ToString(), string.Empty);
}
