using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeedleBot.Enums;
using NeedleBot.Models;

namespace NeedleBot
{
    class Program
    {
        static void Main(string[] args)
        {
            ProcessHistory().ConfigureAwait(false).GetAwaiter().GetResult();

            // AnalyzeReport();
            Console.ReadLine();
        }

        private const string REPORT_FILE = "report.txt";
        
        static async Task ProcessHistory()
        {
            var oldTitle = Console.Title;
            Console.SetOut(TextWriter.Null);

            var maxDetectPriceChangeUsd = 200D;
            var maxStopPriceAllowanceUsd = 200;
            var total = maxDetectPriceChangeUsd * maxStopPriceAllowanceUsd / 100;
            var current = 0;

            //for (var i = 1; i < 12; i++)
            //{
            //    var history = new History($"2019{i:00}.json");

            //    var startDate = new DateTime(2019, i, 1, 0, 0, 0);
            //    var endDate = new DateTime(2019, i + 1, 1, 0, 0, 0);
            //    await history.LoadPrices(startDate, endDate, TimeSpan.FromDays(1));
            //}
            var history = new History("data.json");
            var analysis = new ConcurrentBag<Tuple<int, int, double, int, int>>();
            var tasks = new List<Task>();

            async Task FuncStopPrice(int stopPriceAllowance)
            {
                for (var j = 0; j < maxStopPriceAllowanceUsd; j += 10)
                {
                    var i1 = stopPriceAllowance;
                    var j1 = j;

                    var trade = new Trade(new LocalConfig
                    {
                        DetectPriceChangeUsd = i1, 
                        StopPriceAllowanceUsd = j1
                    });

                    var profit = await TradeTask(history, trade, true);
                    analysis.Add(new Tuple<int, int, double, int, int>(i1, j1, profit, trade.SellCount, trade.BuyCount));

                    Interlocked.Increment(ref current);
                    Console.Title = $"Calculating... {100 * current / total:F1}%";
                }
            }

            for (var i = 0; i < maxDetectPriceChangeUsd; i += 10)
            {
                var iLocal = i;
                var task = Task.Run(() => FuncStopPrice(iLocal));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            Console.Title = oldTitle;

            var file = File.CreateText(REPORT_FILE);
            foreach (var analysisItem in analysis.OrderByDescending(a=>a.Item4))
            {
                await file.WriteLineAsync(
                    $"{analysisItem.Item1}\t{analysisItem.Item2}\t{analysisItem.Item3:F2}\t{analysisItem.Item4}\t{analysisItem.Item5}");
            }
        }

        static async Task<double> TradeTask(History history, Trade trade, bool autoSetZeroPrice = false)
        {
            //trade.OnStateChanged += Trade_OnStateChanged;
            
            var prices = history.GetPrices();
            if (autoSetZeroPrice && trade.Config.Mode == ModeEnum.BTC)
            {
                trade.Config.ZeroProfitPriceUsd = prices.First().Price + 100;
                if (trade.Config.WalletBtc <= 0)
                {
                    trade.Config.WalletBtc = trade.Config.TradeVolumeUsd / trade.Config.ZeroProfitPriceUsd;
                }
            }

            var initialAssetsUsd =
                Math.Round(trade.Config.WalletUsd + trade.Config.WalletBtc * trade.Config.ZeroProfitPriceUsd, 2);

            Console.WriteLine($"We have {initialAssetsUsd} USD");

            foreach (var priceItem in prices)
            {
                await trade.Decide(priceItem.Price, priceItem.Date).ConfigureAwait(false);
            }

            var last = prices.Last();
            var totalUsd = trade.Config.WalletUsd + trade.Config.WalletBtc * last.Price;
            var profit = totalUsd - initialAssetsUsd;

            Console.WriteLine($"WalletUsd: {trade.Config.WalletUsd:F2}");
            Console.WriteLine($"WalletBtc: {trade.Config.WalletBtc:F5}");
            Console.WriteLine($"Total {totalUsd:F2} USD");
            Console.WriteLine($"Profit {profit:F2} USD");

            return profit;
        }

        private static void Trade_OnStateChanged(object sender, EventArgs e)
        {
            var trade = (Trade)sender;
            Console.WriteLine($"State: {trade.State}; Instant profit: {trade.InstantProfitUsd:F2} USD");
        }
    }
}
