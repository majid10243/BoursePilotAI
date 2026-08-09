namespace BoursePilotAI;

public sealed class StockItem
{
    public string InsCode { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public double LastPrice { get; set; }
    public double ClosingPrice { get; set; }
    public double YesterdayPrice { get; set; }
    public double FirstPrice { get; set; }
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
    public long TradeCount { get; set; }
    public double Volume { get; set; }
    public double Value { get; set; }
    public double Eps { get; set; }
    public double? PeRatio { get; set; }
    public double LastChangePercent { get; set; }
    public double ClosingChangePercent { get; set; }
    public double Sma5 { get; set; }
    public double Sma20 { get; set; }
    public double Rsi14 { get; set; }
    public double VolumeRatio { get; set; }
    public double Momentum5 { get; set; }
    public int Score { get; set; }
    public string Status { get; set; } = "در انتظار تحلیل";
    public string AnalysisReason { get; set; } = "هنوز تاریخچه کافی دریافت نشده است.";
    public DateTimeOffset UpdatedAt { get; set; }
}
