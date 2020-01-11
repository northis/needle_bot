using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeedleBot.Dto;
using NeedleBot.Enums;
using NeedleBot.Interfaces;

namespace NeedleBot
{
    public class Trade
    {
        public IConfig Config { get; }

        public double InstantProfitUsd
        {
            get
            {
                if (Mode == ModeEnum.BTC && Config.ZeroProfitPriceUsd > 0)
                {
                    var profit = Config.WalletBtc * (_stopPriceUsd - Config.ZeroProfitPriceUsd);
                    return profit - FromExchangeFee(Config.TradeVolumeUsd);
                }

                return 0;
            }
        }

        public double AvgPrice { get; private set; }
        public ModeEnum Mode { get; set; }
        public int SellCount { get; private set; }
        public int BuyCount { get; private set; }
        public Queue<double> AvgBuffer { get; }
        public Queue<PriceItem> AvgCandleBuffer { get; }

        public StateEnum State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private double _startPriceUsd;
        private double _stopPriceUsd;
        private DateTimeOffset _startDate;
        private StateEnum _state;
        private double _sigma;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        public event EventHandler OnStateChanged;

        public Trade(IConfig config)
        {
            Config = config;
            State = StateEnum.INIT;
            Mode = config.Mode;

            AvgBuffer = new Queue<double>();
            AvgCandleBuffer = new Queue<PriceItem>();
        }

        private void SetDefaultState()
        {
            State = Mode == ModeEnum.BTC ? StateEnum.BTC_DEF : StateEnum.USD_DEF;
        }

        private void SetAveragePrice(PriceItem priceItem)
        {
            if (AvgBuffer.Count > 0)
                AvgPrice = AvgBuffer.Average();
            else if (AvgCandleBuffer.Count > 0)
                AvgPrice = AvgCandleBuffer.Average(a => a.Price);
            else
                AvgPrice = priceItem.Price;

            if (AvgCandleBuffer.Count == 0 || priceItem.Date - AvgCandleBuffer.Peek().Date < Config.AverageDuration)
            {
                AvgCandleBuffer.Enqueue(priceItem);
            }
            else
            {
                AvgBuffer.Enqueue(AvgCandleBuffer.Average(a=>a.Price));
                if (AvgBuffer.Count > Config.AvgBufferLength)
                {
                    AvgBuffer.Dequeue();
                }
                AvgCandleBuffer.Clear();
            }

            if (AvgBuffer.Count > 0)
            {
                var diffSquSum =  AvgBuffer.Sum(a => (a - AvgPrice) * (a - AvgPrice));
                _sigma = Config.BollingerBandsD * Math.Sqrt(diffSquSum / AvgBuffer.Count);
            }
            else
            {
                var diffSquSum = AvgCandleBuffer.Sum(a => (a.Price - AvgPrice) * (a.Price - AvgPrice));
                _sigma = Config.BollingerBandsD * Math.Sqrt(diffSquSum / AvgCandleBuffer.Count);
            }
        }

        private double GetSpeed(PriceItem priceItem)
        {
            var duration = (priceItem.Date - _startDate).TotalMinutes;
            if (duration > 0)
            {
                var diff = priceItem.Price - _startPriceUsd;
                return diff / duration;
            }

            return 0;
        }

        private void ProcessSpeed(PriceItem priceItem)
        {
            if (Mode == ModeEnum.BTC && priceItem.Price > AvgPrice + _sigma)
            {
                State = StateEnum.UP_SPEED;
                _stopPriceUsd = priceItem.Price - Config.StopUsd;

                Logger.WriteExtra($"{priceItem.Date} enter the rocket, countdown ({priceItem.Price:F2})");
            }
            else if (Mode == ModeEnum.USD && priceItem.Price < AvgPrice - _sigma)
            {
                State = StateEnum.DOWN_SPEED;
                _stopPriceUsd = priceItem.Price + Config.StopUsd;
                Logger.WriteExtra($"{priceItem.Date} get to the submarine, countdown ({priceItem.Price:F2})");
            }
        }

        private async Task FixProfitUp(PriceItem priceItem)
        {
            if (InstantProfitUsd > 0)
            {
                Logger.WriteMain(
                    $"{priceItem.Date} fix the profit ${InstantProfitUsd:F2} USD ({priceItem.Price:F2} USD)",
                    ConsoleColor.Green);

                var res = await Config.SellBtc(_stopPriceUsd, Config.WalletBtc)
                    .ConfigureAwait(false);
                if (!res.IsOrderSet)
                {
                    SetDefaultState();
                    return;
                }

                Config.WalletUsd = res.WalletUsd;
                Config.WalletBtc = res.WalletBtc;
                Mode = ModeEnum.USD;
                SellCount++;
                Logger.WriteMain($"WalletUsd: {Config.WalletUsd:F2}; WalletBtc: {Config.WalletBtc:F5}; SellCount: {SellCount}\n");
            }

            SetDefaultState();
        }
        
