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
