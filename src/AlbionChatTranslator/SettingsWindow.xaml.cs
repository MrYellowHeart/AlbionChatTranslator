using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Windows;
using System.Windows.Controls;

namespace AlbionChatTranslator;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _clearCache;
    private readonly List<(CheckBox Box, TabSetting Tab)> _tabBoxes = [];
    private readonly List<(CheckBox Box, string Code)> _readBoxes = [];
    private readonly (CheckBox Box, ChannelKind Kind)[] _channelBoxes;

    public SettingsWindow(AppSettings settings, Action clearCache)
    {
        InitializeComponent();
        _settings = settings;
        _clearCache = clearCache;

        var targets = Languages.All.ToList();
        CboLanguage.ItemsSource = targets;
        CboLanguage.SelectedItem = targets.FirstOrDefault(l => l.Code == settings.TargetLanguage) ?? targets.First(l => l.Code == Languages.English);

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

        RbGoogle.IsChecked = settings.Provider == ProviderChoice.Google;
        RbDeepL.IsChecked = settings.Provider == ProviderChoice.DeepL;
        TxtDeepLKey.Password = settings.DeepLKey ?? string.Empty;
        CbDeepLBackup.IsChecked = settings.DeepLAsBackup;
        TxtCustomTerms.Text = string.Join(", ", settings.CustomTerms);
        CbKeepCache.IsChecked = settings.KeepCacheOnDisk;
        SliderOpacity.Value = settings.OverlayOpacity;
        CbOverlayTranslatedOnly.IsChecked = settings.OverlayTranslatedOnly;
        CbGlobal.IsChecked = settings.IncludeGlobalChannels;
        CbTopmost.IsChecked = settings.AlwaysOnTop;
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

    private void BtnClearCache_Click(object sender, RoutedEventArgs e)
    {
        _clearCache();
        MessageBox.Show(this, "All remembered translations were deleted.", "Settings");
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (RbDeepL.IsChecked == true && string.IsNullOrWhiteSpace(TxtDeepLKey.Password))
        {
            MessageBox.Show(this, "Enter a DeepL API key or choose Google Translate.", "Settings");
            return;
        }

        if (CboLanguage.SelectedItem is LanguageInfo lang) _settings.TargetLanguage = lang.Code;
        _settings.ReadLanguages = _readBoxes.Where(r => r.Box.IsChecked == true && r.Code != _settings.TargetLanguage).Select(r => r.Code).ToList();
        _settings.Mode = RbOnClick.IsChecked == true ? TranslateMode.OnClick : TranslateMode.Automatic;
        _settings.NeverSendChannels = _channelBoxes.Where(c => c.Box.IsChecked == true).Select(c => c.Kind).ToList();
        _settings.Provider = RbDeepL.IsChecked == true ? ProviderChoice.DeepL : ProviderChoice.Google;
        _settings.DeepLKey = TxtDeepLKey.Password;
        _settings.DeepLAsBackup = CbDeepLBackup.IsChecked == true;
        _settings.CustomTerms = TxtCustomTerms.Text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _settings.KeepCacheOnDisk = CbKeepCache.IsChecked == true;
        _settings.OverlayOpacity = SliderOpacity.Value;
        _settings.OverlayTranslatedOnly = CbOverlayTranslatedOnly.IsChecked == true;
        _settings.IncludeGlobalChannels = CbGlobal.IsChecked == true;
        _settings.AlwaysOnTop = CbTopmost.IsChecked == true;
        if (int.TryParse(TxtMinLetters.Text, out int min)) _settings.MinLetters = Math.Clamp(min, 1, 50);
        foreach (var (box, tab) in _tabBoxes) tab.Enabled = box.IsChecked == true;

        DialogResult = true;
    }
}
