using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace AlbionChatTranslator;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _clearCache;
    private readonly ModelStore _store;
    private readonly LanguagePicker _picker;
    private string _microsoftKey, _deepLKey;
    private KeyService _shownService = KeyService.None;
    private readonly List<(CheckBox Box, TabSetting Tab)> _tabBoxes = [];
    private readonly List<(CheckBox Box, string Code)> _readBoxes = [];
    private readonly (CheckBox Box, ChannelKind Kind)[] _channelBoxes;

    public SettingsWindow(AppSettings settings, Action clearCache, ModelStore store, ModelCatalog catalog)
    {
        InitializeComponent();
        _settings = settings;
        _clearCache = clearCache;
        _store = store;
        _microsoftKey = settings.MicrosoftKey ?? string.Empty;
        _deepLKey = settings.DeepLKey ?? string.Empty;

        var targets = Languages.All.ToList();
        CboLanguage.ItemsSource = targets;
        CboLanguage.SelectedItem = targets.FirstOrDefault(l => l.Code == settings.TargetLanguage) ?? targets.First(l => l.Code == Languages.English);
        _picker = new LanguagePicker(CboServer, TxtServerHint, LanguagesPanel, TxtPackSummary, catalog, store,
            settings.TargetLanguage, settings.Server, settings.ChatLanguages);
        CboLanguage.SelectionChanged += (_, _) =>
        {
            if (CboLanguage.SelectedItem is LanguageInfo l) _picker.SetReader(l.Code);
        };

        foreach (var lang in Languages.All)
        {
            var box = new CheckBox { Content = lang.Name, IsChecked = settings.ReadLanguages.Contains(lang.Code), Margin = new Thickness(0, 2, 12, 2), MinWidth = 96 };
            _readBoxes.Add((box, lang.Code));
            ReadLanguagesPanel.Children.Add(box);
        }

        RbAuto.IsChecked = settings.Mode == TranslateMode.Automatic;
        RbOnClick.IsChecked = settings.Mode == TranslateMode.OnClick;
        _channelBoxes =
        [
            (CbNoWhisper, ChannelKind.Whisper), (CbNoParty, ChannelKind.Party), (CbNoGuild, ChannelKind.Guild),
            (CbNoAlliance, ChannelKind.Alliance), (CbNoSay, ChannelKind.Say), (CbNoOther, ChannelKind.Other),
        ];
        foreach (var (box, kind) in _channelBoxes) box.IsChecked = settings.NeverSendChannels.Contains(kind);

        TxtRegion.Text = settings.MicrosoftRegion ?? string.Empty;
        CboKeyService.SelectedIndex = (int)settings.KeyService;
        ShowKeyFields(settings.KeyService);
        CbUseGoogle.IsChecked = settings.UseGoogle;
        CbOffline.IsChecked = settings.OfflineFallback;
        ShowPackSize();
        TxtCustomTerms.Text = string.Join(", ", settings.CustomTerms);
        CbKeepCache.IsChecked = settings.KeepCacheOnDisk;
        SliderOpacity.Value = settings.OverlayOpacity;
        CbOverlayTranslatedOnly.IsChecked = settings.OverlayTranslatedOnly;
        CbGlobal.IsChecked = settings.IncludeGlobalChannels;
        CbDiagnostics.IsChecked = settings.DiagnosticLog;
        CbTopmost.IsChecked = settings.AlwaysOnTop;
        CbOnTopOnlyWithGame.IsChecked = settings.OnTopOnlyWithGame;
        TxtMinLetters.Text = settings.MinLetters.ToString();

        TxtNoTabs.Visibility = settings.Tabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tab in settings.Tabs)
        {
            var box = new CheckBox
            {
                Content = new TextBlock { Text = tab.Name, Foreground = Theme.TabBrush(tab.Name) },
                IsChecked = tab.Enabled,
            };
            _tabBoxes.Add((box, tab));
            TabPanel.Children.Add(box);
        }
    }

    private KeyService SelectedService => (KeyService)Math.Max(0, CboKeyService.SelectedIndex);

    private void CboKeyService_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || TxtKey is null) return;
        StashKey();
        ShowKeyFields(SelectedService);
    }

    /// <summary>Remembers what was typed for the service currently shown, so switching does not lose it.</summary>
    private void StashKey()
    {
        if (_shownService == KeyService.Microsoft) _microsoftKey = TxtKey.Password;
        else if (_shownService == KeyService.DeepL) _deepLKey = TxtKey.Password;
    }

    private void ShowKeyFields(KeyService service)
    {
        _shownService = service;
        KeyPanel.Visibility = service == KeyService.None ? Visibility.Collapsed : Visibility.Visible;
        RegionPanel.Visibility = service == KeyService.Microsoft ? Visibility.Visible : Visibility.Collapsed;
        TxtKey.Password = service switch { KeyService.Microsoft => _microsoftKey, KeyService.DeepL => _deepLKey, _ => string.Empty };
        LinkKeyHelp.NavigateUri = new Uri(service == KeyService.DeepL
            ? "https://www.deepl.com/en/pro-api"
            : "https://learn.microsoft.com/azure/ai-services/translator/create-translator-resource");
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri.Scheme == Uri.UriSchemeHttps)
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ShowPackSize()
    {
        long bytes = _store.InstalledBytes();
        TxtPacks.Text = bytes == 0 ? "No offline packs downloaded yet" : $"Offline packs on disk: {bytes / 1_000_000} MB";
    }

    private void BtnDeletePacks_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Delete all offline language packs? They download again when needed.",
                "Offline packs", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try { _store.DeleteAll(); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Some packs are in use. Restart the app and try again.", "Offline packs");
        }
        ShowPackSize();
    }

    private void BtnOpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(AppSettings.DefaultDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppSettings.DefaultDirectory}\"") { UseShellExecute = true });
    }

    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        _clearCache();
        MessageBox.Show(this, "All remembered translations were deleted.", "Settings");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        StashKey();
        var service = SelectedService;
        var key = service switch { KeyService.Microsoft => _microsoftKey, KeyService.DeepL => _deepLKey, _ => "x" };
        if (string.IsNullOrWhiteSpace(key))
        {
            MessageBox.Show(this, "Enter the key for the service you chose, or choose None.", "Settings");
            return;
        }
        if (service == KeyService.None && CbUseGoogle.IsChecked != true && CbOffline.IsChecked != true)
        {
            MessageBox.Show(this, "Turn on at least one translator.", "Settings");
            return;
        }

        if (CboLanguage.SelectedItem is LanguageInfo lang) _settings.TargetLanguage = lang.Code;
        _settings.Server = _picker.Server;
        _settings.ChatLanguages = _picker.Selected;
        _settings.ReadLanguages = _readBoxes.Where(r => r.Box.IsChecked == true && r.Code != _settings.TargetLanguage).Select(r => r.Code).ToList();
        _settings.Mode = RbOnClick.IsChecked == true ? TranslateMode.OnClick : TranslateMode.Automatic;
        _settings.NeverSendChannels = _channelBoxes.Where(c => c.Box.IsChecked == true).Select(c => c.Kind).ToList();
        _settings.KeyService = service;
        _settings.MicrosoftKey = _microsoftKey;
        _settings.DeepLKey = _deepLKey;
        _settings.MicrosoftRegion = string.IsNullOrWhiteSpace(TxtRegion.Text) ? null : TxtRegion.Text.Trim();
        _settings.UseGoogle = CbUseGoogle.IsChecked == true;
        _settings.OfflineFallback = CbOffline.IsChecked == true;
        _settings.CustomTerms = TxtCustomTerms.Text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _settings.KeepCacheOnDisk = CbKeepCache.IsChecked == true;
        _settings.OverlayOpacity = SliderOpacity.Value;
        _settings.OverlayTranslatedOnly = CbOverlayTranslatedOnly.IsChecked == true;
        _settings.IncludeGlobalChannels = CbGlobal.IsChecked == true;
        _settings.DiagnosticLog = CbDiagnostics.IsChecked == true;
        _settings.AlwaysOnTop = CbTopmost.IsChecked == true;
        _settings.OnTopOnlyWithGame = CbOnTopOnlyWithGame.IsChecked == true;
        if (int.TryParse(TxtMinLetters.Text, out int min)) _settings.MinLetters = Math.Clamp(min, 1, 50);
        foreach (var (box, tab) in _tabBoxes) tab.Enabled = box.IsChecked == true;

        DialogResult = true;
    }
}
