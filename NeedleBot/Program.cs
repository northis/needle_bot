﻿using System;
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
            ProcessHistoryBatch();
            //ProcessHistorySingle().ConfigureAwait(false).GetAwaiter().GetResult();
            // AnalyzeReport();
            Console.ReadLine();
        }

        private const string REPORT_FILE = "report.txt";

        static async Task ProcessHistorySingle()
        {
            var history = new History("201912.json");
            var trade =
                new Trade(new LocalConfig());
            
            await TradeTask(history, trade, true);
        }
        
        static void ProcessHistoryBatch()
        {
            Console.SetOut(TextWriter.Null);

            var maxDetectPriceChangeUsd = 30D;
            var total = maxDetectPriceChangeUsd;
            var current = 0;

            var history = new History("201912.json");
            //var startDate = new DateTime(2020, 1, 4, 18, 0, 0);
            //var endDate = new DateTime(2020, 1, 5, 0, 0, 0);
            //await history.LoadPrices(startDate, endDate, TimeSpan.FromDays(1)).ConfigureAwait(false);

            var analysis = new ConcurrentBag<Tuple<double, double, int, int>>();
            var tasks = new List<Task>();

            async Task FuncStopPrice(double priceChange)
            {
                var trade = new Trade(new LocalConfig
                {
                    DetectPriceChangeUsd = priceChange
                });

                var profit = await TradeTask(history, trade, true);
                analysis.Add(new Tuple<double, double, int, int>(priceChange, profit, trade.SellCount, trade.BuyCount));

                Interlocked.Increment(ref current);
                Console.Title = $"Calculating... {100 * current / total:F1}%";
            }

            for (var i = 0D; i < maxDetectPriceChangeUsd; i += 1)
            {
                var iLocal = i;
                var task = Task.Run(() => FuncStopPrice(iLocal));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            using (var file = File.CreateText(REPORT_FILE))
            {
                foreach (var analysisItem in analysis.OrderByDescending(a => a.Item2))
                {
                    file.WriteLine(
                        $"{analysisItem.Item1:F1}\t{analysisItem.Item2:F1}\t{analysisItem.Item3:F2}\t{analysisItem.Item4}");
                }
            }
            Console.Title = "Done";
        }

        static async Task<double> TradeTask(History history, Trade trade, bool autoSetZeroPrice = false)
        {
            //trade.OnStateChanged += Trade_OnStateChanged;
            var startDate = new DateTime(2019, 10, 1);

            var prices = history.GetPrices();//.SkipWhile(a => a.Date < startDate).ToArray();
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
