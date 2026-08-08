namespace BoursePilotAI.Services
{
    public class StockAnalyzer
    {
        public string Analyze(double change)
        {
            if (change >= 2)
                return "خرید";

            if (change <= -2)
                return "بررسی";

            return "نگهداری";
        }
    }
}
