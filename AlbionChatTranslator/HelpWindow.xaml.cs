using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace AlbionChatTranslator;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
