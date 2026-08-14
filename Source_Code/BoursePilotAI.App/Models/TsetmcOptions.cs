using System;
using System.Text.Json.Serialization;

namespace BoursePilotAI.Models;

/// <summary>
/// TSETMC endpoint configuration loaded from appsettings.json.
/// </summary>
public sealed class TsetmcOptions
{
    public const string SectionName = "Tsetmc";

    /// <summary>Base address of the TSETMC content API, e.g. "https://cdn.tsetmc.com".</summary>
    public string BaseUrl { get; set; } = "https://cdn.tsetmc.com";

    /// <summary>Absolute or relative path of the market watch endpoint.</summary>
    public string MarketWatchPath { get; set; } =
        "/api/ClosingPrice/GetMarketWatch?market=0&industrialGroup=&paperTypes%5B0%5D=1" +
        "&showTraded=false&withBestLimits=true&hEven=0&RefID=0";

    /// <summary>
    /// History endpoint template; {0} is replaced with the numeric instrument code and
    /// {1} with the number of days.
    /// </summary>
    public string HistoryPathTemplate { get; set; } =
        "/api/ClosingPrice/GetClosingPriceDailyList/{0}/{1}";

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>Maximum number of attempts before failing a request.</summary>
    public int MaxRetries { get; set; } = 3;

    [JsonIgnore]
    public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Max(1, TimeoutSeconds));

    [JsonIgnore]
    public int RetryCount => Math.Max(1, MaxRetries);

    /// <summary>
    /// Throws a clear error when the endpoint configuration is incomplete so that the
    /// problem is reported at startup instead of as an opaque network failure.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl) ||
            !Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("پیکربندی نقطه TSETMC ناقص است: آدرس BaseUrl معتبر نیست.");
        }

        if (string.IsNullOrWhiteSpace(MarketWatchPath))
        {
            throw new InvalidOperationException("پیکربندی نقطه TSETMC ناقص است: مسیر دیدبان بازار خالی است.");
        }

        if (string.IsNullOrWhiteSpace(HistoryPathTemplate) ||
            !HistoryPathTemplate.Contains("{0}") ||
            !HistoryPathTemplate.Contains("{1}"))
        {
            throw new InvalidOperationException("پیکربندی نقطه TSETMC ناقص است: قالب مسیر تاریخچه نامعتبر است.");
        }
    }

    public Uri BuildMarketWatchUri() => BuildUri(MarketWatchPath);

    public Uri BuildHistoryUri(string insCode, int days)
    {
        var safeCode = new string(insCode.Where(char.IsDigit).ToArray());
        if (safeCode.Length == 0)
            safeCode = "0";

        var path = string.Format(HistoryPathTemplate, safeCode, Math.Max(1, days));
        return BuildUri(path);
    }

    private Uri BuildUri(string path)
    {
        var baseUri = new Uri(BaseUrl.TrimEnd('/'));
        return new Uri(baseUri, path.TrimStart('/'));
    }
}
