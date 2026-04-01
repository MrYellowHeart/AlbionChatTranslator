using System.Windows.Media;

namespace AlbionChatTranslator;

/// <summary>Shared color palette for tab headers and message text.</summary>
public static class TabStyle
{
    // Fixed colours for well-known channel types
    public static readonly Color SayHeader      = Color.FromRgb(0xF0, 0xE6, 0x8C);
    public static readonly Color WhisperHeader  = Color.FromRgb(0xDD, 0xA0, 0xDD);
    public static readonly Color FriendlyHeader = Color.FromRgb(0x90, 0xEE, 0x90);
    public static readonly Color GlobalHeader   = Color.FromRgb(0xA0, 0xA0, 0xA0);
    public static readonly Color PartyHeader    = Color.FromRgb(0x87, 0xCE, 0xFA); // light blue
    public static readonly Color GuildHeader    = Color.FromRgb(0xAD, 0xFF, 0x2F); // lime green
    public static readonly Color AllianceHeader = Color.FromRgb(0xCC, 0x99, 0xFF); // purple
    public static readonly Color SystemHeader   = Color.FromRgb(0x88, 0x88, 0x88); // dim gray
    public static readonly Color Disabled       = Color.FromRgb(0x55, 0x55, 0x55);

    // Message body colours (slightly brighter/more saturated than headers)
    public static readonly Color SayText      = Color.FromRgb(0xFF, 0xF0, 0x80);
    public static readonly Color WhisperText  = Color.FromRgb(0xDD, 0xAA, 0xFF);
    public static readonly Color FriendlyText = Color.FromRgb(0x90, 0xEE, 0x90);
    public static readonly Color GlobalText   = Color.FromRgb(0xA0, 0xA0, 0xA0);
    public static readonly Color PartyText    = Color.FromRgb(0x87, 0xCE, 0xFA);
    public static readonly Color GuildText    = Color.FromRgb(0xAD, 0xFF, 0x2F);
    public static readonly Color AllianceText = Color.FromRgb(0xCC, 0x99, 0xFF);
    public static readonly Color SystemText   = Color.FromRgb(0x88, 0x88, 0x88); // dim gray, same as meta
    public static readonly Color MetaText     = Color.FromRgb(0x77, 0x77, 0x77);

    // Palette for EventCode-216 dynamic tabs — assigned by hash so the same name
    // always gets the same colour across sessions.
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x6A, 0x9F, 0xB5), // muted blue
        Color.FromRgb(0xE0, 0x90, 0x70), // terracotta
        Color.FromRgb(0x7A, 0xC5, 0x8A), // sage green
        Color.FromRgb(0xD4, 0xB0, 0x50), // amber
        Color.FromRgb(0x70, 0xC0, 0xC0), // teal
        Color.FromRgb(0xC0, 0x88, 0xC0), // mauve
        Color.FromRgb(0xA0, 0xC0, 0x70), // lime
        Color.FromRgb(0x90, 0x90, 0xD0), // periwinkle
        Color.FromRgb(0xD0, 0x90, 0x90), // rose
        Color.FromRgb(0x70, 0xD0, 0xA0), // mint
    ];

    public static Color GetTabColor(string tabName) => tabName switch
    {
        "SAY"      => SayHeader,
        "WHISPER"  => WhisperHeader,
        "Friendly" => FriendlyHeader,
        "Global"   => GlobalHeader,
        _ => Palette[Math.Abs(tabName.GetHashCode()) % Palette.Length]
    };

    public static Color GetMessageColor(string channelType) => channelType switch
    {
        "SAY"      => SayText,
        "WHISPER"  => WhisperText,
        "Global"   => GlobalText,
        "Party"    => PartyText,
        "Guild"    => GuildText,
        "Alliance" => AllianceText,
        "System"   => SystemText,
        _          => FriendlyText,
    };
}
