using System;
using System.Threading.Tasks;
using NeedleBot.Models;

namespace NeedleBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Run().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task Run()
        {
            var history = new History();
            var trade = new Trade(new LocalConfig());

            Console.WriteLine($"WalletUsd: {trade.Config.WalletUsd}");
            Console.WriteLine(
                $"WalletBtc: {trade.Config.WalletBtc} ({trade.Config.WalletBtc * trade.Config.ZeroProfitPriceUsd} USD)");

             trade.OnStateChanged += Trade_OnStateChanged;

            //var startDate = new DateTime(2019, 11, 26, 0, 0, 0);
            //var endDate = new DateTime(2019, 12, 26, 0, 0, 0);
            //await history.LoadPrices(startDate, endDate, TimeSpan.FromDays(1));

            var prices = history.GetPrices();
            foreach (var priceItem in prices)
            {
               await trade.Decide(priceItem.Price, priceItem.Date).ConfigureAwait(false);
            }
            Console.WriteLine($"WalletUsd: {trade.Config.WalletUsd}");
            Console.WriteLine($"WalletBtc: {trade.Config.WalletBtc}");
        }

        private static void Trade_OnStateChanged(object sender, EventArgs e)
        {
            var trade = (Trade)sender;
            Console.WriteLine($"Instant profit: {trade.InstantProfitUsd}");
        }
    }
}
