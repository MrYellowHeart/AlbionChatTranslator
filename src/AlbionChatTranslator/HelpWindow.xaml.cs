using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace AlbionChatTranslator;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        TxtVersion.Text = $"Version {version}";
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        // Only ever opens the fixed https link above.
        if (e.Uri.Scheme == Uri.UriSchemeHttps)
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
