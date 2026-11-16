using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Windows;
using System.Windows.Controls;

namespace AlbionChatTranslator;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly List<(CheckBox Box, TabSetting Tab)> _tabBoxes = [];

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        var targets = Languages.All.ToList();
        CboLanguage.ItemsSource = targets;
        CboLanguage.SelectedItem = targets.FirstOrDefault(l => l.Code == settings.TargetLanguage) ?? targets.First(l => l.Code == Languages.English);

        RbGoogle.IsChecked = settings.Provider == ProviderChoice.Google;
        RbDeepL.IsChecked = settings.Provider == ProviderChoice.DeepL;
        TxtDeepLKey.Password = settings.DeepLKey ?? string.Empty;
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

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (RbDeepL.IsChecked == true && string.IsNullOrWhiteSpace(TxtDeepLKey.Password))
        {
            MessageBox.Show(this, "Enter a DeepL API key or choose Google Translate.", "Settings");
            return;
        }

        if (CboLanguage.SelectedItem is LanguageInfo lang) _settings.TargetLanguage = lang.Code;
        _settings.Provider = RbDeepL.IsChecked == true ? ProviderChoice.DeepL : ProviderChoice.Google;
        _settings.DeepLKey = TxtDeepLKey.Password;
        _settings.IncludeGlobalChannels = CbGlobal.IsChecked == true;
        _settings.AlwaysOnTop = CbTopmost.IsChecked == true;
        if (int.TryParse(TxtMinLetters.Text, out int min)) _settings.MinLetters = Math.Clamp(min, 1, 50);
        foreach (var (box, tab) in _tabBoxes) tab.Enabled = box.IsChecked == true;

        DialogResult = true;
    }
}
