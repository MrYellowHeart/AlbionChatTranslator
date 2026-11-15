using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Ipc;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace AlbionChatTranslator;

public partial class MainWindow : Window
{
    private const string FallbackTabName = "All chat";

    private readonly AppSettings _settings;
    private readonly ModelCatalog _catalog;
    private readonly ModelStore _store;
    private readonly OfflineProvider _offline;
    private readonly TranslationService _translation;
    private readonly Updater _updater = new();
    private readonly ObservableCollection<ChatTabViewModel> _tabs = [];
    private ITranslationProvider? _deepL;
    private CaptureClient? _capture;
    private int _messageCount;
    private int _lastProgressPercent = -1;

    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _catalog = ModelCatalog.LoadEmbedded();
        _store = new ModelStore(ModelStore.DefaultRoot);
        _offline = new OfflineProvider(_catalog, _store);
        var detector = new ChatLanguageDetector();
        _translation = new TranslationService(detector, _offline);
        _ = Task.Run(detector.WarmUp); // so the first chat line is not slow

        _store.Progress += OnDownloadProgress;
        _offline.DownloadFinished += (pack, error) => Dispatcher.InvokeAsync(() =>
        {
            _lastProgressPercent = -1;
            TxtActivity.Text = error is null ? $"Language pack {pack} ready" : string.Empty;
            if (error is not null) TxtError.Text = $"Language pack {pack} failed: {error}";
        });

        Tabs.ItemsSource = _tabs;
        ApplySettings();

        Loaded += async (_, _) =>
        {
            if (_settings.IsFirstRun)
            {
                new WelcomeWindow(_settings, _catalog) { Owner = this }.ShowDialog();
                _settings.Save();
                ApplySettings();
            }
            await CheckForUpdatesAsync();
        };
        Closing += async (_, _) =>
        {
            _settings.Save();
            if (_capture is not null) await _capture.DisposeAsync();
        };
        Closed += async (_, _) =>
        {
            await _translation.DisposeAsync();
            _offline.Dispose();
            _deepL?.Dispose();
        };
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    private void ApplySettings()
    {
        Topmost = _settings.AlwaysOnTop;
        _translation.TargetLanguage = _settings.TargetLanguage;
        _translation.MinLetters = _settings.MinLetters;
        _offline.AutoDownload = _settings.AutoDownloadLanguagePacks;

        _deepL?.Dispose();
        _deepL = null;
        if (_settings.Provider == ProviderChoice.DeepL && _settings.DeepLKey is { Length: > 0 } key)
        {
            _deepL = new DeepLProvider(key);
            _translation.Provider = _deepL;
            TxtProvider.Text = $"DeepL → {Languages.NameOf(_settings.TargetLanguage)}";
        }
        else
        {
            _translation.Provider = _offline;
            TxtProvider.Text = $"Offline → {Languages.NameOf(_settings.TargetLanguage)} · nothing leaves your PC";
        }

        RebuildTabs();
    }

    private void RebuildTabs()
    {
        var wanted = _settings.Tabs.Where(t => t.Enabled).Select(t => t.Name).ToList();
        if (_settings.Tabs.Count == 0) wanted.Add(FallbackTabName);

        var selected = (Tabs.SelectedItem as ChatTabViewModel)?.Name;
        var existing = _tabs.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        _tabs.Clear();
        foreach (var name in wanted)
            _tabs.Add(existing.TryGetValue(name, out var keep) ? keep : new ChatTabViewModel(name));

        Tabs.SelectedItem = _tabs.FirstOrDefault(t => t.Name == selected) ?? _tabs.FirstOrDefault();
    }

    // ── Capture ──────────────────────────────────────────────────────────────

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        BtnStart.IsEnabled = false;
        TxtError.Text = string.Empty;
        SetStatus("Waiting for administrator permission…", "#D4B050");

