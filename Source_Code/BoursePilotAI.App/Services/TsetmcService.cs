using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BoursePilotAI.Models;

namespace BoursePilotAI.Services;

public sealed class TsetmcService
{
    private readonly HttpClient _httpClient;
    private readonly TsetmcOptions _options;

    public TsetmcService(HttpClient httpClient, TsetmcOptions options)
    {
        _httpClient = httpClient;
        _options = options ?? new TsetmcOptions();
        _options.Validate();
    }

    public async Task<IReadOnlyList<StockItem>> GetMarketWatchAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonWithRetryAsync(_options.BuildMarketWatchUri(), cancellationToken);
        var now = DateTimeOffset.Now;
        var stocks = document.RootElement
            .FindArrayItems("marketwatch", "marketWatch")
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => ParseMarketItem(item, now))
            .Where(item => !string.IsNullOrWhiteSpace(item.InsCode) && !string.IsNullOrWhiteSpace(item.Symbol))
            .GroupBy(item => item.InsCode)
            .Select(group => group.First())
            .OrderByDescending(item => item.Value)
            .ToList();

        if (stocks.Count == 0)
            throw new InvalidDataException("پاسخ TSETMC دریافت شد، اما آرایه marketwatch قابل خواندن نبود.");

        return stocks;
    }

    public async Task<IReadOnlyList<PriceHistoryItem>> GetHistoryAsync(
        string insCode,
        int days,
        CancellationToken cancellationToken)
    {
        var safeCode = new string(insCode.Where(char.IsDigit).ToArray());
        if (safeCode.Length == 0)
            return Array.Empty<PriceHistoryItem>();

        var url = _options.BuildHistoryUri(insCode, days);
        using var document = await GetJsonWithRetryAsync(url, cancellationToken);
        var history = document.RootElement
            .FindArrayItems("closingPriceDaily", "closingPriceDailyList")
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(ParseHistoryItem)
            .Where(item => item.DateKey > 0 && item.Close > 0)
            .GroupBy(item => item.DateKey)
            .Select(group => group.First())
            .OrderBy(item => item.DateKey)
            .ToList();

        return history;
    }

    private async Task<JsonDocument> GetJsonWithRetryAsync(Uri url, CancellationToken cancellationToken)
    {
        var retries = _options.RetryCount;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Referrer = new Uri("https://www.tsetmc.com/");

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
                {
                    lastError = new HttpRequestException($"TSETMC پاسخ {(int)response.StatusCode} داد.");
                    if (attempt < retries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = new TimeoutException("مهلت دریافت پاسخ از TSETMC تمام شد.");
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
            {
                lastError = ex;
            }

            if (attempt < retries)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
        }

        throw new HttpRequestException(
            $"دریافت داده از TSETMC پس از {retries} تلاش ناموفق بود.", lastError);
    }

    private static StockItem ParseMarketItem(JsonElement item, DateTimeOffset updatedAt)
    {
        var yesterday = item.ReadDouble("py", "priceYesterday");
        var last = item.ReadDouble("pdv", "pDrCotVal", "lastPrice");
        var close = item.ReadDouble("pcl", "pClosing", "closingPrice");
        var peText = item.ReadString("pe", "pE");
        var pe = double.TryParse(peText, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsedPe)
            ? parsedPe
            : (double?)null;

        return new StockItem
        {
            InsCode = item.ReadString("insCode", "insID"),
            Symbol = NormalizePersianText(item.ReadString("lva", "lVal18AFC", "symbol")),
            Name = NormalizePersianText(item.ReadString("lvc", "lVal30", "name")),
            LastPrice = last,
            ClosingPrice = close,
            YesterdayPrice = yesterday,
            FirstPrice = item.ReadDouble("pf", "priceFirst", "open"),
            MinPrice = item.ReadDouble("pmn", "priceMin", "low"),
            MaxPrice = item.ReadDouble("pmx", "priceMax", "high"),
            TradeCount = item.ReadLong("ztt", "zTotTran", "tradeCount"),
            Volume = item.ReadDouble("qtj", "qTotTran5J", "volume"),
            Value = item.ReadDouble("qtc", "qTotCap", "value"),
            Eps = item.ReadDouble("eps"),
            PeRatio = pe,
            LastChangePercent = Percentage(last, yesterday),
            ClosingChangePercent = Percentage(close, yesterday),
            UpdatedAt = updatedAt
        };
    }

    private static PriceHistoryItem ParseHistoryItem(JsonElement item)
    {
        return new PriceHistoryItem
        {
            DateKey = item.ReadInt("dEven", "date"),
            Open = item.ReadDouble("priceFirst", "open"),
            High = item.ReadDouble("priceMax", "high"),
            Low = item.ReadDouble("priceMin", "low"),
            Last = item.ReadDouble("pDrCotVal", "last", "close"),
            Close = item.ReadDouble("pClosing", "final"),
            Yesterday = item.ReadDouble("priceYesterday", "yesterday"),
            Volume = item.ReadDouble("qTotTran5J", "volume"),
            Value = item.ReadDouble("qTotCap", "value"),
            TradeCount = item.ReadLong("zTotTran", "tradeCount")
        };
    }

    private static double Percentage(double value, double basis)
        => basis <= 0 ? 0 : Math.Round((value - basis) / basis * 100, 2);

    private static string NormalizePersianText(string value)
        => value.Replace('ي', 'ی').Replace('ك', 'ک').Trim();
}
