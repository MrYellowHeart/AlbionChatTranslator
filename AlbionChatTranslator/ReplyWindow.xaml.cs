using AlbionChatTranslator.Translation;
using System.Globalization;
using System.Windows;

namespace AlbionChatTranslator;

public partial class ReplyWindow : Window
{
    private readonly DeepLTranslator _translator;
    private readonly string          _targetLangCode;

    public ReplyWindow(DeepLTranslator translator, string targetLangCode)
    {
        InitializeComponent();

        _translator     = translator;
        _targetLangCode = targetLangCode;

        string langName;
        try   { langName = new CultureInfo(targetLangCode).EnglishName; }
        catch { langName = targetLangCode; }

        TxtLabel.Text = $"Type your reply below — it will be translated to {langName}.";
        Title = $"Reply in {langName}";

        TxtInput.Focus();
    }

    private async void BtnTranslate_Click(object sender, RoutedEventArgs e)
    {
        var input = TxtInput.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        TxtError.Visibility = Visibility.Collapsed;
        TxtOutput.Text = "Translating…";

        var result = await _translator.TranslateToAsync(input, _targetLangCode);

        if (result == null)
        {
            TxtOutput.Text = string.Empty;
            TxtError.Text = _translator.LastError ?? "Translation failed.";
            TxtError.Visibility = Visibility.Visible;
        }
        else
        {
            TxtOutput.Text = result;
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtOutput.Text))
            Clipboard.SetText(TxtOutput.Text);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