        var client = new CaptureClient();
        client.LineReceived += line => Dispatcher.InvokeAsync(() => OnChatLine(line));
        client.TabsReceived += tabs => Dispatcher.InvokeAsync(() => OnTabsFromGame(tabs));
        client.StatusChanged += (text, ok) => Dispatcher.InvokeAsync(() =>
        {
            if (ok) SetStatus(text, "#4EC94E");
            else { SetStatus("Stopped", "#777777"); TxtError.Text = text; }
        });
        client.Stopped += () => Dispatcher.InvokeAsync(() =>
        {
            if (_capture != client) return;
            _capture = null;
            SetStatus("Stopped", "#777777");
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        });

        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the app executable.");
        if (await client.StartAsync(exe))
        {
            _capture = client;
            SetStatus("Capturing — log in or chat in Albion", "#4EC94E");
            BtnStop.IsEnabled = true;
        }
        else
        {
            await client.DisposeAsync();
            BtnStart.IsEnabled = true;
            if (TxtStatus.Text.StartsWith("Waiting", StringComparison.Ordinal)) SetStatus("Stopped", "#777777");
        }
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        var client = _capture;
        _capture = null;
        if (client is not null) await client.DisposeAsync();
        SetStatus("Stopped", "#777777");
        BtnStart.IsEnabled = true;
        BtnStop.IsEnabled = false;
    }

    private void OnTabsFromGame(IReadOnlyList<ChatTab> tabs)
    {
        _settings.MergeTabs(tabs);
        _settings.Save();
        RebuildTabs();
    }

    private void OnChatLine(ChatLine line)
    {
        if (line.Kind == ChannelKind.Global && !_settings.IncludeGlobalChannels) return;

        var targets = _settings.Tabs.Count == 0
            ? _tabs.Where(t => t.Name == FallbackTabName).ToList()
            : _settings.Tabs.Where(t => t.Enabled && t.ChannelIds.Contains(line.ChannelId))
                .Select(t => _tabs.FirstOrDefault(vm => vm.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
                .OfType<ChatTabViewModel>().ToList();
        if (targets.Count == 0) return;

        var entry = new ChatEntry(line);
        foreach (var tab in targets) tab.Add(entry, _settings.MaxLinesPerTab);
        TxtCount.Text = (++_messageCount).ToString();

        _translation.Submit(line.Text, result => Dispatcher.InvokeAsync(() => entry.Apply(result)));
    }

    // ── Chat list behaviour ──────────────────────────────────────────────────

    /// <summary>Keeps a list pinned to the newest line unless the user has scrolled up to read.</summary>
    private void ChatList_Loaded(object sender, RoutedEventArgs e)
    {
        var list = (ListBox)sender;
        if (list.Tag is true) return;
        list.Tag = true;

        var viewer = FindChild<ScrollViewer>(list);
        if (viewer is null) return;

        ((INotifyCollectionChanged)list.Items).CollectionChanged += (_, args) =>
        {
            bool atBottom = viewer.VerticalOffset >= viewer.ScrollableHeight - 4;
            if (args.Action == NotifyCollectionChangedAction.Reset || atBottom)
                Dispatcher.InvokeAsync(viewer.ScrollToEnd, DispatcherPriority.Background);
        };
        list.DataContextChanged += (_, _) => Dispatcher.InvokeAsync(viewer.ScrollToEnd, DispatcherPriority.Background);
        viewer.ScrollToEnd();
    }

    private void LanguageTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink { DataContext: ChatEntry { SourceLanguage: { } lang } })
            OpenReply(lang);
    }

    private void BtnReply_Click(object sender, RoutedEventArgs e) => OpenReply(null);

    private void OpenReply(string? toLanguage) =>
        new ReplyWindow(_translation, _catalog, _settings.TargetLanguage, toLanguage) { Owner = this, Topmost = Topmost }.Show();

    // ── Toolbar ──────────────────────────────────────────────────────────────

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var tab in _tabs) tab.Entries.Clear();
        _messageCount = 0;
        TxtCount.Text = "0";
        TxtError.Text = string.Empty;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings, _catalog, _store) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _settings.Save();
            ApplySettings();
        }
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e) =>
        new HelpWindow { Owner = this }.ShowDialog();

    // ── Downloads & updates ──────────────────────────────────────────────────

    private void OnDownloadProgress(string pack, long done, long total)
    {
        int percent = total == 0 ? 100 : (int)(done * 100 / total);
        if (percent == _lastProgressPercent) return;
        _lastProgressPercent = percent;
        Dispatcher.InvokeAsync(() => TxtActivity.Text = $"Downloading language pack {pack}: {percent}% of {total / 1_000_000} MB");
    }

    private async Task CheckForUpdatesAsync()
    {
        var version = await _updater.DownloadUpdateIfAvailableAsync();
        if (version is null) return;
        TxtUpdate.Text = $"Version {version} is ready to install.";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private async void BtnApplyUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_capture is not null) await _capture.DisposeAsync();
        _settings.Save();
        _updater.ApplyAndRestart();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetStatus(string text, string color)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } nested) return nested;
        }
        return null;
    }
}
