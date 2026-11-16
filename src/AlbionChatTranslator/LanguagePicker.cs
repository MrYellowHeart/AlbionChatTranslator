using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Windows;
using System.Windows.Controls;

namespace AlbionChatTranslator;

/// <summary>
/// The "which server / which languages do you see in chat" picker used by the Welcome screen and
/// Settings. It shows how much the offline packs for the ticked languages would download.
/// Only the Americas server has a preset (English, Spanish, Portuguese, the region's main
/// languages); for Europe and Asia there is no published data, so nothing is pre-ticked.
/// </summary>
internal sealed class LanguagePicker
{
    private readonly ComboBox _server;
    private readonly WrapPanel _panel;
    private readonly TextBlock _summary;
    private readonly TextBlock _serverHint;
    private readonly ModelCatalog _catalog;
    private readonly ModelStore _store;
    private readonly Dictionary<string, CheckBox> _boxes = [];
    private string _reader;
    private bool _loading;

    public LanguagePicker(ComboBox server, TextBlock serverHint, WrapPanel panel, TextBlock summary,
        ModelCatalog catalog, ModelStore store, string reader, GameServer currentServer, IEnumerable<string> selected)
    {
        _server = server;
        _serverHint = serverHint;
        _panel = panel;
        _summary = summary;
        _catalog = catalog;
        _store = store;
        _reader = reader;

        _loading = true;
        foreach (var (label, value) in new[] { ("Americas", GameServer.Americas), ("Europe", GameServer.Europe), ("Asia", GameServer.Asia), ("Not sure / skip", GameServer.Unknown) })
            _server.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        _server.SelectedItem = _server.Items.Cast<ComboBoxItem>().First(i => (GameServer)i.Tag == currentServer);
        _server.SelectionChanged += (_, _) => OnServerChanged();
        Rebuild(selected);
        ShowServerHint();
        _loading = false;
        UpdateSummary();
    }

    public GameServer Server => _server.SelectedItem is ComboBoxItem { Tag: GameServer s } ? s : GameServer.Unknown;

    public List<string> Selected => _boxes.Where(b => b.Value.IsChecked == true).Select(b => b.Key).ToList();

    /// <summary>Languages pre-ticked for a server. Only where the answer is known.</summary>
    public static IReadOnlyList<string> Preset(GameServer server, string reader) => ServerLanguages.Preset(server, reader);

    /// <summary>Call when the reader's own language changes.</summary>
    public void SetReader(string reader)
    {
        var keep = Selected;
        _reader = reader;
        _loading = true;
        Rebuild(keep.Count > 0 ? keep : Preset(Server, reader));
        _loading = false;
        UpdateSummary();
    }

    private void OnServerChanged()
    {
        if (_loading) return;
        _loading = true;
        Rebuild(Preset(Server, _reader));
        _loading = false;
        ShowServerHint();
        UpdateSummary();
    }

    private void ShowServerHint() => _serverHint.Text = Server switch
    {
        GameServer.Americas => "English, Spanish and Portuguese are pre-selected (the region's main languages). Change them to fit what you see.",
        GameServer.Europe or GameServer.Asia => "There is no published data on which languages are most common on this server, so nothing is pre-selected: tick the languages you see in chat.",
        _ => "Tick the languages you see in chat, or skip: packs also download automatically the first time a language appears.",
    };

    private void Rebuild(IEnumerable<string> selected)
    {
        var chosen = selected.Select(Languages.Normalize).ToHashSet();
        _panel.Children.Clear();
        _boxes.Clear();
        foreach (var lang in Languages.All.Where(l => l.Code != Languages.Normalize(_reader)))
        {
            var box = new CheckBox { Content = lang.Name, IsChecked = chosen.Contains(lang.Code), Margin = new Thickness(0, 2, 12, 2), MinWidth = 96 };
            box.Checked += (_, _) => UpdateSummary();
            box.Unchecked += (_, _) => UpdateSummary();
            _boxes[lang.Code] = box;
            _panel.Children.Add(box);
        }
    }

    private void UpdateSummary()
    {
        if (_loading) return;
        var selected = Selected;
        if (selected.Count == 0)
        {
            _summary.Text = "No languages selected: packs download automatically the first time a language appears in chat.";
            return;
        }
        var packs = _catalog.PacksFor(_reader, selected);
        long toDownload = packs.Where(p => !_store.IsInstalled(p)).Sum(p => p.TotalSize);
        var noOfflineReply = selected.Where(l => !_catalog.CanReplyOffline(_reader, l)).Select(Languages.NameOf).ToList();
        _summary.Text = (toDownload == 0
                ? $"All {packs.Count} offline packs for these languages are already on this PC."
                : $"{packs.Count} offline packs ({toDownload / 1_000_000} MB to download once, in the background).")
            + (noOfflineReply.Count > 0 ? $" Replies into {string.Join(", ", noOfflineReply)} need Google or your own key (no offline model for writing in them)." : "");
    }
}
