using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeedleBot.Models;

namespace NeedleBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Run().ConfigureAwait(false).GetAwaiter().GetResult();

            //AnalyzeReport();
            Console.ReadLine();
        }

        private const string REPORT_FILE = "report.txt";

        static void AnalyzeReport()
        {
            if (!File.Exists(REPORT_FILE))
            {
                Console.WriteLine($"No report file {REPORT_FILE}");
                return;
            }

            var lines = File.ReadAllLines(REPORT_FILE);

            var formatList = new List<Tuple<string, double, int>>();
            foreach (var line in lines)
            {
                var split = line.Split(new[] {"\t"}, StringSplitOptions.RemoveEmptyEntries);
                var key = split[0] + split[1];
                var month = int.Parse(split[2]);
                var value = double.Parse(split[3]);
                formatList.Add(new Tuple<string, double, int>(key, value, month));
            }

            var groupedValues = formatList.GroupBy(a => a.Item3);

            var sortedValues = groupedValues
                .Select(a => new Tuple<int, string[]>(a.Key,
                    a.OrderByDescending(c => c.Item2).Take(10).Select(b => $"{b.Item1}: {b.Item2:F2}").ToArray()));
            
            Console.WriteLine("Top 10");
            foreach (var sortedValue in sortedValues)
            {
                Console.WriteLine($"Month: {sortedValue.Item1}");
                foreach (var topItem in sortedValue.Item2)
                {
                    Console.WriteLine($"\t{topItem}");
                }
            }
        }

        static async Task Run()
        {
            var oldTitle = Console.Title;
            //Console.SetOut(TextWriter.Null);

            var maxDetectPriceChangeUsd = 200D;
            var maxStopPriceAllowanceUsd = 200;
            var months = 11;
            var total = maxDetectPriceChangeUsd * maxStopPriceAllowanceUsd * months / 100;
            var current = 0;

            for (var i = 1; i < 12; i++)
            {
                var history = new History($"2019{i:00}.json");

                var startDate = new DateTime(2019, i, 1, 0, 0, 0);
                var endDate = new DateTime(2019, i + 1, 1, 0, 0, 0);
                await history.LoadPrices(startDate, endDate, TimeSpan.FromDays(1));
            }

            var analysis = new ConcurrentBag<Tuple<int, int, int, double, int, int>>();

            for (var i = 0; i < maxDetectPriceChangeUsd; i+=10)
            {
                for (var j = 0; j < maxStopPriceAllowanceUsd; j+=10)
                {
                    var i1 = i;
                    var j1 = j;

                    Parallel.For(1, months, m => {
                        var history = new History($"2019{m:00}.json");
                        var trade = new Trade(new LocalConfig
                            { DetectPriceChangeUsd = i1, StopPriceAllowanceUsd = j1 });

                        var profit = TradeTask(history, trade, true).Result;
                        analysis.Add(new Tuple<int, int, int, double, int, int>(
                            i1, j1, m, profit, trade.SellCount, trade.BuyCount));

                        Interlocked.Increment(ref current);
                        Console.Title = $"Calculating... {99 * current / total:F1}%";
                    });
                }
            }

            Console.Title = oldTitle;

            var file = File.CreateText(REPORT_FILE);
            foreach (var analysisItem in analysis.OrderByDescending(a=>a.Item4))
            {
                await file.WriteLineAsync(
                    $"{analysisItem.Item1}\t{analysisItem.Item2}\t{analysisItem.Item3}\t{analysisItem.Item4:F2}\t{analysisItem.Item5}\t{analysisItem.Item6}");
            }
        }

        static async Task<double> TradeTask(History history, Trade trade, bool autoSetZeroPrice = false)
        {
            var initialAssetsUsd =
                Math.Round(trade.Config.WalletUsd + trade.Config.WalletBtc * trade.Config.ZeroProfitPriceUsd, 2);

            Console.WriteLine($"We have {initialAssetsUsd} USD");

            //trade.OnStateChanged += Trade_OnStateChanged;
            
            var prices = history.GetPrices();

            if (autoSetZeroPrice)
            {
                trade.Config.ZeroProfitPriceUsd = prices.First().Price + 100;
            }

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
            Console.WriteLine($"Instant profit: {trade.InstantProfitUsd:F2} USD");
        }
    }
}
