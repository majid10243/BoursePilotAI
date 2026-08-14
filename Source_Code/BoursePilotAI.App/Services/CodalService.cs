using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoursePilotAI.Models;

namespace BoursePilotAI.Services;

public sealed class CodalService
{
    private readonly HttpClient _httpClient;

    public CodalService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<CodalAnnouncement>> GetLatestAnnouncementsAsync(
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 20, 200);
        var url = "https://search.codal.ir/api/search/v2/q" +
                  $"?PageNumber=1&PageSize={pageSize}" +
                  "&Audited=true&NotAudited=true&IsNotAudited=false" +
                  "&Childs=true&Mains=true&Publisher=false" +
                  "&CompanyState=0&Category=-1&CompanyType=1" +
                  "&Consolidatable=true&NotConsolidatable=true" +
                  "&LetterType=-1&AuditorRef=-1&search=true";

        using var document = await GetJsonWithRetryAsync(url, cancellationToken);
        var announcements = document.RootElement
            .FindArrayItems("Letters", "letters")
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(ParseAnnouncement)
            .Where(item => item.TracingNo > 0 || !string.IsNullOrWhiteSpace(item.Url))
            .GroupBy(item => item.TracingNo > 0 ? item.TracingNo.ToString() : item.Url)
            .Select(group => group.First())
            .ToList();

        if (announcements.Count == 0)
            throw new InvalidDataException("پاسخ کدال دریافت شد، اما فهرست Letters قابل خواندن نبود.");

        return announcements;
    }

    private async Task<JsonDocument> GetJsonWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Referrer = new Uri("https://www.codal.ir/");

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
                {
                    lastError = new HttpRequestException($"کدال پاسخ {(int)response.StatusCode} داد.");
                    if (attempt < 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 3), cancellationToken);
                        continue;
                    }
                }

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = new TimeoutException("مهلت دریافت پاسخ از کدال تمام شد.");
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException)
            {
                lastError = ex;
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 3), cancellationToken);
        }

        throw new HttpRequestException("دریافت اطلاعیه‌ها از کدال پس از سه تلاش ناموفق بود.", lastError);
    }

    private static CodalAnnouncement ParseAnnouncement(JsonElement item)
    {
        var url = item.ReadString("Url", "url");
        var attachment = item.ReadString("AttachmentUrl", "attachmentUrl");
        return new CodalAnnouncement
        {
            TracingNo = item.ReadLong("TracingNo", "tracingNo"),
            Symbol = NormalizePersianText(item.ReadString("Symbol", "symbol")),
            CompanyName = NormalizePersianText(item.ReadString("CompanyName", "companyName")),
            Title = NormalizePersianText(item.ReadString("Title", "title")),
            LetterCode = item.ReadString("LetterCode", "letterCode"),
            SentDateTime = item.ReadString("SentDateTime", "sentDateTime"),
            PublishDateTime = item.ReadString("PublishDateTime", "publishDateTime"),
            Url = MakeAbsoluteUrl(url),
            AttachmentUrl = MakeAbsoluteUrl(attachment)
        };
    }

    private static string MakeAbsoluteUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            Uri.TryCreate("https://www.codal.ir" + (value.StartsWith('/') ? value : "/" + value),
                UriKind.Absolute, out absolute);

        if (absolute is null)
            return "";

        var isCodalHost = absolute.Host.Equals("codal.ir", StringComparison.OrdinalIgnoreCase) ||
                          absolute.Host.EndsWith(".codal.ir", StringComparison.OrdinalIgnoreCase);
        return isCodalHost
            ? absolute.ToString()
            : "";
    }

    private static string NormalizePersianText(string value)
        => value.Replace('ي', 'ی').Replace('ك', 'ک').Trim();
}
