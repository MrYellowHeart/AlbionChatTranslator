using AlbionChatTranslator.Network;
using AlbionChatTranslator.Settings;
using AlbionChatTranslator.Translation;
using StatisticsAnalysisTool.Network;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace AlbionChatTranslator;

public partial class MainWindow : Window
{
    private SocketsPacketProvider? _provider;
    private ChatHandler?           _handler;
    private ChatSettingsHandler?   _settingsHandler;
    private SystemMessageHandler?  _systemHandler;
    private LootHandler?           _lootHandler;

    private readonly DeepLTranslator _translator = new();
    private readonly AppSettings     _settings;

    private readonly Dictionary<string, (TabItem Item, RichTextBox Rtb)> _tabs
        = new(StringComparer.OrdinalIgnoreCase);

    private int _messageCount;

    private static readonly Color[] Rainbow =
    [
        Color.FromRgb(0xFF, 0x55, 0x55),
        Color.FromRgb(0xFF, 0xA5, 0x00),
        Color.FromRgb(0xFF, 0xFF, 0x55),
        Color.FromRgb(0x55, 0xFF, 0x55),
        Color.FromRgb(0x55, 0xCC, 0xFF),
        Color.FromRgb(0xBB, 0x77, 0xFF),
        Color.FromRgb(0xFF, 0x77, 0xFF),
    ];

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();

        if (!string.IsNullOrEmpty(_settings.ApiKey))
            TxtApiKey.Password = _settings.ApiKey;

        BuildTabUI();

        // Load item index in background — ready well before the user starts capturing
        _ = ItemIndex.LoadAsync();

        bool firstRun = !File.Exists(AppSettings.FilePath);
        if (firstRun)
            Loaded += (_, _) => new HelpWindow { Owner = this }.ShowDialog();

        Closed += (_, _) =>
        {
            _provider?.Stop();
            _translator.Dispose();
        };
    }

    // ── Toolbar ───────────────────────────────────────────────────────────────

    private void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = TxtApiKey.Password.Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            var dialog = new ApiKeyDialog(_settings.ApiKey) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            apiKey = dialog.ApiKey;
            TxtApiKey.Password = apiKey;
        }

        _settings.ApiKey = apiKey;
        _settings.Save();
        _translator.SetApiKey(apiKey);
        _translator.MinLength      = _settings.MinTranslationLength;
        _translator.TargetLanguage = _settings.TargetLanguage;

        TxtError.Text = string.Empty;

        _settingsHandler = new ChatSettingsHandler
        {
#if DEBUG
            DumpPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "chat_server_events.txt")
#endif
        };
        _settingsHandler.TabsReceived += tabs =>
            Dispatcher.InvokeAsync(() => OnTabsReceived(tabs));

        _settingsHandler.GlobalChannelMapped += (byteVal, channelId) =>
        {
            _settings.GlobalChannelMap[byteVal] = channelId;
            _settings.Save();
            if (_handler != null)
                _handler.GlobalChannelMap = _settings.GlobalChannelMap;
        };

        _settingsHandler.DynamicChannelMapped += (dynamicId, ec216Type) =>
        {
            _settings.DynamicChannelMap[dynamicId] = ec216Type;
            _settings.Save();
            if (_handler != null)
                _handler.DynamicChannelMap = _settings.DynamicChannelMap;
        };

        _handler = new ChatHandler
        {
            BlockGlobalChannels = _settings.BlockGlobalChannels,
            GlobalChannelMap    = _settings.GlobalChannelMap,
            DynamicChannelMap   = _settings.DynamicChannelMap,
            ChannelResolver     = ResolveChannel,
#if DEBUG
            DumpPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "ec73_globals.txt")
