using BoursePilotAI.Models;

namespace BoursePilotAI.Services;

public sealed class StockAnalyzer
{
    public void Analyze(StockItem stock, IReadOnlyList<PriceHistoryItem> history)
    {
        var closes = history
            .Where(item => item.Close > 0)
            .OrderBy(item => item.DateKey)
            .Select(item => item.Close)
            .ToList();

        var volumes = history
            .Where(item => item.Volume >= 0)
            .OrderBy(item => item.DateKey)
            .Select(item => item.Volume)
            .ToList();

        stock.Sma5 = AverageLast(closes, 5);
        stock.Sma20 = AverageLast(closes, 20);
        stock.Rsi14 = CalculateRsi(closes, 14);
        stock.Momentum5 = CalculateMomentum(closes, 5);

        var averageVolume20 = AverageLast(volumes, 20);
        stock.VolumeRatio = averageVolume20 > 0 ? Math.Round(stock.Volume / averageVolume20, 2) : 0;

        var score = 50;
        var reasons = new List<string>();

        if (stock.ClosingChangePercent >= 2)
        {
            score += 10;
            reasons.Add("تغییر روزانه مثبت");
        }
        else if (stock.ClosingChangePercent <= -2)
        {
            score -= 10;
            reasons.Add("تغییر روزانه منفی");
        }

        if (stock.Sma5 > 0 && stock.Sma20 > 0)
        {
            if (stock.Sma5 > stock.Sma20)
            {
                score += 18;
                reasons.Add("میانگین ۵روزه بالاتر از ۲۰روزه");
            }
            else
            {
                score -= 18;
                reasons.Add("میانگین ۵روزه پایین‌تر از ۲۰روزه");
            }
        }

        if (stock.Momentum5 >= 3)
        {
            score += 10;
            reasons.Add("مومنتوم ۵روزه مثبت");
        }
        else if (stock.Momentum5 <= -3)
        {
            score -= 10;
            reasons.Add("مومنتوم ۵روزه منفی");
        }

        if (stock.Rsi14 is >= 50 and <= 70)
        {
            score += 8;
            reasons.Add("RSI در محدوده مثبت");
        }
        else if (stock.Rsi14 >= 75)
        {
            score -= 8;
            reasons.Add("RSI در محدوده اشباع نسبی");
        }
        else if (stock.Rsi14 is > 0 and < 30)
        {
            score -= 5;
            reasons.Add("RSI ضعیف");
        }

        if (stock.VolumeRatio >= 1.5)
        {
            var volumeScore = stock.ClosingChangePercent >= 0 ? 8 : -8;
            score += volumeScore;
            reasons.Add("حجم بالاتر از میانگین ۲۰روزه");
        }

        stock.Score = Math.Clamp(score, 0, 100);
        stock.Status = stock.Score switch
        {
            >= 75 => "روند قوی",
            >= 60 => "مثبت",
            >= 45 => "خنثی",
            >= 30 => "ضعیف",
            _ => "پرریسک"
        };
        stock.AnalysisReason = reasons.Count > 0
            ? string.Join("، ", reasons)
            : "برای تحلیل فنی دقیق‌تر، تاریخچه بیشتری لازم است.";
    }

    private static double AverageLast(IReadOnlyList<double> values, int period)
    {
        if (values.Count < period)
            return 0;
        return Math.Round(values.Skip(values.Count - period).Average(), 2);
    }

    private static double CalculateMomentum(IReadOnlyList<double> closes, int period)
    {
        if (closes.Count <= period)
            return 0;
        var basis = closes[closes.Count - 1 - period];
        return basis <= 0 ? 0 : Math.Round((closes[^1] - basis) / basis * 100, 2);
    }

    private static double CalculateRsi(IReadOnlyList<double> closes, int period)
    {
        if (closes.Count <= period)
            return 0;

        double gains = 0;
        double losses = 0;
        for (var index = closes.Count - period; index < closes.Count; index++)
        {
            var change = closes[index] - closes[index - 1];
            if (change > 0)
                gains += change;
            else
                losses -= change;
        }

        var averageGain = gains / period;
        var averageLoss = losses / period;
        if (averageLoss == 0)
            return averageGain > 0 ? 100 : 50;

        var relativeStrength = averageGain / averageLoss;
        return Math.Round(100 - 100 / (1 + relativeStrength), 2);
    }
}
