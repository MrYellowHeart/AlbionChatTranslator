using AlbionChatTranslator.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AlbionChatTranslator;

public partial class SettingsWindow : Window
{
    private record LangItem(string Code, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private static readonly LangItem[] AllLanguages =
    [
        new("AR",      "Arabic"),
        new("BG",      "Bulgarian"),
        new("CS",      "Czech"),
        new("DA",      "Danish"),
        new("DE",      "German"),
        new("EL",      "Greek"),
        new("EN-GB",   "English (British)"),
        new("EN-US",   "English (American)"),
        new("ES",      "Spanish"),
        new("ET",      "Estonian"),
        new("FI",      "Finnish"),
        new("FR",      "French"),
        new("HU",      "Hungarian"),
        new("ID",      "Indonesian"),
        new("IT",      "Italian"),
        new("JA",      "Japanese"),
        new("KO",      "Korean"),
        new("LT",      "Lithuanian"),
        new("LV",      "Latvian"),
        new("NB",      "Norwegian"),
        new("NL",      "Dutch"),
        new("PL",      "Polish"),
        new("PT-BR",   "Portuguese (Brazilian)"),
        new("PT-PT",   "Portuguese (European)"),
        new("RO",      "Romanian"),
        new("RU",      "Russian"),
        new("SK",      "Slovak"),
        new("SL",      "Slovenian"),
        new("SV",      "Swedish"),
        new("TR",      "Turkish"),
        new("UK",      "Ukrainian"),
        new("ZH-HANS", "Chinese (Simplified)"),
        new("ZH-HANT", "Chinese (Traditional)"),
    ];

    private readonly AppSettings _settings;
    private readonly List<(CheckBox Cb, ChatTabSettings Tab)> _tabCheckboxes = new();
    private bool _suppressLangFilter;
    private LangItem? _selectedLang;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        CbBlockGlobals.IsChecked  = settings.BlockGlobalChannels;
        TxtMinLength.Text         = settings.MinTranslationLength.ToString();

        // Populate language dropdown
        var view = new ListCollectionView(AllLanguages);
        CboTargetLang.ItemsSource = view;

        _suppressLangFilter = true;
        _selectedLang = AllLanguages.FirstOrDefault(l =>
            string.Equals(l.Code, settings.TargetLanguage, StringComparison.OrdinalIgnoreCase))
            ?? AllLanguages.First(l => l.Code == "EN-US");
        CboTargetLang.SelectedItem = _selectedLang;
        CboTargetLang.Text         = _selectedLang.DisplayName;
        _suppressLangFilter = false;

        CboTargetLang.SelectionChanged += OnLangSelectionChanged;
        CboTargetLang.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(OnLangTextChanged));

        if (settings.Tabs.Count > 0)
        {
            TxtNoTabs.Visibility = Visibility.Collapsed;
            foreach (var tab in settings.Tabs)
                AddTabRow(tab);
        }
    }

    private void AddTabRow(ChatTabSettings tab)
    {
        var color = TabStyle.GetTabColor(tab.Name);

        var dot = new Ellipse
        {
            Width  = 8,
            Height = 8,
            Fill   = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var label = new TextBlock
        {
            Text       = tab.Name,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)),
            FontFamily = new FontFamily("Consolas"),
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(dot);
        row.Children.Add(label);

        var cb = new CheckBox
        {
            Content   = row,
            IsChecked = tab.Enabled,
            Margin    = new Thickness(0, 2, 0, 2),
            Cursor    = Cursors.Hand,
        };
        _tabCheckboxes.Add((cb, tab));
        TabCheckboxPanel.Children.Add(cb);
    }

    private void OnLangSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboTargetLang.SelectedItem is LangItem li)
        {
            _selectedLang = li;
            // Reset filter so all items appear next time the dropdown opens
            if (CboTargetLang.ItemsSource is ListCollectionView view)
                view.Filter = null;
        }
    }

    private void OnLangTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressLangFilter) return;
        if (CboTargetLang.ItemsSource is not ListCollectionView view) return;

        var query = CboTargetLang.Text;
        // If text exactly matches the selected item's name, don't filter
        if (_selectedLang != null &&
            string.Equals(query, _selectedLang.DisplayName, StringComparison.OrdinalIgnoreCase))
            return;

        view.Filter = o => o is LangItem li &&
            (li.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             li.Code.Contains(query, StringComparison.OrdinalIgnoreCase));
        view.Refresh();

        if (!CboTargetLang.IsDropDownOpen)
            CboTargetLang.IsDropDownOpen = true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.BlockGlobalChannels = CbBlockGlobals.IsChecked == true;

        if (int.TryParse(TxtMinLength.Text, out int minLen) && minLen >= 0)
            _settings.MinTranslationLength = minLen;

        if (_selectedLang != null)
            _settings.TargetLanguage = _selectedLang.Code;

        foreach (var (cb, tab) in _tabCheckboxes)
            tab.Enabled = cb.IsChecked == true;

        _settings.Save();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        _settings.Tabs.Clear();
        _settings.BlockGlobalChannels = true;
        _settings.Save();
        DialogResult = true;
    }
}
