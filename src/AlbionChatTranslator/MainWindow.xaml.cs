using AlbionChatTranslator.Core.Chat;
using AlbionChatTranslator.Core.Ipc;
using AlbionChatTranslator.Core.Settings;
using AlbionChatTranslator.Core.Translation;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
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
    private readonly GoogleProvider _google = new();
    private readonly OfflineProvider _offline;
    private readonly TranslationService _translation;
    private readonly Updater _updater = new();
    private readonly ObservableCollection<ChatTabViewModel> _tabs = [];
    private GameFocusWatcher? _focus;
    private ITranslationProvider? _keyed;
    private int _lastProgressPercent = -1;
    private CaptureClient? _capture;
    private OverlayWindow? _overlay;
    private GlobalHotkeys? _hotkeys;
    private ReplyWindow? _reply;
    private string? _lastForeignLanguage;
    private int _messageCount;
    private bool _unsortedHintShown;
    private int _sentCount;


    public MainWindow()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _offline = new OfflineProvider(ModelCatalog.LoadEmbedded(), new ModelStore(ModelStore.DefaultRoot));
        _offline.Store.Progress += OnDownloadProgress;
        _offline.DownloadFinished += (pack, error) => Dispatcher.InvokeAsync(() =>
        {
            _lastProgressPercent = -1;
            TxtActivity.Text = error is null ? "Offline language pack ready" : $"Offline language pack failed: {error}";
        });
        var detector = new ChatLanguageDetector();
        _translation = new TranslationService(detector, _google);
        _ = Task.Run(detector.WarmUp); // so the first chat line is not slow
        _translation.Sent += () => Dispatcher.InvokeAsync(() => TxtSent.Text = (++_sentCount).ToString());

        Tabs.ItemsSource = _tabs;
        ApplySettings();
        if (_settings.KeepCacheOnDisk) _translation.LoadCache(AppSettings.CachePath);

        SourceInitialized += (_, _) =>
        {
            RegisterHotkeys();
            _focus = new GameFocusWatcher();
            _focus.ActiveChanged += active =>
            {
                UpdateTopmost();
                _overlay?.SetGameActive(active);
            };
            UpdateTopmost();
        };

        Loaded += async (_, _) =>
        {
            if (_settings.IsFirstRun)
            {
                new WelcomeWindow(_settings, _offline.Catalog, _offline.Store) { Owner = this }.ShowDialog();
                _settings.Save();
                ApplySettings();
            }
            await CheckForUpdatesAsync();
        };
        Closing += async (_, _) =>
        {
            _overlay?.Close();
            _settings.Save();
            if (_settings.KeepCacheOnDisk)
            {
                try { _translation.SaveCache(AppSettings.CachePath); }
                catch (IOException) { }
            }
            _hotkeys?.Dispose();
            _overlayHotkeys?.Dispose();
            _focus?.Dispose();
            if (_capture is not null) await _capture.DisposeAsync();
        };
        Closed += async (_, _) =>
        {
            await _translation.DisposeAsync();
            _google.Dispose();
            _offline.Dispose();
            _keyed?.Dispose();
        };
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    /// <summary>On top only while you're in Albion or this app, unless that option is off.</summary>
    private void UpdateTopmost() =>
        Topmost = _settings.AlwaysOnTop && (!_settings.OnTopOnlyWithGame || _focus is null || _focus.IsActive);

    private void ApplySettings()
    {
        UpdateTopmost();
        DiagnosticLog.Enabled = _settings.DiagnosticLog;
        DiagnosticLog.Write($"settings: target={_settings.TargetLanguage} read=[{string.Join(",", _settings.ReadLanguages)}] key={_settings.KeyService} google={_settings.UseGoogle} offline={_settings.OfflineFallback} globalAuto={_settings.IncludeGlobalChannels} mode={_settings.Mode} never=[{string.Join(",", _settings.NeverSendChannels)}] version={typeof(MainWindow).Assembly.GetName().Version}");
        _translation.TargetLanguage = _settings.TargetLanguage;
        _translation.MinLetters = _settings.MinLetters;
        _translation.ReadLanguages = _settings.ReadLanguages.ToHashSet();
        Glossary.SetCustomTerms(_settings.CustomTerms);

        // Translators are tried in order: your own key, then offline (nothing sent), then Google
        // for languages without an offline model (Google limits its free endpoint quickly).
        _keyed?.Dispose();
        _keyed = _settings.KeyService switch
        {
            KeyService.Microsoft when _settings.MicrosoftKey is { Length: > 0 } k => new MicrosoftProvider(k, _settings.MicrosoftRegion),
            KeyService.DeepL when _settings.DeepLKey is { Length: > 0 } k => new DeepLProvider(k),
            _ => null,
        };
        var chain = new List<ITranslationProvider>();
        if (_keyed is not null) chain.Add(_keyed);
        if (_settings.OfflineFallback) chain.Add(_offline);
        if (_settings.UseGoogle || chain.Count == 0) chain.Add(_google);
        _translation.Provider = new ChainProvider(chain);
        _translation.Offline = _settings.OfflineFallback ? _offline : null;

        TxtProvider.Text = $"{string.Join(" → ", chain.Select(p => p.Name))} · into {Languages.NameOf(_settings.TargetLanguage)}";
        if (_settings.Mode == TranslateMode.OnClick) TxtProvider.Text += " · click to translate";

        if (!_settings.KeepCacheOnDisk) _translation.ClearCacheFile(AppSettings.CachePath);
        StartPrefetch();
        ApplyOverlayEnabled();
        _overlay?.ApplySettings();
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
        client.RoomJoined += (room, channel) => Dispatcher.InvokeAsync(() => OnRoomJoined(room, channel));
        client.RoomRemoved += room => Dispatcher.InvokeAsync(() => { if (_settings.Rooms.Remove(room)) SaveRoomsSoon(); });
        client.Diagnostic += note => DiagnosticLog.Write($"capture: {note}");
        client.StatusChanged += (text, ok) => Dispatcher.InvokeAsync(() =>
        {
            DiagnosticLog.Write($"capture status ok={ok}: {text}");
            if (ok) SetStatus(text, "#4EC94E");
            else { SetStatus("Stopped", "#777777"); TxtError.Text = text; }
        });
        client.Stopped += () => Dispatcher.InvokeAsync(() =>
        {
            DiagnosticLog.Write("capture stopped");
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
        DiagnosticLog.Write("game tabs: " + string.Join("; ", tabs.Select(t => $"{t.Name}=[{string.Join(",", t.ChannelIds)}]")));
        _unsortedHintShown = false;
        _settings.MergeTabs(tabs);
        _settings.Save();
        RebuildTabs();
    }

    private bool _roomsSavePending;

    /// <summary>The game said which channel a chat room belongs to; remember it for next time.</summary>
    private void OnRoomJoined(int room, int channel)
    {
        if (_settings.Rooms.TryGetValue(room, out int old) && old == channel) return;
        _settings.Rooms[room] = channel;
        SaveRoomsSoon();
    }

    private void SaveRoomsSoon()
    {
        if (_roomsSavePending) return;
        _roomsSavePending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _roomsSavePending = false;
            _settings.Save();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnChatLine(ChatLine line)
    {
        // A room the game hasn't assigned this session: use what it said last time.
        bool known = true;
        if (line.Room is int room)
        {
            if (KnownChannels.TryResolveRoom(room, _settings.Rooms, out int channel))
                line = line with { ChannelId = channel, Kind = KnownChannels.KindOf(channel), Room = null };
            else
                known = false;
        }

        // Exactly like the game: a message shows in every tab whose channel list includes its channel.
        // An unassigned room's number is not a channel (room 2 is not Recruit's channel 2), so it
        // is never matched against the tabs' channel lists.
        var targets = _settings.Tabs.Count == 0
            ? _tabs.Where(t => t.Name == FallbackTabName).ToList()
            : !known ? []
            : _settings.Tabs.Where(t => t.Enabled && KnownChannels.TabShows(t.ChannelIds, line.ChannelId))
                .Select(t => _tabs.FirstOrDefault(vm => vm.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase)))
                .OfType<ChatTabViewModel>().ToList();

        bool unsorted = false;
        if (targets.Count == 0)
        {
            if (known)
            {
                // The game doesn't show this channel in any of your tabs, so neither does the app
                // (and nothing is translated or sent).
                DiagnosticLog.Write($"line channel={line.ChannelId} kind={line.Kind} -> hidden (in no shown tab, like the game)");
                return;
            }
            // A room we have never seen assigned: show it in the first tab rather than losing it.
            if (_tabs.FirstOrDefault() is { } first) targets.Add(first);
            else return;
            unsorted = true;
            if (!_unsortedHintShown)
            {
                _unsortedHintShown = true;
                TxtActivity.Text = "Some chat can't be sorted into its tab yet (shown in the first tab). Change character once while capturing; after that the app remembers.";
            }
        }

        var entry = new ChatEntry(line);
        foreach (var tab in targets) tab.Add(entry, _settings.MaxLinesPerTab);
        TxtCount.Text = (++_messageCount).ToString();

        string action;
        if (line.Outgoing && ReplyMemory.Shared.TryRecall(line.Text, out var typed, out var sentIn))
        {
            // A reply the reply box translated: show the exact words you typed, no translator needed.
            entry.Apply(new TranslationResult(TranslationOutcome.Translated, typed, sentIn, By: "you (reply box)"));
            action = "own whisper (showing what was typed in the reply box)";
        }
        else if (_settings.NeverSendChannels.Contains(line.Kind))
        {
            entry.MarkNotSent();
            action = "not sent (channel excluded)";
        }
        else if (_settings.Mode == TranslateMode.OnClick || line.Kind == ChannelKind.Global && !_settings.IncludeGlobalChannels)
        {
            entry.MarkClickToTranslate();
            action = "click to translate";
        }
        else
        {
            Translate(entry);
            action = "translating";
        }
        DiagnosticLog.Write($"line #{_messageCount} channel={line.ChannelId} kind={line.Kind} letters={line.Text.Count(char.IsLetter)} tabs=[{string.Join(", ", targets.Select(t => t.Name))}]{(unsorted ? " (unsorted)" : "")} -> {action}");
    }

    private void Translate(ChatEntry entry, bool userAsked = false)
    {
        entry.MarkPending();
        var started = DateTime.UtcNow;
        _translation.Submit(entry.Line.Text, userAsked: userAsked, callback: result => Dispatcher.InvokeAsync(() =>
        {
            entry.Apply(result);
            DiagnosticLog.Write($"  result channel={entry.Line.ChannelId} outcome={result.Outcome} by={result.By ?? "-"} source={result.SourceLanguage ?? "-"} after={(DateTime.UtcNow - started).TotalMilliseconds:0} ms{(result.Message is { } m ? $" note=\"{m}\"" : "")}");
            if (result.Outcome == TranslationOutcome.Translated)
            {
                TxtActivity.Text = string.Empty;
                _lastForeignLanguage = result.SourceLanguage ?? _lastForeignLanguage;
            }
            else if (result.Outcome is TranslationOutcome.Busy or TranslationOutcome.Failed && result.Message is { } msg)
            {
                TxtActivity.Text = msg;
            }
        }));
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
        if (sender is not Hyperlink { DataContext: ChatEntry entry }) return;
        if (entry.AwaitingClick) Translate(entry, userAsked: true);
        else if (entry.SourceLanguage is { } lang) OpenReply(lang);
    }

    private void BtnReply_Click(object sender, RoutedEventArgs e) => OpenReply(_lastForeignLanguage);

    /// <summary>Opens (or reuses) the reply box, aimed at <paramref name="toLanguage"/>.</summary>
    private void OpenReply(string? toLanguage)
    {
        if (_reply is { IsLoaded: true })
        {
            _reply.SetTarget(toLanguage);
            _reply.BringToFront();
            return;
        }
        _reply = new ReplyWindow(_translation, _settings.TargetLanguage, toLanguage) { Topmost = true };
        _reply.Closed += (_, _) => _reply = null;
        _reply.Show();
        _reply.BringToFront();
    }

    // ── Overlay & hotkeys ────────────────────────────────────────────────────

    private void RegisterHotkeys()
    {
        _hotkeys = new GlobalHotkeys(this);
        var missing = new List<string>();
        if (!_hotkeys.Register(ctrl: true, shift: true, alt: false, 'R', () => OpenReply(_lastForeignLanguage))) missing.Add("Ctrl+Shift+R");
        if (missing.Count > 0)
            TxtError.Text = $"Hotkeys already used by another app: {string.Join(", ", missing)}";
        ApplyOverlayEnabled();
    }

    private GlobalHotkeys? _overlayHotkeys;

    /// <summary>
    /// The overlay is off unless turned on in Settings (see <see cref="AppSettings.OverlayWarning"/>).
    /// While off: no button, no hotkeys (Ctrl+Shift+O/L stay free for other programs), no window.
    /// </summary>
    private void ApplyOverlayEnabled()
    {
        BtnOverlay.Visibility = _settings.OverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (!_settings.OverlayEnabled)
        {
            _overlay?.Close();
            _overlayHotkeys?.Dispose();
            _overlayHotkeys = null;
            return;
        }
        if (_overlayHotkeys is not null || _hotkeys is null) return; // already on, or window not ready yet
        _overlayHotkeys = new GlobalHotkeys(this);
        var missing = new List<string>();
        if (!_overlayHotkeys.Register(ctrl: true, shift: true, alt: false, 'O', ToggleOverlay)) missing.Add("Ctrl+Shift+O");
        if (!_overlayHotkeys.Register(ctrl: true, shift: true, alt: false, 'L', () => _overlay?.ToggleLock())) missing.Add("Ctrl+Shift+L");
        if (missing.Count > 0)
            TxtError.Text = $"Hotkeys already used by another app: {string.Join(", ", missing)}";
    }

    private void BtnOverlay_Click(object sender, RoutedEventArgs e) => ToggleOverlay();

    private void ToggleOverlay()
    {
        if (!_settings.OverlayEnabled) return;
        if (_overlay is { IsVisible: true })
        {
            _overlay.Close();
            return;
        }
        _overlay = new OverlayWindow(_settings, _tabs);
        _overlay.Closed += (_, _) => { _overlay = null; _settings.Save(); };
        _overlay.Show();
        _overlay.SetGameActive(_focus?.IsActive ?? true);
    }

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
        var window = new SettingsWindow(_settings, () => _translation.ClearCache(AppSettings.CachePath), _offline.Store, _offline.Catalog) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _settings.Save();
            ApplySettings();
        }
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e) =>
        new HelpWindow { Owner = this }.ShowDialog();

    private CancellationTokenSource? _prefetch;

    /// <summary>Downloads the offline packs for the languages the user sees in chat, in the background.</summary>
    private void StartPrefetch()
    {
        _prefetch?.Cancel();
        if (!_settings.OfflineFallback || _settings.ChatLanguages.Count == 0) return;
        var packs = _offline.Catalog.PacksFor(_settings.TargetLanguage, _settings.ChatLanguages)
            .Where(p => !_offline.Store.IsInstalled(p)).ToList();
        if (packs.Count == 0) return;

        var cts = _prefetch = new CancellationTokenSource();
        DiagnosticLog.Write($"prefetch: {packs.Count} packs ({packs.Sum(p => p.TotalSize) / 1_000_000} MB): {string.Join(", ", packs.Select(p => p.Id))}");
        _ = Task.Run(async () =>
        {
            try
            {
                int ready = await _offline.PrefetchAsync(packs, cts.Token);
                await Dispatcher.InvokeAsync(() => TxtActivity.Text = ready == packs.Count
                    ? "Offline language packs ready"
                    : $"{packs.Count - ready} offline pack(s) could not be downloaded; they will be retried when needed");
            }
            catch (OperationCanceledException) { }
        });
    }

    private void OnDownloadProgress(string pack, long done, long total)
    {
        int percent = total == 0 ? 100 : (int)(done * 100 / total);
        if (percent == _lastProgressPercent) return;
        _lastProgressPercent = percent;
        Dispatcher.InvokeAsync(() => TxtActivity.Text = $"Downloading offline backup pack ({pack}): {percent}% of {total / 1_000_000} MB");
    }

    // ── Updates ──────────────────────────────────────────────────────────────

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
