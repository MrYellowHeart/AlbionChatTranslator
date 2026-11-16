using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Translation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace AlbionChatTranslator;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>
/// One chat line on screen. The same instance is shared by every tab that shows it,
/// so a translation arriving later updates all of them at once.
/// </summary>
public sealed class ChatEntry : Observable
{
    private string? _translated;
    private string? _sourceLanguage;
    private string _status = "…";

    public ChatEntry(ChatLine line)
    {
        Line = line;
        Original = Glossary.CleanForDisplay(line.Text);
        Brush = Theme.MessageBrush(line.Kind);
    }

    public ChatLine Line { get; }
    public string Time => Line.Timestamp.ToString("HH:mm");
    public string Sender => Line.Sender + ": ";
    public string Original { get; }
    public Brush Brush { get; }

    public string Text => _translated ?? Original;

    /// <summary>"(Spanish)" after a translated line, or a short status while waiting.</summary>
    public string Tag => _translated is not null && _sourceLanguage is not null
        ? $" ({Languages.NameOf(_sourceLanguage)})"
        : _status;

    public bool IsTranslated => _translated is not null;
    public string? SourceLanguage => _sourceLanguage;
    public string? Tooltip => _translated is null ? null : "Original: " + Original;

    public void Apply(TranslationResult result)
    {
        _sourceLanguage = result.SourceLanguage;
        _translated = result.Outcome == TranslationOutcome.Translated ? result.Text : null;
        _status = result.Outcome switch
        {
            TranslationOutcome.Busy => " (translation paused)",
            TranslationOutcome.Failed => " (not translated)",
            _ => string.Empty,
        };
        Raise(nameof(Text));
        Raise(nameof(Tag));
        Raise(nameof(IsTranslated));
        Raise(nameof(Tooltip));
    }
}

public sealed class ChatTabViewModel(string name) : Observable
{
    public string Name { get; } = name;
    public Brush HeaderBrush { get; } = Theme.TabBrush(name);
    public ObservableCollection<ChatEntry> Entries { get; } = [];

    public void Add(ChatEntry entry, int maxLines)
    {
        Entries.Add(entry);
        while (Entries.Count > maxLines)
            Entries.RemoveAt(0);
    }
}

public static class Theme
{
    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static readonly SolidColorBrush Say = Frozen(0xFF, 0xF0, 0x80);
    private static readonly SolidColorBrush Whisper = Frozen(0xDD, 0xAA, 0xFF);
    private static readonly SolidColorBrush Party = Frozen(0x87, 0xCE, 0xFA);
    private static readonly SolidColorBrush Guild = Frozen(0xAD, 0xFF, 0x2F);
    private static readonly SolidColorBrush Alliance = Frozen(0xCC, 0x99, 0xFF);
    private static readonly SolidColorBrush Global = Frozen(0xB0, 0xB0, 0xB0);
    private static readonly SolidColorBrush Other = Frozen(0x90, 0xEE, 0x90);

    private static readonly SolidColorBrush[] TabPalette =
    [
        Frozen(0x6A, 0x9F, 0xB5), Frozen(0xE0, 0x90, 0x70), Frozen(0x7A, 0xC5, 0x8A), Frozen(0xD4, 0xB0, 0x50),
        Frozen(0x70, 0xC0, 0xC0), Frozen(0xC0, 0x88, 0xC0), Frozen(0xA0, 0xC0, 0x70), Frozen(0x90, 0x90, 0xD0),
    ];

    public static Brush MessageBrush(ChannelKind kind) => kind switch
    {
        ChannelKind.Say => Say,
        ChannelKind.Whisper => Whisper,
        ChannelKind.Party => Party,
        ChannelKind.Guild => Guild,
        ChannelKind.Alliance => Alliance,
        ChannelKind.Global => Global,
        _ => Other,
    };

    /// <summary>Stable colour per tab name (string.GetHashCode is randomised per run, so hash by hand).</summary>
    public static Brush TabBrush(string name)
    {
        uint h = 2166136261;
        foreach (char c in name.ToUpperInvariant()) h = (h ^ c) * 16777619;
        return TabPalette[h % TabPalette.Length];
    }
}
