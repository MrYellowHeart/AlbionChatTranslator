using System.Text.RegularExpressions;

namespace AlbionChatTranslator.Translation;

/// <summary>
/// Albion Online proper nouns that should never be translated.
/// Sorted longest-first so multi-word phrases match before their component words.
/// </summary>
public static class AlbionGlossary
{
    private static readonly string[] Terms =
    [
        // ── Cities & royal cities ────────────────────────────────────────────────
        "Fort Sterling",
        "Black Market",
        "Caerleon",
        "Bridgewatch",
        "Lymhurst",
        "Martlock",
        "Thetford",
        "Brecilien",

        // ── City abbreviations ───────────────────────────────────────────────────
        "Breccy",
        "Brec",
        "Thet",
        "Lym",
        "BW",
        "FS",
        "ML",
        "BM",
        "Caer",
        "RoA",
        "HG",
        "CD",

        // ── Roads / Outlands regions ─────────────────────────────────────────────
        "Roads of Avalon",
        "Bleachskull Desert",
        "Everwinter Peak",
        "Frostspring Volcano",
        "Iceburn Firth",
        "Floatshoal Floe",
        "Driftwood Hollow",
        "Deepwood Copse",
        "Timberscar Copse",
        "Timberslope Grove",
        "Westweald Thicket",
        "Sunkenbough Woods",
        "Thirstwater Steppe",
        "Slakesands Mesa",
        "Sunstrand Quicksands",
        "Sunfang Ravine",
        "Sunfang Wasteland",
        "Dry Basin Riverbed",
        "Dryvein Confluence",
        "Springsump Basin",
        "Runnelvein Sink",
        "Meltwater Bog",
        "Scuttle Sink Marsh",
        "River Copse Fount",
        "Razorrock Bank",
        "Razorrock Verge",
        "Gravemound Knoll",
        "Highstone Loch",
        "Munten Fell",
        "Murdergulch Cross",
        "Murdergulch Ravine",
        "Farshore Heath",
        "Willowshade Hills",
        "Willowshade Ice Marsh",
        "White Peak Tundra",
        "Steppe Cross",
        "Highland Cross",
        "Mountain Cross",
        "Forest Cross",
        "Swamp Cross",
        "Arthurs Rest",
        "Merlyns Rest",
        "Morganas Rest",
        "Avalon",

        // ── Factions / enemy types ───────────────────────────────────────────────
        "Morgana",
        "Heretics",
        "Heretic",
        "Keepers",
        "Keeper",

        // ── Game systems & dungeon types ─────────────────────────────────────────
        "Crystal Arena",
        "Crystal League",
        "Hellgate",
        "Corrupted Dungeon",
        "Corrupted",
        "Expedition",
        "Mists",
        "Abyssal",
        "Smugglers Den",

        // ── Common item/slot shorthands used in chat ─────────────────────────────
        "Caerleon Portal",
        "Thetford Portal",
        "Lymhurst Portal",
        "Bridgewatch Portal",
        "Martlock Portal",
    ];

    // Build a single regex that matches any term as a whole word, longest first.
    // (?i) = case-insensitive so "caerleon" and "Caerleon" both match.
    private static readonly Regex _glossaryRegex = new(
        string.Join("|", Terms.OrderByDescending(t => t.Length)
                               .Select(t => $@"(?<![a-zA-Z]){Regex.Escape(t)}(?![a-zA-Z])")),
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Wraps all Albion proper nouns in the text with &lt;x&gt; tags so DeepL
    /// leaves them untouched (requires TagHandling="xml", IgnoreTags=["x"]).
    /// Also escapes raw XML special characters so the payload is valid XML.
    /// Item links (U+FFFF…U+FFFF) are wrapped in &lt;x&gt; tags so DeepL ignores them.
    /// NOTE: U+FFFF cannot be used in .NET regex patterns (reserved as internal sentinel),
    ///       so item-link replacement is done via Split rather than Regex.
    /// </summary>
    public static string Protect(string text)
    {
        // Escape XML special chars first (U+FFFF in item links survives this untouched)
        var escaped = text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        // Split on U+FFFF: even-indexed parts are plain text, odd-indexed are item-link content.
        var parts = escaped.Split('\uFFFF');
        if (parts.Length > 1)
        {
            var sb = new System.Text.StringBuilder(escaped.Length + parts.Length * 7);
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 0) sb.Append(parts[i]);
                else             sb.Append("<x>").Append(parts[i]).Append("</x>");
            }
            escaped = sb.ToString();
        }

        // Wrap matched glossary terms
        return _glossaryRegex.Replace(escaped, m => $"<x>{m.Value}</x>");
    }

    /// <summary>
    /// Returns true if there is translatable text outside &lt;x&gt; tags.
    /// DeepL fails if the entire input is ignored-tag content with nothing to translate.
    /// </summary>
    public static bool HasTranslatableContent(string protected_)
    {
        // Strip all <x>...</x> blocks, XML entities, and whitespace; check what remains.
        var outside = Regex.Replace(protected_, @"<x>[^<]*</x>", "")
                           .Replace("&amp;", "").Replace("&lt;", "").Replace("&gt;", "")
                           .Trim();
        return outside.Length >= 1;
    }

    /// <summary>
    /// Strips Albion item-link delimiters (U+FFFF) for clean display.
    /// U+FFFFcontent U+FFFF → content, lone U+FFFF removed.
    /// </summary>
    public static string CleanForDisplay(string text) =>
        text.Replace("\uFFFF", "");

    /// <summary>
    /// Strips the &lt;x&gt; tags DeepL echoes back and unescapes XML entities.
    /// </summary>
    public static string Restore(string text) =>
        Regex.Replace(text, @"<x>(.*?)</x>", m => m.Groups[1].Value)
             .Replace("&amp;", "&")
             .Replace("&lt;",  "<")
             .Replace("&gt;",  ">");
}
