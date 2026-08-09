using System.Text.Json;
using BoursePilotAI.Models;

namespace BoursePilotAI.Services;

public sealed class LocalDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public LocalDataStore()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BoursePilotAI",
            "Data");
        HistoryDirectory = Path.Combine(DataDirectory, "History");
        Directory.CreateDirectory(HistoryDirectory);
    }

    public string DataDirectory { get; }
    private string HistoryDirectory { get; }
    private string MarketPath => Path.Combine(DataDirectory, "market-latest.json");
    private string CodalPath => Path.Combine(DataDirectory, "codal-latest.json");
    private string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public Task<IReadOnlyList<StockItem>> LoadMarketAsync(CancellationToken cancellationToken = default)
        => LoadListAsync<StockItem>(MarketPath, cancellationToken);

    public Task SaveMarketAsync(IReadOnlyList<StockItem> stocks, CancellationToken cancellationToken = default)
        => SaveAsync(MarketPath, stocks, cancellationToken);

    public Task<IReadOnlyList<CodalAnnouncement>> LoadCodalAsync(CancellationToken cancellationToken = default)
        => LoadListAsync<CodalAnnouncement>(CodalPath, cancellationToken);

    public Task SaveCodalAsync(
        IReadOnlyList<CodalAnnouncement> announcements,
        CancellationToken cancellationToken = default)
        => SaveAsync(CodalPath, announcements, cancellationToken);

    public Task<IReadOnlyList<PriceHistoryItem>> LoadHistoryAsync(
        string insCode,
        CancellationToken cancellationToken = default)
        => LoadListAsync<PriceHistoryItem>(GetHistoryPath(insCode), cancellationToken);

    public Task SaveHistoryAsync(
        string insCode,
        IReadOnlyList<PriceHistoryItem> history,
        CancellationToken cancellationToken = default)
        => SaveAsync(GetHistoryPath(insCode), history, cancellationToken);

    public bool IsHistoryFresh(string insCode)
    {
        var path = GetHistoryPath(insCode);
        return File.Exists(path) && File.GetLastWriteTime(path).Date == DateTime.Now.Date;
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                           ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Normalize();
        return SaveAsync(SettingsPath, settings, cancellationToken);
    }

    private async Task<IReadOnlyList<T>> LoadListAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return Array.Empty<T>();

        try
        {
            await using var stream = File.OpenRead(path);
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken);
            if (items is null)
                return Array.Empty<T>();
            return items;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Array.Empty<T>();
        }
    }

    private async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = path + ".tmp";
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, path, true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private string GetHistoryPath(string insCode)
    {
        var safeCode = new string(insCode.Where(char.IsDigit).ToArray());
        return Path.Combine(HistoryDirectory, $"{(safeCode.Length > 0 ? safeCode : "unknown")}.json");
    }
}
