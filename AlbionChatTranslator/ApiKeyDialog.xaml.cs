using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace AlbionChatTranslator;

public partial class ApiKeyDialog : Window
{
    public string ApiKey { get; private set; } = string.Empty;

    public ApiKeyDialog(string existingKey = "")
    {
        InitializeComponent();
        TxtKey.Text = existingKey;
        TxtKey.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var key = TxtKey.Text.Trim();
        if (string.IsNullOrEmpty(key))
        {
            TxtValidationError.Text = "Please paste your API key before clicking OK.";
            TxtValidationError.Visibility = Visibility.Visible;
            return;
        }

        ApiKey = key;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void LinkDeepL_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
