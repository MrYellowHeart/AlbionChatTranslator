using AlbionChatTranslator.Core.Translation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AlbionChatTranslator;

/// <summary>
/// Type in your own language, press Enter: the translation is put on the clipboard,
/// ready to paste into Albion with Ctrl+V. The app never types into the game itself.
/// </summary>
public partial class ReplyWindow : Window
{
    private readonly TranslationService _translation;
    private readonly List<LanguageInfo> _targets;

    public ReplyWindow(TranslationService translation, string myLanguage, string? theirLanguage)
    {
        InitializeComponent();
        _translation = translation;

        _targets = Languages.All.Where(l => l.Code != myLanguage).ToList();
        CboTarget.ItemsSource = _targets;
        SetTarget(theirLanguage);
        TxtInfo.Text = "Enter: translate and copy · Shift+Enter: new line · Esc: close";

        TxtInput.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                _ = TranslateAndCopyAsync();
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
        };
        Loaded += (_, _) => TxtInput.Focus();
    }

    /// <summary>Points the window at a language (e.g. whoever spoke last), keeping the typed text.</summary>
    public void SetTarget(string? language)
    {
        CboTarget.SelectedItem = _targets.FirstOrDefault(l => l.Code == language)
                                 ?? CboTarget.SelectedItem
                                 ?? _targets.FirstOrDefault(l => l.Code == Languages.English)
                                 ?? _targets.FirstOrDefault();
    }

    /// <summary>Brings the window forward even when the game has focus (used by the hotkey).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        var hwnd = new WindowInteropHelper(this).Handle;
        uint foreground = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        uint mine = GetCurrentThreadId();
        // Windows only lets the foreground thread hand focus over, so briefly share its input queue.
        if (foreground != mine) AttachThreadInput(foreground, mine, true);
        Activate();
        SetForegroundWindow(hwnd);
        if (foreground != mine) AttachThreadInput(foreground, mine, false);
        TxtInput.Focus();
        TxtInput.SelectAll();
    }

    private void BtnTranslate_Click(object sender, RoutedEventArgs e) => _ = TranslateAndCopyAsync();

    private async Task TranslateAndCopyAsync()
    {
        var text = TxtInput.Text.Trim();
        if (text.Length == 0 || CboTarget.SelectedItem is not LanguageInfo target || !BtnTranslate.IsEnabled) return;

        BtnTranslate.IsEnabled = false;
        TxtOutput.Text = string.Empty;
        TxtInfo.Text = "Translating…";
        try
        {
            var result = await _translation.TranslateReplyAsync(text, target.Code);
            switch (result.Outcome)
            {
                case TranslationOutcome.Translated:
                case TranslationOutcome.SameLanguage:
                    TxtOutput.Text = result.Text ?? text;
                    Copy();
                    TxtInfo.Text = result.Outcome == TranslationOutcome.SameLanguage
                        ? $"That already looks like {target.Name}. Copied as is."
                        : "Copied. Paste it into Albion chat with Ctrl+V. Check names and numbers first.";
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

    private void Copy()
    {
        if (TxtOutput.Text.Length == 0) return;
        try { Clipboard.SetText(TxtOutput.Text); }
        catch (COMException) { TxtInfo.Text = "Could not reach the clipboard; select the text and copy it."; }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e) => Copy();

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
