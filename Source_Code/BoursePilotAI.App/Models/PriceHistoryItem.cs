namespace BoursePilotAI.Models;

public sealed class PriceHistoryItem
{
    public int DateKey { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Last { get; set; }
    public double Close { get; set; }
    public double Yesterday { get; set; }
    public double Volume { get; set; }
    public double Value { get; set; }
    public long TradeCount { get; set; }
}
