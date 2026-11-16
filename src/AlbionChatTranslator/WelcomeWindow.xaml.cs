using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Windows;

namespace AlbionChatTranslator;

public partial class WelcomeWindow : Window
{
    private readonly AppSettings _settings;

    public WelcomeWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        var targets = Languages.All.ToList();
        CboLanguage.ItemsSource = targets;
        CboLanguage.SelectedItem = targets.FirstOrDefault(l => l.Code == settings.TargetLanguage) ?? targets.First(l => l.Code == Languages.English);
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CboLanguage.SelectedItem is LanguageInfo lang) _settings.TargetLanguage = lang.Code;
        DialogResult = true;
    }
}