        private async Task EnterToBtc(PriceItem priceItem)
        {
            if (Config.TradeVolumeUsd > Config.WalletUsd)
            {
                throw new Exception("Where is the money, Lebowski?");
            }

            var isMoneyEnough = Config.WalletUsd > Config.TradeVolumeUsd + FromExchangeFee(Config.TradeVolumeUsd);

            if (isMoneyEnough)
            {
                Logger.WriteMain($"{priceItem.Date} buy BTC for ${priceItem.Price:F2} USD", ConsoleColor.Red);
                var res = await Config.BuyBtc(_stopPriceUsd, Config.TradeVolumeUsd)
                    .ConfigureAwait(false);
                if (!res.IsOrderSet)
                {
                    SetDefaultState();
                    return;
                }

                Config.WalletUsd = res.WalletUsd;
                Config.WalletBtc = res.WalletBtc;
                Config.ZeroProfitPriceUsd = res.PriceUsd;
                Mode = ModeEnum.BTC;
                BuyCount++;

                Logger.WriteMain($"WalletUsd: {Config.WalletUsd:F2}; WalletBtc: {Config.WalletBtc:F5}; BuyCount: {BuyCount}\n");
            }

            SetDefaultState();
        }

        private bool UpdateTrailUp(double price)
        {
            if (price > _stopPriceUsd + Config.StopUsd)
            {
                _stopPriceUsd = price - Config.StopUsd;
                return true;
            }

            if (price > _stopPriceUsd)
            {
                return true;
            }

            return false;
        }

        private bool UpdateTrailDown(double price)
        {
            if (price < _stopPriceUsd - Config.StopUsd || _stopPriceUsd <= 0)
            {
                _stopPriceUsd = price + Config.StopUsd;
                return true;
            }

            if (price < _stopPriceUsd)
            {
                return true;
            }

            return false;
        }

        private async Task DecideInner(PriceItem priceItem)
        {
            SetAveragePrice(priceItem);

            if (State == StateEnum.INIT)
            {
                var end = priceItem.Date.AddMinutes(-1);
                var avgSumDuration = TimeSpan.FromMinutes(
                    Config.AverageDuration.TotalMinutes * Config.AvgBufferLength);

                var history = await Config.GetHistory(end.Add(-avgSumDuration), end);

                foreach (var historyItem in history)
                {
                    SetAveragePrice(historyItem);
                }
            }

            switch (State)
            {
                case StateEnum.NO_DATA:
                case StateEnum.ERROR:
                    _startPriceUsd = priceItem.Price;
                    _startDate = priceItem.Date;
                    State = StateEnum.INIT;
                    return;

                case StateEnum.BTC_DEF:
                case StateEnum.USD_DEF:
                case StateEnum.INIT:

                    if (priceItem.Date - _startDate >= Config.DetectDuration)
                    {
                        SetDefaultState();

                        ProcessSpeed(priceItem);

                        _startPriceUsd = priceItem.Price;
                        _startDate = priceItem.Date;
                    }

                    return;

                case StateEnum.UP_SPEED:
                    if (priceItem.Price < _startPriceUsd - _sigma)
                    {
                        Logger.WriteExtra(
                            $"{priceItem.Date} ok, cancel the launch, price ${priceItem.Price:F2} is lower than {_startPriceUsd:F2}");
                        SetDefaultState();
                        return;
                    }

                    if (!UpdateTrailUp(priceItem.Price))
                    {
                        var diff = _stopPriceUsd - _startPriceUsd;

                        Logger.WriteExtra($"{priceItem.Date} SELL: {_startPriceUsd:F2}->{_stopPriceUsd:F2} ({diff:F2})\t{InstantProfitUsd:F2}$\t({_startDate})", ConsoleColor.DarkGreen);

                        await FixProfitUp(priceItem).ConfigureAwait(false);
                    }

                    return;

                case StateEnum.DOWN_SPEED:
                    if (priceItem.Price > _startPriceUsd + _sigma)
                    {
                        Logger.WriteExtra($"{priceItem.Date} ok, cancel the dive, price ${priceItem.Price:F2} is higher than {_startPriceUsd:F2}");
                        SetDefaultState();
                        return;
                    }

                    var gotTrail = !UpdateTrailDown(priceItem.Price);
                    if (gotTrail)
                    {
                        var diff = _stopPriceUsd - _startPriceUsd;

                        Logger.WriteExtra($"{priceItem.Date} BUY: {_startPriceUsd:F2}->{_stopPriceUsd:F2} ({diff:F2})\t({_startDate})", ConsoleColor.DarkRed);

                        await EnterToBtc(priceItem).ConfigureAwait(false);
                    }

                    return;
            }
        }

        public async Task Decide(PriceItem priceItem)
        {
            if (priceItem.Date == default)
            {
                Logger.WriteMain("dateTime isn't set");
                State = StateEnum.ERROR;
                return;
            }

            if (priceItem.Price <= 0)
            {
                Logger.WriteMain("price isn't positive");
                State = StateEnum.ERROR;
                return;
            }

            if (_startPriceUsd <= 0 || _startDate == default)
            {
                Logger.WriteMain("setting the integration values");
                _startPriceUsd = priceItem.Price;
                _startDate = priceItem.Date;
            }

            await _semaphoreSlim.WaitAsync();
            try
            {
                await DecideInner(priceItem);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private double FromPercent(double percent, double mainValue)
        {
            return mainValue * percent / 100;
        }

        private double FromExchangeFee(double mainValue)
        {
            return FromPercent(Config.ExchangeFeePercent, mainValue);
        }
    }
}