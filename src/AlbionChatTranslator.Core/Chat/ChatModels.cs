namespace AlbionChatTranslator.Core.Chat;

/// <summary>Broad channel category, used for colouring and filtering.</summary>
public enum ChannelKind
{
    Say,
    Whisper,
    Party,
    Guild,
    Alliance,
    Global,
    Other,
}

/// <summary>One chat message as received from the game.</summary>
/// <param name="ChannelId">
/// The chat-settings channel type ID (the IDs that appear in each tab's channel list),
/// so the UI can decide which tabs show the message.
/// </param>
/// <param name="Room">
/// Set only when the game hasn't said (this session) which channel the message's chat room
/// belongs to: the room number, so the app can use what it learned in an earlier session.
/// </param>
public sealed record ChatLine(
    DateTimeOffset Timestamp,
    int ChannelId,
    ChannelKind Kind,
    string Sender,
    string Text,
    int? Room = null);

/// <summary>A chat tab as configured in the game client.</summary>
/// <param name="GameKey">The game's built-in tab key (e.g. "COMBAT") for default tabs; null for tabs the player named.</param>
public sealed record ChatTab(string Name, IReadOnlyList<int> ChannelIds, string? GameKey = null)
{
    /// <summary>The game's own Combat tab: system and combat messages, which this app does not read.</summary>
    public bool IsCombatTab => string.Equals(GameKey, "COMBAT", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Chat-settings channel type IDs. These were identified empirically by the original
/// project and are not part of upstream's event list, so keep them in one place.
/// </summary>
public static class KnownChannels
{
    public const int Say = 31;
    public const int Whisper = 28;
    public const int Party = 26;
    public const int Guild = 24;
    public const int Alliance = 25;

    /// <summary>
    /// The game's fixed chat rooms (room number -> channel), exactly as the game announced them at
    /// login in two separate sessions (Nov 2026): e.g. room 18 = Recruit (2), 19 = Looking for
    /// group (3), 21 = Help (5). Used only until the game announces its own list, which always wins.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> DefaultRooms = new Dictionary<int, int>
    {
        [1] = 0, [2] = 8, [3] = 9, [4] = 10, [5] = 11, [6] = 13, [7] = 14, [8] = 12, [9] = 15, [10] = 16, [11] = 17,
        [12] = 18, [13] = 19, [14] = 20, [15] = 21, [16] = 22, [17] = 1, [18] = 2, [19] = 3, [20] = 4, [21] = 5, [22] = 7,
    };

    /// <summary>Channel for a room the game hasn't assigned this session: last announcement, else the fixed table.</summary>
    public static bool TryResolveRoom(int room, IReadOnlyDictionary<int, int> remembered, out int channel) =>
        remembered.TryGetValue(room, out channel) || DefaultRooms.TryGetValue(room, out channel);

    /// <summary>
    /// Tab entry that stands for the public chat of the player's language. Inferred, not
    /// documented: channels 8-22 are fifteen consecutive rooms, the client joins exactly one of
    /// them at login, no tab lists any of them, and the General tab lists 23, which has no room
    /// of its own.
    /// </summary>
    public const int LanguageChat = 23;

    /// <summary>Whether a tab with these channel entries shows a message from this channel.</summary>
    public static bool TabShows(IReadOnlyCollection<int> tabChannels, int channel) =>
        tabChannels.Contains(channel) || channel is >= 8 and <= 22 && tabChannels.Contains(LanguageChat);

    public static ChannelKind KindOf(int channelId) => channelId switch
    {
        Say => ChannelKind.Say,
        Whisper => ChannelKind.Whisper,
        Party => ChannelKind.Party,
        Guild => ChannelKind.Guild,
        Alliance => ChannelKind.Alliance,
        _ => ChannelKind.Other,
    };
}
