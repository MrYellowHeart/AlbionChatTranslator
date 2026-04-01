namespace AlbionChatTranslator.Network;

/// <summary>
/// Converts raw Albion zone ID strings (e.g. "STEPPE_GREEN_MARKETPLACE_CENTERCITY")
/// into human-readable names (e.g. "Bridgewatch Marketplace").
/// </summary>
public static class ZoneNames
{
    // Biome prefix (or city prefix) → display city name
    private static readonly Dictionary<string, string> BiomeToCity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["STEPPE"]          = "Bridgewatch",
        ["FOREST"]          = "Martlock",
        ["SWAMP"]           = "Thetford",
        ["HIGHLAND"]        = "Fort Sterling",
        ["MOUNTAIN"]        = "Caerleon",
        ["CAERLEON"]        = "Caerleon",
        ["LYMHURST"]        = "Lymhurst",
        ["BRIDGEWATCH"]     = "Bridgewatch",
        ["MARTLOCK"]        = "Martlock",
        ["THETFORD"]        = "Thetford",
        ["BRECILIEN"]       = "Brecilien",
        ["AVALON"]          = "Roads of Avalon",
        ["MERLYN"]          = "Roads of Avalon",
        ["EXPEDITION"]      = "Expedition",
        ["ARENA"]           = "Arena",
    };

    // Two-token biome combinations
    private static readonly Dictionary<string, string> TwoTokenCity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FORT_STERLING"] = "Fort Sterling",
    };

    // Tokens to silently discard (tier colours, directional suffixes, etc.)
    private static readonly HashSet<string> StripTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "GREEN", "YELLOW", "BLUE", "RED", "BLACK", "WHITE",
        "CENTERCITY", "CITY_CENTER",
        "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8",
        "NORTH", "SOUTH", "EAST", "WEST",
        "OPEN", "CLOSED",
    };

    // Tokens that map to a friendlier display name
    private static readonly Dictionary<string, string> TypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MARKETPLACE"]  = "Marketplace",
        ["MARKET"]       = "Market",
        ["CITY"]         = "City",
        ["TOWN"]         = "Town",
        ["DUNGEON"]      = "Dungeon",
        ["ROAD"]         = "Road",
        ["OUTLAND"]      = "Outlands",
        ["PORTAL"]       = "Portal",
        ["BANK"]         = "Bank",
        ["CRAFTING"]     = "Crafting Quarter",
        ["ISLAND"]       = "Island",
        ["GUILD"]        = "Guild Island",
        ["HOME"]         = "Home",
        ["SOLO"]         = "Solo",
        ["GROUP"]        = "Group",
        ["CORRUPTED"]    = "Corrupted Dungeon",
        ["HELLGATE"]     = "Hellgate",
        ["ARENA"]        = "Arena",
        ["CRYSTAL"]      = "Crystal",
        ["HIDEOUT"]      = "Hideout",
        ["HARBOR"]       = "Harbor",
        ["RESPAWN"]      = "Respawn",
        ["ENTRY"]        = "Entry",
        ["EXIT"]         = "Exit",
        ["ROAD"]         = "Road",
    };

    /// <summary>
    /// Returns only the city name derived from the biome/city prefix of a zone ID,
    /// e.g. "STEPPE_GREEN_FACTION_TRADER" → "Bridgewatch".
    /// Returns null if the zone ID has no recognised city prefix.
    /// Used for cross-zone travel where only a city-level entry message is needed.
    /// </summary>
    public static string? ExtractCity(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId)) return null;

        var parts = zoneId.Split('_');

        if (parts.Length >= 2)
        {
            var twoToken = $"{parts[0]}_{parts[1]}";
            if (TwoTokenCity.TryGetValue(twoToken, out var twoCity))
                return twoCity;
        }

        if (parts.Length >= 1 && BiomeToCity.TryGetValue(parts[0], out var oneCity))
            return oneCity;

        return null;
    }

    public static string Format(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            return zoneId;

        var parts = zoneId.Split('_');
        string? city = null;
        int startIdx = 0;

        // Check for two-token city prefix (FORT_STERLING)
        if (parts.Length >= 2)
        {
            var twoToken = $"{parts[0]}_{parts[1]}";
            if (TwoTokenCity.TryGetValue(twoToken, out var twoCity))
            {
                city = twoCity;
                startIdx = 2;
            }
        }

        // Single-token city/biome prefix
        if (city == null && parts.Length >= 1 && BiomeToCity.TryGetValue(parts[0], out var oneCity))
        {
            city = oneCity;
            startIdx = 1;
        }

        // Collect remaining meaningful tokens
        var tokens = new List<string>();
        for (int i = startIdx; i < parts.Length; i++)
        {
            var p = parts[i];
            if (StripTokens.Contains(p)) continue;
            tokens.Add(TypeNames.TryGetValue(p, out var friendly) ? friendly : ToTitleCase(p));
        }

        if (city != null && tokens.Count > 0)
            return $"{city} {string.Join(" ", tokens)}";
        if (city != null)
            return city;
        if (tokens.Count > 0)
            return string.Join(" ", tokens);

        // Absolute fallback: title-case the raw ID
        return ToTitleCase(zoneId.Replace('_', ' '));
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.ToLowerInvariant();
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
