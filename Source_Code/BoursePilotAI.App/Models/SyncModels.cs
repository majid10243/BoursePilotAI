using System;
using System.Collections.Generic;

namespace BoursePilotAI.Models;

public sealed record SyncProgress(int Percent, string Stage, string Detail);

public sealed class DataSyncResult
{
    public IReadOnlyList<StockItem> Stocks { get; init; } = Array.Empty<StockItem>();
    public IReadOnlyList<CodalAnnouncement> Announcements { get; init; } = Array.Empty<CodalAnnouncement>();
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public bool UsedCachedMarketData { get; init; }
    public bool UsedCachedCodalData { get; init; }
}
