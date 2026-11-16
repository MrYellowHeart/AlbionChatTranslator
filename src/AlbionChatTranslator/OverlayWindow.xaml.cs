using AlbionChatTranslator.Core.Settings;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace AlbionChatTranslator;

/// <summary>
/// Compact see-through chat on top of the game, with the same tabs as the game. While locked,
/// mouse clicks on the messages pass straight through to Albion; the small tab bar above stays
/// clickable without taking focus from the game. Unlock (Ctrl+Shift+L) to move or resize.
/// Both hide while you're in another program.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;
    private const int WsExToolWindow = 0x80;
    private const int WsExNoActivate = 0x08000000;

    private readonly AppSettings _settings;
    private readonly ObservableCollection<ChatTabViewModel> _tabs;
    private readonly Window _tabBar;
    private readonly StackPanel _tabButtons = new() { Orientation = Orientation.Horizontal };
    private readonly Dictionary<ChatTabViewModel, int> _unread = [];
    private readonly List<ChatTabViewModel> _watched = [];
    private ChatTabViewModel? _selected;
    private ListCollectionView? _view;
    private bool _gameActive = true;

    public OverlayWindow(AppSettings settings, ObservableCollection<ChatTabViewModel> tabs)
    {
        InitializeComponent();
        _settings = settings;
        _tabs = tabs;

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

        _tabBar = CreateTabBar();
        _tabs.CollectionChanged += OnTabsChanged;
        WatchTabs();
        Select(_tabs.FirstOrDefault(t => t.Name == settings.OverlayTab) ?? _tabs.FirstOrDefault());

        SourceInitialized += (_, _) => ApplySettings();
        Loaded += (_, _) =>
        {
            _tabBar.Owner = this;
            _tabBar.Show();
            PlaceTabBar();
            UpdateVisibility();
        };
        LocationChanged += (_, _) => PlaceTabBar();
        SizeChanged += (_, _) => PlaceTabBar();
        MouseLeftButtonDown += (_, e) =>
        {
            if (!_settings.OverlayClickThrough && e.ButtonState == MouseButtonState.Pressed) DragMove();
        };
        Closing += (_, _) =>
        {
            RememberPlacement();
            // Always come back locked: an unlocked overlay would swallow clicks meant for the game.
            _settings.OverlayClickThrough = true;
            _tabs.CollectionChanged -= OnTabsChanged;
            foreach (var t in _watched) t.Entries.CollectionChanged -= OnEntriesChanged;
            _tabBar.Close();
        };
    }

    public void ApplySettings()
    {
        BackgroundBrush.Opacity = Math.Clamp(_settings.OverlayOpacity, 0.0, 1.0);
        ApplyFilter();
        SetClickThrough(_settings.OverlayClickThrough);
        ScrollToEnd();
    }

    public void ToggleLock()
    {
        _settings.OverlayClickThrough = !_settings.OverlayClickThrough;
        SetClickThrough(_settings.OverlayClickThrough);
        if (_settings.OverlayClickThrough) RememberPlacement();
    }

    /// <summary>Called when you switch between Albion (or this app) and other programs.</summary>
    public void SetGameActive(bool active)
    {
        _gameActive = active;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool show = _gameActive || !_settings.OnTopOnlyWithGame;
        Visibility = show ? Visibility.Visible : Visibility.Hidden;
        _tabBar.Visibility = show && IsLoaded ? Visibility.Visible : Visibility.Hidden;
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    private void Select(ChatTabViewModel? tab)
    {
        _selected = tab;
        if (tab is not null)
        {
            _unread[tab] = 0;
            _settings.OverlayTab = tab.Name;
        }

        _view = tab is null ? null : new ListCollectionView(tab.Entries) { IsLiveFiltering = true };
        _view?.LiveFilteringProperties.Add(nameof(ChatEntry.IsTranslated));
        ApplyFilter();
        Feed.ItemsSource = _view;
        RebuildTabButtons();
        ScrollToEnd();
    }

    private void ApplyFilter()
    {
        if (_view is not null)
            _view.Filter = _settings.OverlayTranslatedOnly ? o => o is ChatEntry { IsTranslated: true } : null;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchTabs();
        if (_selected is null || !_tabs.Contains(_selected))
            Select(_tabs.FirstOrDefault(t => t.Name == _settings.OverlayTab) ?? _tabs.FirstOrDefault());
        else
            RebuildTabButtons();
    }

    /// <summary>Counts new lines on tabs you aren't looking at.</summary>
    private void WatchTabs()
    {
        foreach (var t in _watched.Except(_tabs).ToList())
        {
            t.Entries.CollectionChanged -= OnEntriesChanged;
            _watched.Remove(t);
            _unread.Remove(t);
        }
        foreach (var t in _tabs.Except(_watched).ToList())
        {
            t.Entries.CollectionChanged += OnEntriesChanged;
            _watched.Add(t);
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        var tab = _watched.FirstOrDefault(t => ReferenceEquals(t.Entries, sender));
        if (tab is null || tab == _selected) return;
        _unread[tab] = _unread.GetValueOrDefault(tab) + (e.NewItems?.Count ?? 1);
        RebuildTabButtons();
    }

    private Window CreateTabBar()
    {
        var bar = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Title = "Chat overlay tabs",
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x10, 0x10, 0x10)),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Padding = new Thickness(2),
                Child = _tabButtons,
            },
        };
        // Clickable, but never takes focus away from the game.
        bar.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(bar).Handle;
            SetWindowLong(hwnd, GwlExStyle, GetWindowLong(hwnd, GwlExStyle) | WsExNoActivate | WsExToolWindow);
        };
        bar.SizeChanged += (_, _) => PlaceTabBar();
        return bar;
    }

    private void RebuildTabButtons()
    {
        _tabButtons.Children.Clear();
        foreach (var tab in _tabs)
        {
            int unread = _unread.GetValueOrDefault(tab);
            var button = new Button
            {
                Content = unread > 0 ? $"{tab.Name}  {Math.Min(unread, 99)}" : tab.Name,
                Foreground = tab == _selected ? Brushes.White : tab.HeaderBrush,
                Background = tab == _selected ? new SolidColorBrush(Color.FromRgb(0x2D, 0x5A, 0x8E)) : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 1, 8, 2),
                Margin = new Thickness(0, 0, 2, 0),
                FontSize = 11,
                FontWeight = unread > 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Focusable = false,
                Cursor = Cursors.Hand,
                ToolTip = unread > 0 ? $"{unread} new" : null,
            };
            button.Click += (_, _) => Select(tab);
            _tabButtons.Children.Add(button);
        }
    }

    private void PlaceTabBar()
    {
        if (!_tabBar.IsLoaded) return;
        _tabBar.Left = Left;
        _tabBar.Top = Top - _tabBar.ActualHeight;
    }

    // ── Click-through ────────────────────────────────────────────────────────

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
