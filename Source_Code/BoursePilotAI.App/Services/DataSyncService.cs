using BoursePilotAI.Models;

namespace BoursePilotAI.Services;

public sealed class DataSyncService
{
    private readonly TsetmcService _tsetmc;
    private readonly CodalService _codal;
    private readonly LocalDataStore _store;
    private readonly StockAnalyzer _analyzer;

    public DataSyncService(
        TsetmcService tsetmc,
        CodalService codal,
        LocalDataStore store,
        StockAnalyzer analyzer)
    {
        _tsetmc = tsetmc;
        _codal = codal;
        _store = store;
        _analyzer = analyzer;
    }

    public async Task<DataSyncResult> SynchronizeAsync(
        AppSettings settings,
        IProgress<SyncProgress> progress,
        CancellationToken cancellationToken)
    {
        settings.Normalize();
        var warnings = new List<string>();
        var cachedStocks = await _store.LoadMarketAsync(cancellationToken);
        var cachedCodal = await _store.LoadCodalAsync(cancellationToken);
        IReadOnlyList<StockItem> stocks = cachedStocks;
        IReadOnlyList<CodalAnnouncement> announcements = cachedCodal;
        var usedCachedMarket = false;
        var usedCachedCodal = false;
        var marketSourceAvailable = false;

        progress.Report(new SyncProgress(3, "خواندن اطلاعات محلی", "در حال بارگذاری آخرین داده ذخیره‌شده"));

        try
        {
            progress.Report(new SyncProgress(8, "اتصال به TSETMC", "در حال دریافت دیدبان بازار"));
            stocks = await _tsetmc.GetMarketWatchAsync(cancellationToken);
            marketSourceAvailable = true;
            await _store.SaveMarketAsync(stocks, cancellationToken);
            progress.Report(new SyncProgress(20, "ذخیره دیدبان بازار", $"{stocks.Count:N0} نماد دریافت شد"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            usedCachedMarket = cachedStocks.Count > 0;
            warnings.Add($"TSETMC: {ex.Message}");
            if (cachedStocks.Count == 0)
                warnings.Add("هیچ داده محلی از بازار موجود نیست.");
            progress.Report(new SyncProgress(20, "TSETMC در دسترس نیست", "نمایش آخرین داده ذخیره‌شده"));
        }

        var candidates = stocks
            .Where(stock => stock.Volume > 0 && !string.IsNullOrWhiteSpace(stock.InsCode))
            .OrderByDescending(stock => stock.Value)
            .Take(settings.HistorySymbolLimit)
            .ToList();

        var historyFailures = 0;
        var consecutiveHistoryFailures = 0;
        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stock = candidates[index];
            IReadOnlyList<PriceHistoryItem> history;

            try
            {
                if (_store.IsHistoryFresh(stock.InsCode) || !marketSourceAvailable)
                {
                    history = await _store.LoadHistoryAsync(stock.InsCode, cancellationToken);
                }
                else
                {
                    history = await _tsetmc.GetHistoryAsync(stock.InsCode, settings.HistoryDays, cancellationToken);
                    if (history.Count > 0)
                        await _store.SaveHistoryAsync(stock.InsCode, history, cancellationToken);
                    await Task.Delay(120, cancellationToken);
                }

                _analyzer.Analyze(stock, history);
                consecutiveHistoryFailures = 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                historyFailures++;
                consecutiveHistoryFailures++;
                history = await _store.LoadHistoryAsync(stock.InsCode, cancellationToken);
                _analyzer.Analyze(stock, history);
                if (historyFailures <= 3)
                    warnings.Add($"تاریخچه {stock.Symbol}: {ex.Message}");

                if (consecutiveHistoryFailures >= 3)
                {
                    marketSourceAvailable = false;
                    warnings.Add("دریافت تاریخچه پس از سه خطای پیاپی متوقف شد؛ باقی نمادها از کش تحلیل شدند.");
                }
            }

            var percent = candidates.Count == 0
                ? 70
                : 20 + (int)Math.Round((index + 1d) / candidates.Count * 50);
            progress.Report(new SyncProgress(
                percent,
                "به‌روزرسانی تاریخچه و تحلیل",
                $"{index + 1:N0} از {candidates.Count:N0} نماد — {stock.Symbol}"));
        }

        foreach (var stock in stocks.Except(candidates))
            _analyzer.Analyze(stock, Array.Empty<PriceHistoryItem>());

        if (stocks.Count > 0)
            await _store.SaveMarketAsync(stocks, cancellationToken);

        if (historyFailures > 3)
            warnings.Add($"تاریخچه {historyFailures:N0} نماد کامل به‌روزرسانی نشد و در صورت وجود از کش استفاده شد.");

        try
        {
            progress.Report(new SyncProgress(78, "اتصال به کدال", "در حال دریافت آخرین اطلاعیه‌ها"));
            announcements = await _codal.GetLatestAnnouncementsAsync(100, cancellationToken);
            await _store.SaveCodalAsync(announcements, cancellationToken);
            progress.Report(new SyncProgress(92, "ذخیره اطلاعیه‌های کدال", $"{announcements.Count:N0} اطلاعیه دریافت شد"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            usedCachedCodal = cachedCodal.Count > 0;
            warnings.Add($"کدال: {ex.Message}");
            progress.Report(new SyncProgress(92, "کدال در دسترس نیست", "نمایش آخرین اطلاعیه ذخیره‌شده"));
        }

        progress.Report(new SyncProgress(100, "به‌روزرسانی کامل شد", BuildCompletionDetail(stocks, announcements, warnings)));
        return new DataSyncResult
        {
            Stocks = stocks.OrderByDescending(stock => stock.Value).ToList(),
            Announcements = announcements,
            CompletedAt = DateTimeOffset.Now,
            Warnings = warnings,
            UsedCachedMarketData = usedCachedMarket,
            UsedCachedCodalData = usedCachedCodal
        };
    }

    private static string BuildCompletionDetail(
        IReadOnlyCollection<StockItem> stocks,
        IReadOnlyCollection<CodalAnnouncement> announcements,
        IReadOnlyCollection<string> warnings)
    {
        var suffix = warnings.Count == 0 ? "بدون خطا" : $"با {warnings.Count:N0} هشدار";
        return $"{stocks.Count:N0} نماد و {announcements.Count:N0} اطلاعیه — {suffix}";
    }
}
