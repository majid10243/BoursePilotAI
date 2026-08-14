using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using BoursePilotAI.Models;
using BoursePilotAI.Services;

namespace BoursePilotAI;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<StockItem> _stocks = new();
    private readonly ObservableCollection<StockItem> _dashboardStocks = new();
    private readonly ObservableCollection<CodalAnnouncement> _announcements = new();
    private readonly ListCollectionView _scannerView;
    private readonly ListCollectionView _searchView;
    private readonly ListCollectionView _codalView;
    private readonly HttpClient _httpClient;
    private readonly LocalDataStore _store;
    private readonly DataSyncService _syncService;
    private readonly DispatcherTimer _autoUpdateTimer = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private AppSettings _settings = new();
    private bool _isSyncing;
    private bool _isLoaded;

    public MainWindow()
    {
        InitializeComponent();

        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = App.TsetmcOptions.Timeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 BoursePilotAI/1.1");

        _store = new LocalDataStore();
        _syncService = new DataSyncService(
            new TsetmcService(_httpClient, App.TsetmcOptions),
            new CodalService(_httpClient),
            _store,
            new StockAnalyzer());

        _scannerView = new ListCollectionView(_stocks) { Filter = ScannerFilter };
        _searchView = new ListCollectionView(_stocks) { Filter = SearchFilter };
        _codalView = new ListCollectionView(_announcements) { Filter = CodalFilter };

        DashboardDataGrid.ItemsSource = _dashboardStocks;
        ScannerDataGrid.ItemsSource = _scannerView;
        SearchDataGrid.ItemsSource = _searchView;
        CodalDataGrid.ItemsSource = _codalView;
        AnalysisSymbolComboBox.ItemsSource = _stocks;

        _autoUpdateTimer.Tick += AutoUpdateTimer_Tick;
        DataDirectoryText.Text = _store.DataDirectory;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
            return;
        _isLoaded = true;

        _settings = await _store.LoadSettingsAsync(_lifetimeCancellation.Token);
        PopulateSettingsControls();
        ConfigureAutoUpdateTimer();

        var cachedStocks = await _store.LoadMarketAsync(_lifetimeCancellation.Token);
        var cachedCodal = await _store.LoadCodalAsync(_lifetimeCancellation.Token);
        if (cachedStocks.Count > 0 || cachedCodal.Count > 0)
        {
            ApplyData(cachedStocks, cachedCodal);
            SyncStageText.Text = "اطلاعات محلی بارگذاری شد";
            SyncDetailText.Text = $"{cachedStocks.Count:N0} نماد و {cachedCodal.Count:N0} اطلاعیه";
            LastUpdatedText.Text = "نمایش آخرین اطلاعات ذخیره‌شده؛ در حال بررسی به‌روزرسانی";
        }

        if (_settings.AutoUpdateEnabled || cachedStocks.Count == 0)
            await RefreshDataAsync();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _autoUpdateTimer.Stop();
        _lifetimeCancellation.Cancel();
        _httpClient.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        => await RefreshDataAsync();

    private async void AutoUpdateTimer_Tick(object? sender, EventArgs e)
        => await RefreshDataAsync();

    private async Task RefreshDataAsync()
    {
        if (_isSyncing || _lifetimeCancellation.IsCancellationRequested)
            return;

        _isSyncing = true;
        RefreshButton.IsEnabled = false;
        RefreshButton.Content = "در حال به‌روزرسانی...";
        var progress = new Progress<SyncProgress>(UpdateProgress);

        try
        {
            var result = await _syncService.SynchronizeAsync(
                _settings,
                progress,
                _lifetimeCancellation.Token);

            ApplyData(result.Stocks, result.Announcements);
            var cacheNotice = result.UsedCachedMarketData || result.UsedCachedCodalData
                ? " — بخشی از داده‌ها از حافظه محلی"
                : "";
            LastUpdatedText.Text = $"آخرین به‌روزرسانی: {result.CompletedAt:yyyy/MM/dd HH:mm}{cacheNotice}";

            if (result.Warnings.Count > 0)
            {
                SyncStageText.Text = "به‌روزرسانی با هشدار پایان یافت";
                SyncDetailText.Text = result.Warnings[0];
            }
        }
        catch (OperationCanceledException)
        {
            SyncStageText.Text = "به‌روزرسانی متوقف شد";
            SyncDetailText.Text = "عملیات به درخواست کاربر یا هنگام خروج از برنامه متوقف شد.";
        }
        catch (Exception ex)
        {
            SyncStageText.Text = "خطا در به‌روزرسانی";
            SyncDetailText.Text = ex.Message;
        }
        finally
        {
            _isSyncing = false;
            RefreshButton.IsEnabled = true;
            RefreshButton.Content = "به‌روزرسانی اکنون";
        }
    }

    private void UpdateProgress(SyncProgress value)
    {
        SyncProgressBar.Value = value.Percent;
        SyncPercentText.Text = $"{value.Percent}%";
        SyncStageText.Text = value.Stage;
        SyncDetailText.Text = value.Detail;
    }

    private void ApplyData(
        IReadOnlyList<StockItem> stocks,
        IReadOnlyList<CodalAnnouncement> announcements)
    {
        _stocks.Clear();
        foreach (var stock in stocks.OrderByDescending(item => item.Value))
            _stocks.Add(stock);

        _dashboardStocks.Clear();
        foreach (var stock in _stocks.Take(30))
            _dashboardStocks.Add(stock);

        _announcements.Clear();
        foreach (var announcement in announcements)
            _announcements.Add(announcement);

        _scannerView.Refresh();
        _searchView.Refresh();
        _codalView.Refresh();
        UpdateDashboardCards();

        if (AnalysisSymbolComboBox.SelectedItem is null && _stocks.Count > 0)
            AnalysisSymbolComboBox.SelectedIndex = 0;
    }

    private void UpdateDashboardCards()
    {
        MarketCountText.Text = _stocks.Count.ToString("N0");
        PositiveCountText.Text = _stocks.Count(item => item.ClosingChangePercent > 0).ToString("N0");
        NegativeCountText.Text = _stocks.Count(item => item.ClosingChangePercent < 0).ToString("N0");

        var totalValue = _stocks.Sum(item => item.Value);
        MarketValueText.Text = totalValue switch
        {
            >= 10_000_000_000_000 => $"{totalValue / 10_000_000_000_000:N1} همت",
            >= 1_000_000_000 => $"{totalValue / 1_000_000_000:N1} میلیارد ریال",
            _ => $"{totalValue:N0} ریال"
        };
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string pageKey })
            ShowPage(pageKey);
    }

    private void ShowPage(string pageKey)
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        AnalysisPage.Visibility = Visibility.Collapsed;
        ScannerPage.Visibility = Visibility.Collapsed;
        SearchPage.Visibility = Visibility.Collapsed;
        CodalPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;

        switch (pageKey)
        {
            case "Analysis":
                AnalysisPage.Visibility = Visibility.Visible;
                PageTitleText.Text = "تحلیل نماد";
                break;
            case "Scanner":
                ScannerPage.Visibility = Visibility.Visible;
                PageTitleText.Text = "اسکنر بازار";
                break;
            case "Search":
                SearchPage.Visibility = Visibility.Visible;
                PageTitleText.Text = "جستجوی نماد";
                SymbolSearchTextBox.Focus();
                break;
            case "Codal":
                CodalPage.Visibility = Visibility.Visible;
                PageTitleText.Text = "اطلاعیه‌های کدال";
                break;
            case "Settings":
                SettingsPage.Visibility = Visibility.Visible;
                PageTitleText.Text = "تنظیمات";
                break;
            default:
                DashboardPage.Visibility = Visibility.Visible;
                PageTitleText.Text = "داشبورد بازار";
                break;
        }
    }

    private void StockDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid { SelectedItem: StockItem stock })
            return;

        AnalysisSymbolComboBox.SelectedItem = stock;
        ShowPage("Analysis");
    }

    private void AnalysisSymbolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AnalysisSymbolComboBox.SelectedItem is not StockItem stock)
            return;

        AnalysisSymbolTitleText.Text = $"{stock.Symbol} — {stock.Name}";
        AnalysisScoreText.Text = $"{stock.Score:N0} / 100";
        AnalysisRsiText.Text = stock.Rsi14 > 0 ? stock.Rsi14.ToString("N1") : "ناموجود";
        AnalysisSmaText.Text = stock.Sma5 > 0 && stock.Sma20 > 0
            ? $"{stock.Sma5:N0} / {stock.Sma20:N0}"
            : "ناموجود";
        AnalysisVolumeText.Text = stock.VolumeRatio > 0 ? $"{stock.VolumeRatio:N2} برابر" : "ناموجود";
        AnalysisSummaryText.Text = $"وضعیت محاسباتی: {stock.Status}. {stock.AnalysisReason}";
    }

    private void ScannerFilter_Changed(object sender, EventArgs e)
        => _scannerView?.Refresh();

    private bool ScannerFilter(object item)
    {
        if (item is not StockItem stock)
            return false;

        var query = NormalizeSearch(ScannerSearchTextBox?.Text);
        var matchesQuery = query.Length == 0 ||
                           NormalizeSearch(stock.Symbol).Contains(query) ||
                           NormalizeSearch(stock.Name).Contains(query);

        var status = (ScannerStatusComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var matchesStatus = string.IsNullOrWhiteSpace(status) || status == "همه وضعیت‌ها" || stock.Status == status;
        return matchesQuery && matchesStatus;
    }

    private void SymbolSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        => _searchView.Refresh();

    private bool SearchFilter(object item)
    {
        if (item is not StockItem stock)
            return false;
        var query = NormalizeSearch(SymbolSearchTextBox?.Text);
        return query.Length == 0 ||
               NormalizeSearch(stock.Symbol).Contains(query) ||
               NormalizeSearch(stock.Name).Contains(query) ||
               stock.InsCode.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void CodalSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        => _codalView.Refresh();

    private bool CodalFilter(object item)
    {
        if (item is not CodalAnnouncement announcement)
            return false;
        var query = NormalizeSearch(CodalSearchTextBox?.Text);
        return query.Length == 0 ||
               NormalizeSearch(announcement.Symbol).Contains(query) ||
               NormalizeSearch(announcement.CompanyName).Contains(query) ||
               NormalizeSearch(announcement.Title).Contains(query);
    }

    private void CodalDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CodalDataGrid.SelectedItem is not CodalAnnouncement announcement ||
            string.IsNullOrWhiteSpace(announcement.Url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(announcement.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"باز کردن اطلاعیه ممکن نشد:\n{ex.Message}", "کدال",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings, out var error))
        {
            MessageBox.Show(this, error, "تنظیمات نامعتبر", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings = settings;
        await _store.SaveSettingsAsync(_settings, _lifetimeCancellation.Token);
        PopulateSettingsControls();
        ConfigureAutoUpdateTimer();
        SyncStageText.Text = "تنظیمات ذخیره شد";
        SyncDetailText.Text = "تنظیمات جدید از به‌روزرسانی بعدی اعمال می‌شود.";
    }

    private bool TryReadSettings(out AppSettings settings, out string error)
    {
        settings = new AppSettings();
        error = "";

        if (!int.TryParse(UpdateIntervalTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval) ||
            interval is < 5 or > 240)
        {
            error = "فاصله به‌روزرسانی باید عددی بین ۵ تا ۲۴۰ دقیقه باشد.";
            return false;
        }

        if (!int.TryParse(HistoryDaysTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var historyDays) ||
            historyDays is < 30 or > 500)
        {
            error = "تعداد روزهای تاریخچه باید عددی بین ۳۰ تا ۵۰۰ باشد.";
            return false;
        }

        if (!int.TryParse(HistoryLimitTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var historyLimit) ||
            historyLimit is < 10 or > 1000)
        {
            error = "تعداد نمادهای تحلیل‌شونده باید عددی بین ۱۰ تا ۱۰۰۰ باشد.";
            return false;
        }

        settings = new AppSettings
        {
            AutoUpdateEnabled = AutoUpdateCheckBox.IsChecked == true,
            UpdateIntervalMinutes = interval,
            HistoryDays = historyDays,
            HistorySymbolLimit = historyLimit
        };
        return true;
    }

    private void PopulateSettingsControls()
    {
        AutoUpdateCheckBox.IsChecked = _settings.AutoUpdateEnabled;
        UpdateIntervalTextBox.Text = _settings.UpdateIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        HistoryDaysTextBox.Text = _settings.HistoryDays.ToString(CultureInfo.InvariantCulture);
        HistoryLimitTextBox.Text = _settings.HistorySymbolLimit.ToString(CultureInfo.InvariantCulture);
        DataDirectoryText.Text = _store.DataDirectory;
    }

    private void ConfigureAutoUpdateTimer()
    {
        _autoUpdateTimer.Stop();
        if (!_settings.AutoUpdateEnabled)
            return;

        _autoUpdateTimer.Interval = TimeSpan.FromMinutes(_settings.UpdateIntervalMinutes);
        _autoUpdateTimer.Start();
    }

    private static string NormalizeSearch(string? value)
        => (value ?? "")
            .Replace('ي', 'ی')
            .Replace('ك', 'ک')
            .Trim()
            .ToLowerInvariant();
}