#endif
        };
        _handler.MessageReceived += OnChatMessage;

        _systemHandler = new SystemMessageHandler
        {
            ChannelResolver = ResolveChannel,
        };
        _systemHandler.MessageReceived += OnChatMessage;

        _lootHandler = new LootHandler
        {
            ChannelResolver = ResolveChannel,
        };
        _lootHandler.MessageReceived += OnChatMessage;

        var builder = ReceiverBuilder.Create();
        builder.AddHandler(_settingsHandler);
        builder.AddHandler(_handler);
        builder.AddHandler(_systemHandler);
        builder.AddHandler(_lootHandler);
        var receiver = builder.Build();

        _provider = new SocketsPacketProvider(receiver);
        _provider.Start();

        if (_provider.IsRunning)
        {
            SetStatus("Capturing...", "#4EC94E");
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled  = true;
        }
        else
        {
            SetStatus("Failed — run as Administrator", "#C05050");
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _provider?.Stop();
        _provider = null;
        SetStatus("Stopped", "#666666");
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled  = false;
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        ClearAllTabs();
        _messageCount = 0;
        TxtCount.Text = "0";
        TxtError.Text = string.Empty;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings) { Owner = this };
        if (win.ShowDialog() != true) return;

        if (_handler != null)
        {
            _handler.BlockGlobalChannels = _settings.BlockGlobalChannels;
            _handler.GlobalChannelMap    = _settings.GlobalChannelMap;
        }
        _translator.MinLength      = _settings.MinTranslationLength;
        _translator.TargetLanguage = _settings.TargetLanguage;

        BuildTabUI();
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow { Owner = this }.ShowDialog();
    }

    private void LinkDeepL_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    // ── Tab UI ────────────────────────────────────────────────────────────────

    private void BuildTabUI()
    {
        // Keep the currently selected tab name so we can restore it after rebuild.
        string? selectedName = (MainTabControl.SelectedItem as TabItem)?.Header is TextBlock tb ? tb.Text : null;

        // Remove tabs that no longer exist in settings, add new ones — but preserve existing RTBs.
        var newNames = _settings.Tabs.Count > 0
            ? _settings.Tabs.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string> { "Awaiting login\u2026" };

        // Visible tabs = only enabled ones (plus placeholder when none exist)
        var visibleNames = _settings.Tabs.Count > 0
            ? _settings.Tabs.Where(t => t.Enabled).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string> { "Awaiting login\u2026" };

        // Drop tabs that are no longer visible
        foreach (var name in _tabs.Keys.Except(visibleNames, StringComparer.OrdinalIgnoreCase).ToList())
        {
            MainTabControl.Items.Remove(_tabs[name].Item);
            _tabs.Remove(name);
        }

        if (_settings.Tabs.Count == 0)
        {
            if (!_tabs.ContainsKey("Awaiting login\u2026"))
                CreateTab("Awaiting login\u2026", Color.FromRgb(0x44, 0x44, 0x44), enabled: true);
        }
        else
        {
            int insertIdx = 0;
            foreach (var tab in _settings.Tabs.Where(t => t.Enabled))
            {
                if (_tabs.TryGetValue(tab.Name, out var existing))
                {
                    // Ensure order matches settings
                    int currentIdx = MainTabControl.Items.IndexOf(existing.Item);
                    if (currentIdx != insertIdx)
                    {
                        MainTabControl.Items.RemoveAt(currentIdx);
                        MainTabControl.Items.Insert(insertIdx, existing.Item);
                    }
                }
                else
                {
                    CreateTabAt(tab.Name, TabStyle.GetTabColor(tab.Name), enabled: true, insertIdx);
                }
                insertIdx++;
            }
        }

        // Restore selection
        if (selectedName != null && _tabs.TryGetValue(selectedName, out var sel))
            MainTabControl.SelectedItem = sel.Item;
        else if (MainTabControl.Items.Count > 0)
            MainTabControl.SelectedIndex = 0;
    }

    private void CreateTab(string name, Color headerColor, bool enabled) =>
        CreateTabAt(name, headerColor, enabled, MainTabControl.Items.Count);

    private void CreateTabAt(string name, Color headerColor, bool enabled, int index)
    {
        var rtb = new RichTextBox
        {
            IsReadOnly      = true,
            Background      = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14)),
            FontFamily      = new FontFamily("Consolas"),
            FontSize        = 12,
            BorderThickness = new Thickness(0),
            Padding         = new Thickness(6),
        };
        rtb.Document.Blocks.Clear();
        ScrollViewer.SetVerticalScrollBarVisibility(rtb,   ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(rtb, ScrollBarVisibility.Disabled);
        rtb.SizeChanged += (_, _) =>
        {
            if (rtb.ActualWidth > 0)
                rtb.Document.PageWidth = rtb.ActualWidth;
        };

        var headerText = new TextBlock
        {
            Text       = name,
            Foreground = new SolidColorBrush(enabled ? headerColor : TabStyle.Disabled),
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 12,
        };

        var tabItem = new TabItem { Header = headerText, Content = rtb };
        MainTabControl.Items.Insert(index, tabItem);
        _tabs[name] = (tabItem, rtb);
    }

    private void ClearAllTabs()
    {
        foreach (var (_, rtb) in _tabs.Values)
            rtb.Document.Blocks.Clear();
    }

    // ── EventCode 216 ─────────────────────────────────────────────────────────

    private void OnTabsReceived(List<ChatTabSettings> newTabs)
    {
        // Preserve enabled/disabled choices from saved settings
        foreach (var newTab in newTabs)
        {
            var saved = _settings.Tabs.FirstOrDefault(
                t => string.Equals(t.Name, newTab.Name, StringComparison.OrdinalIgnoreCase));
            if (saved != null)
                newTab.Enabled = saved.Enabled;
        }

        _settings.Tabs = newTabs;
        _settings.Save();
        BuildTabUI();
    }

    // ── Channel resolver ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns every enabled tab whose ChannelIds list contains the given channel ID.
    /// A single message can legitimately appear in multiple tabs.
    /// </summary>
    private IReadOnlyList<string> ResolveChannel(int channelId)
    {
        if (_settings.Tabs.Count == 0) return [];

        var matching = _settings.Tabs
            .Where(t => t.Enabled && t.ChannelIds.Contains(channelId))
            .Select(t => t.Name)
            .ToList();

        return matching;
    }

    // ── Message display ───────────────────────────────────────────────────────

    private void OnChatMessage(ChatMessage msg)
    {
        _ = Task.Run(async () =>
        {
            // System messages are never translated
            string displayText;
            string langSuffix      = string.Empty;
            string? detectedLang   = null;

            if (msg.Channel == "System" || msg.Channel == "Kill")
            {
                displayText = msg.Text;
            }
            else
            {
                var result = await _translator.TranslateAsync(msg.Text);

                if (result == null)
                {
                    displayText = AlbionGlossary.CleanForDisplay(msg.Text);
                    if (_translator.LastError is string err)
                        await Dispatcher.InvokeAsync(() => TxtError.Text = $"Translation error: {err}");
                }
                else
                {
                    // Check if the detected source matches our target language (base code comparison)
                    var targetBase = _settings.TargetLanguage.Split('-')[0];
                    if (result.Value.LangCode.StartsWith(targetBase, StringComparison.OrdinalIgnoreCase))
                    {
                        displayText = AlbionGlossary.CleanForDisplay(msg.Text);
                    }
                    else
                    {
                        displayText  = result.Value.Text;
                        detectedLang = result.Value.LangCode;
                        langSuffix   = $" ({GetLanguageName(result.Value.LangCode)})";
                    }
                }
            }

            await Dispatcher.InvokeAsync(() =>
            {
                bool delivered = false;
                foreach (var tabName in msg.Tabs)
                {
                    if (!_tabs.TryGetValue(tabName, out var tabData)) continue;

                    AppendToTab(tabData.Rtb, msg, displayText, langSuffix, detectedLang);
                    delivered = true;
                }

                if (delivered)
                {
                    _messageCount++;
                    TxtCount.Text = _messageCount.ToString();
                }
            });
        });
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private void AppendToTab(RichTextBox rtb, ChatMessage msg, string displayText, string langSuffix, string? detectedLangCode = null)
    {
        var metaBrush  = new SolidColorBrush(TabStyle.MetaText);
        var msgColor   = TabStyle.GetMessageColor(msg.Channel);
        var nameBrush  = new SolidColorBrush(msgColor);
        var sender     = Capitalize(msg.Sender);

        bool isTenith  = string.Equals(msg.Sender, "Tenith", StringComparison.OrdinalIgnoreCase);
        bool isSystem  = msg.Channel == "System";
        bool isKill    = msg.Channel == "Kill";

        var para = new Paragraph { Margin = new Thickness(0, 0, 0, 1) };
        para.Inlines.Add(new Run($"[{msg.Timestamp:HH:mm}] ") { Foreground = metaBrush });

        if (isSystem)
        {
            para.Inlines.Add(new Run("[System] ") { Foreground = new SolidColorBrush(TabStyle.SystemText) });
        }
        else if (!isKill)
        {
            if (isTenith)
                AppendRainbow(para, sender + ": ");
            else
                para.Inlines.Add(new Run($"{sender}: ") { Foreground = nameBrush });
        }

        if (isKill)
            AppendKillColored(para, displayText);
        else if (isTenith)
            AppendRainbow(para, displayText);
        else
            para.Inlines.Add(new Run(displayText) { Foreground = new SolidColorBrush(msgColor) });

        if (!string.IsNullOrEmpty(langSuffix))
        {
            if (detectedLangCode != null)
            {
                var link = new Hyperlink(new Run(langSuffix))
                {
                    Foreground           = metaBrush,
                    Cursor               = Cursors.Hand,
                    Tag                  = detectedLangCode,
                    TextDecorations      = null,
                };
                // Click requires Ctrl in a RichTextBox; PreviewMouseLeftButtonDown fires on plain click
                link.PreviewMouseLeftButtonDown += OnLangSuffixClick;
                para.Inlines.Add(link);
            }
            else
            {
                para.Inlines.Add(new Run(langSuffix) { Foreground = metaBrush });
            }
        }

        rtb.Document.Blocks.Add(para);

        // Only auto-scroll if already at (or within 20px of) the bottom
        if (rtb.VerticalOffset + rtb.ViewportHeight >= rtb.ExtentHeight - 20)
            rtb.ScrollToEnd();
    }

    private static void AppendRainbow(Paragraph para, string text)
    {
        for (int i = 0; i < text.Length; i++)
            para.Inlines.Add(new Run(text[i].ToString())
            {
                Foreground = new SolidColorBrush(Rainbow[i % Rainbow.Length])
            });
    }

    private static readonly SolidColorBrush KillWhite = new(Colors.White);
    private static readonly SolidColorBrush KillRed   = new(Color.FromRgb(0xFF, 0x55, 0x55));

    private static void AppendKillColored(Paragraph para, string text)
    {
        const string pivot = " killed ";
        int idx = text.IndexOf(pivot, StringComparison.Ordinal);
        if (idx < 0)
        {
            para.Inlines.Add(new Run(text) { Foreground = KillWhite });
            return;
        }
        para.Inlines.Add(new Run(text[..idx])              { Foreground = KillWhite });
        para.Inlines.Add(new Run(" killed ")               { Foreground = KillRed   });
        para.Inlines.Add(new Run(text[(idx + pivot.Length)..]) { Foreground = KillWhite });
    }

    // ── Reply window ─────────────────────────────────────────────────────────

    private void OnLangSuffixClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled    = true;
        var link     = (Hyperlink)sender;
        var langCode = (string)link.Tag;
        new ReplyWindow(_translator, langCode).Show();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetLanguageName(string langCode)
    {
        try   { return new CultureInfo(langCode).EnglishName; }
        catch { return langCode; }
    }

    private void SetStatus(string text, string hexColour)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(hexColour));
    }
}
