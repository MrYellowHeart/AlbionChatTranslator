using AlbionChatTranslator.Core.Translation;
using System.Windows;
using System.Windows.Input;

namespace AlbionChatTranslator;

public partial class ReplyWindow : Window
{
    private readonly TranslationService _translation;

    public ReplyWindow(TranslationService translation, ModelCatalog catalog, string myLanguage, string? theirLanguage)
    {
        InitializeComponent();
        _translation = translation;

        // Only languages we can actually translate into (English pivot included).
        var targets = Languages.All.Where(l => catalog.Route(myLanguage, l.Code) is not null && l.Code != myLanguage).ToList();
        if (_translation.Provider is DeepLProvider) targets = Languages.All.Where(l => l.Code != myLanguage).ToList();
        CboTarget.ItemsSource = targets;
        CboTarget.SelectedItem = targets.FirstOrDefault(l => l.Code == theirLanguage)
                                 ?? targets.FirstOrDefault(l => l.Code == Languages.English)
                                 ?? targets.FirstOrDefault();

        if (theirLanguage is not null && targets.All(l => l.Code != theirLanguage))
            TxtInfo.Text = $"There is no offline model for writing in {Languages.NameOf(theirLanguage)} yet. Pick another language or use DeepL in Settings.";

        TxtInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnTranslate_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        };
        Loaded += (_, _) => TxtInput.Focus();
    }

    private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtInput.Text.Trim();
        if (text.Length == 0 || CboTarget.SelectedItem is not LanguageInfo target) return;

        BtnTranslate.IsEnabled = false;
        TxtOutput.Text = string.Empty;
        TxtInfo.Text = "Translating… (the first time for a language also downloads its pack)";
        try
        {
            var result = await _translation.TranslateReplyAsync(text, target.Code);
            switch (result.Outcome)
            {
                case TranslationOutcome.Translated:
                    TxtOutput.Text = result.Text;
                    TxtInfo.Text = "Check names and numbers before sending. Copy, then paste into Albion chat.";
                    break;
                case TranslationOutcome.SameLanguage:
                    TxtOutput.Text = text;
                    TxtInfo.Text = $"That already looks like {target.Name}.";
                    break;
                default:
                    TxtInfo.Text = result.Message ?? "Translation failed.";
                    break;
            }
        }
        finally
        {
            BtnTranslate.IsEnabled = true;
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (TxtOutput.Text.Length > 0) Clipboard.SetText(TxtOutput.Text);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
