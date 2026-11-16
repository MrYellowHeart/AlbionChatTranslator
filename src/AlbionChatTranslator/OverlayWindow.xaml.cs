using AlbionChatTranslator.Core.Settings;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace AlbionChatTranslator;

/// <summary>
/// Compact see-through chat feed that sits on top of the game. While locked, mouse clicks
/// pass straight through to Albion; unlock it (Ctrl+Shift+L) to move or resize it.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    private readonly AppSettings _settings;
    private readonly ListCollectionView _view;

    public OverlayWindow(AppSettings settings, ObservableCollection<ChatEntry> feed)
    {
        InitializeComponent();
        _settings = settings;

        _view = new ListCollectionView(feed) { IsLiveFiltering = true };
        _view.LiveFilteringProperties.Add(nameof(ChatEntry.IsTranslated));
        Feed.ItemsSource = _view;
        ((INotifyCollectionChanged)Feed.Items).CollectionChanged += (_, _) =>
            Dispatcher.InvokeAsync(ScrollToEnd, System.Windows.Threading.DispatcherPriority.Background);

        Width = settings.OverlayWidth;
        Height = settings.OverlayHeight;
        if (double.IsNaN(settings.OverlayLeft) || double.IsNaN(settings.OverlayTop))
        {
            Left = SystemParameters.WorkArea.Left + 24;
            Top = SystemParameters.WorkArea.Bottom - Height - 260;
        }
        else
        {
            Left = settings.OverlayLeft;
            Top = settings.OverlayTop;
        }

        SourceInitialized += (_, _) => ApplySettings();
        MouseLeftButtonDown += (_, e) =>
        {
            if (!_settings.OverlayClickThrough && e.ButtonState == MouseButtonState.Pressed) DragMove();
        };
        Closing += (_, _) =>
        {
            RememberPlacement();
            // Always come back locked: an unlocked overlay would swallow clicks meant for the game.
            _settings.OverlayClickThrough = true;
        };
    }

    public void ApplySettings()
    {
        BackgroundBrush.Opacity = Math.Clamp(_settings.OverlayOpacity, 0.0, 1.0);
        _view.Filter = _settings.OverlayTranslatedOnly ? o => o is ChatEntry { IsTranslated: true } : null;
        SetClickThrough(_settings.OverlayClickThrough);
        ScrollToEnd();
    }

    public void ToggleLock()
    {
        _settings.OverlayClickThrough = !_settings.OverlayClickThrough;
        SetClickThrough(_settings.OverlayClickThrough);
        if (_settings.OverlayClickThrough) RememberPlacement();
    }

    private void SetClickThrough(bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int style = GetWindowLong(hwnd, GwlExStyle) | WsExLayered | WsExToolWindow | WsExNoActivate;
        style = clickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, style);

        Frame.BorderBrush = clickThrough ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(0x6A, 0x9F, 0xB5));
        TxtUnlocked.Visibility = clickThrough ? Visibility.Collapsed : Visibility.Visible;
        ResizeMode = clickThrough ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
    }

    private void RememberPlacement()
    {
        _settings.OverlayLeft = Left;
        _settings.OverlayTop = Top;
        _settings.OverlayWidth = ActualWidth > 0 ? ActualWidth : Width;
        _settings.OverlayHeight = ActualHeight > 0 ? ActualHeight : Height;
    }

    private void ScrollToEnd()
    {
        if (Feed.Items.Count > 0) Feed.ScrollIntoView(Feed.Items[^1]);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
