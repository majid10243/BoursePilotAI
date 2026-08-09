namespace BoursePilotAI.Models;

public sealed class AppSettings
{
    public bool AutoUpdateEnabled { get; set; } = true;
    public int UpdateIntervalMinutes { get; set; } = 15;
    public int HistoryDays { get; set; } = 120;
    public int HistorySymbolLimit { get; set; } = 100;

    public void Normalize()
    {
        UpdateIntervalMinutes = Math.Clamp(UpdateIntervalMinutes, 5, 240);
        HistoryDays = Math.Clamp(HistoryDays, 30, 500);
        HistorySymbolLimit = Math.Clamp(HistorySymbolLimit, 10, 1000);
    }
}
