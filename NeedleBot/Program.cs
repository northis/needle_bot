using System;
using System.Linq;
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
            var history = new History("201910.json");
            var trade = new Trade(new LocalConfig());
            var initialAssetsUsd =
                Math.Round(trade.Config.WalletUsd + trade.Config.WalletBtc * trade.Config.ZeroProfitPriceUsd, 2);

            Console.WriteLine($"We have {initialAssetsUsd} USD");

            //trade.OnStateChanged += Trade_OnStateChanged;

            var startDate = new DateTime(2019, 10, 1, 0, 0, 0);
            var endDate = new DateTime(2019, 11, 1, 0, 0, 0);
            await history.LoadPrices(startDate, endDate, TimeSpan.FromDays(1));

            var prices = history.GetPrices();
            foreach (var priceItem in prices)
            {
               await trade.Decide(priceItem.Price, priceItem.Date).ConfigureAwait(false);
            }

            var last = prices.Last();
            var totalUsd = Math.Round(trade.Config.WalletUsd + trade.Config.WalletBtc * last.Price, 2);

            Console.WriteLine($"WalletUsd: {trade.Config.WalletUsd:F2}");
            Console.WriteLine($"WalletBtc: {trade.Config.WalletBtc:F5}");
            Console.WriteLine($"Total {totalUsd:F2} USD");
            Console.WriteLine($"Profit {totalUsd - initialAssetsUsd:F2} USD");
        }

        private static void Trade_OnStateChanged(object sender, EventArgs e)
        {
            var trade = (Trade)sender;
            Console.WriteLine($"Instant profit: {trade.InstantProfitUsd:F2} USD");
        }
    }
}
