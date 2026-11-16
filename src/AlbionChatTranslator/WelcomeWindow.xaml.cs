using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Windows;

namespace AlbionChatTranslator;

public partial class WelcomeWindow : Window
{
    private readonly AppSettings _settings;
    private readonly LanguagePicker _picker;

    public WelcomeWindow(AppSettings settings, ModelCatalog catalog, ModelStore store)
    {
        InitializeComponent();
        _settings = settings;
        var targets = Languages.All.ToList();
        CboLanguage.ItemsSource = targets;
        CboLanguage.SelectedItem = targets.FirstOrDefault(l => l.Code == settings.TargetLanguage) ?? targets.First(l => l.Code == Languages.English);

        _picker = new LanguagePicker(CboServer, TxtServerHint, LanguagesPanel, TxtPackSummary, catalog, store,
            settings.TargetLanguage, settings.Server, settings.ChatLanguages);
        CboLanguage.SelectionChanged += (_, _) =>
        {
            if (CboLanguage.SelectedItem is LanguageInfo lang) _picker.SetReader(lang.Code);
        };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (CboLanguage.SelectedItem is LanguageInfo lang) _settings.TargetLanguage = lang.Code;
        _settings.Server = _picker.Server;
        _settings.ChatLanguages = _picker.Selected;
        DialogResult = true;
    }
}
