using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Windows;
using System.Windows.Controls;

namespace AlbionChatTranslator;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ModelStore _store;
    private readonly List<(CheckBox Box, TabSetting Tab)> _tabBoxes = [];

    public SettingsWindow(AppSettings settings, ModelCatalog catalog, ModelStore store)
    {
        InitializeComponent();
        _settings = settings;
        _store = store;

        var targets = catalog.TargetLanguages.ToList();
        CboLanguage.ItemsSource = targets;
        CboLanguage.SelectedItem = targets.FirstOrDefault(l => l.Code == settings.TargetLanguage) ?? targets.First(l => l.Code == Languages.English);

        RbOffline.IsChecked = settings.Provider == ProviderChoice.Offline;
        RbDeepL.IsChecked = settings.Provider == ProviderChoice.DeepL;
        CbAutoDownload.IsChecked = settings.AutoDownloadLanguagePacks;
        TxtDeepLKey.Password = settings.DeepLKey ?? string.Empty;
        CbGlobal.IsChecked = settings.IncludeGlobalChannels;
        CbTopmost.IsChecked = settings.AlwaysOnTop;
        TxtMinLetters.Text = settings.MinLetters.ToString();
        ShowPackSize();

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

    private void ShowPackSize()
    {
        long bytes = _store.InstalledBytes();
        TxtPacks.Text = bytes == 0 ? "No language packs downloaded" : $"Language packs on disk: {bytes / 1_000_000} MB";
    }

    private void BtnDeletePacks_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Delete all downloaded language packs? They will download again when needed.",
                "Delete language packs", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try
        {
            _store.DeleteAll();
        }
        catch (System.IO.IOException)
        {
            MessageBox.Show(this, "Some packs are in use. Restart the app and try again.", "Delete language packs");
        }
        ShowPackSize();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (RbDeepL.IsChecked == true && string.IsNullOrWhiteSpace(TxtDeepLKey.Password))
        {
            MessageBox.Show(this, "Enter a DeepL API key or choose Offline.", "Settings");
            return;
        }

        if (CboLanguage.SelectedItem is LanguageInfo lang) _settings.TargetLanguage = lang.Code;
        _settings.Provider = RbDeepL.IsChecked == true ? ProviderChoice.DeepL : ProviderChoice.Offline;
        _settings.DeepLKey = TxtDeepLKey.Password;
        _settings.AutoDownloadLanguagePacks = CbAutoDownload.IsChecked == true;
        _settings.IncludeGlobalChannels = CbGlobal.IsChecked == true;
        _settings.AlwaysOnTop = CbTopmost.IsChecked == true;
        if (int.TryParse(TxtMinLetters.Text, out int min)) _settings.MinLetters = Math.Clamp(min, 1, 50);
        foreach (var (box, tab) in _tabBoxes) tab.Enabled = box.IsChecked == true;

        DialogResult = true;
    }
}
