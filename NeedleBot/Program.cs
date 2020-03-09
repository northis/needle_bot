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
            //for (int i = 1; i <= 12; i++)
            //{
            //    ProcessHistoryBatch($"2019{i:D2}.json");
            //}
            //ProcessHistoryBatch("202002.json");
            //Logger.LogLevel = LogLevel.Debug;
            ProcessHistorySingle().ConfigureAwait(false).GetAwaiter().GetResult();
            // AnalyzeReport();
            Console.ReadLine();
        }
        
        static async Task ProcessHistorySingle()
        {
            var history = new History("20206m.json");
            var historyPre = new History("pre20206m.json", "data1m");
            var trade = new Trade(new LocalConfig(historyPre));
            //Logger.LogLevel = LogLevel.Extra;
            //var startDate = new DateTimeOffset(2019, 9, 8, 0, 0, 0, TimeSpan.Zero);
            //var endDate = new DateTimeOffset(2020, 3, 7, 0, 0, 0, TimeSpan.Zero);
            //await history.LoadPrices(startDate, endDate).ConfigureAwait(false);
            await TradeTask(history, trade, true);
        }
        
        static void ProcessHistoryBatch(string fileName)
        {
            Console.SetOut(TextWriter.Null);

            var speedActivateValue = 20D;
            var bufCount = 20;
            var total = speedActivateValue * (bufCount-1);
            var current = 0;

            var history = new History(fileName);
            var historyPre = new History("pre" + fileName);

            var analysis = new ConcurrentBag<Tuple<double, double, double, int, int>>();
            var tasks = new List<Task>();

            async Task FuncStopPrice(double i)
            {
                for (var j = 2; j <= bufCount; j++)
                {
                    var trade = new Trade(new LocalConfig(historyPre) {SpeedActivateValue = i, SpeedBufferLength = j});
                    double profit = 0;
                    try
                    {
                        profit = await TradeTask(history, trade, true);
                    }
                    catch
                    {
                        profit = -100500;
                    }
                    finally
                    {
                        analysis.Add(
                            new Tuple<double, double, double, int, int>(i, j, profit, trade.SellCount,
                                trade.BuyCount));
                        Interlocked.Increment(ref current);
                        Console.Title = $"Calculating... {100 * current / total:F1}%";
                    }
                }
            }

            for (var i = 1; i <= speedActivateValue; i++)
            {
                var iLocal = i;
                var task = Task.Run(() => FuncStopPrice(iLocal));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            using (var file = File.CreateText(fileName + ".txt"))
            {
                foreach (var analysisItem in analysis.OrderByDescending(a => a.Item2))
                {
                    file.WriteLine(
                        $"{analysisItem.Item1:F1}\t{analysisItem.Item2:F1}\t{analysisItem.Item3:F1}\t{analysisItem.Item4}\t{analysisItem.Item5}");
                }
            }
            Console.Title = "Done";
        }

        static async Task<double> TradeTask(History history, Trade trade, bool autoSetZeroPrice = false)
        {
            //trade.OnStateChanged += Trade_OnStateChanged;
            var prices = history.GetPrices();
            if (autoSetZeroPrice && trade.Config.Mode == ModeEnum.BTC)
            {
                trade.Config.ZeroProfitPriceUsd = prices.First().Price + 10;
                if (trade.Config.WalletBtc <= 0)
                {
                    trade.Config.WalletBtc = (trade.Config.TradeVolumeUsd + 1) / trade.Config.ZeroProfitPriceUsd;
                }
            }

            var initialAssetsUsd =
                Math.Round(trade.Config.WalletUsd + trade.Config.WalletBtc * trade.Config.ZeroProfitPriceUsd, 2);

            Console.WriteLine($"We have {initialAssetsUsd} USD");

            foreach (var priceItem in prices)
            {
                await trade.Decide(priceItem).ConfigureAwait(false);
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
