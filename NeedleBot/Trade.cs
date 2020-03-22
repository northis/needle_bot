using System;
using System.Collections.Generic;
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

        public decimal InstantProfitUsd
        {
            get
            {
                if (Config.ZeroProfitPriceUsd > 0 && _currentPriceItem != null)
                {
                    if (Mode == ModeEnum.BTC)
                    {
                        var profit = Config.WalletBtc * (_currentPriceItem.Price - Config.ZeroProfitPriceUsd);
                        return profit - FromExchangeFee(Config.TradeVolumeUsd);
                    }
                    else
                    {
                        var profit = Config.ZeroProfitPriceUsd -_currentPriceItem.Price;
                        return profit - FromExchangeFee(Config.TradeVolumeUsd);
                    }
                }

                return 0;
            }
        }

        public decimal Speed { get; private set; }
        public ModeEnum Mode { get; set; }
        public int SellCount { get; private set; }
        public int BuyCount { get; private set; }
        public Queue<PriceItem> SpeedBuffer { get; }

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

        private decimal _startPriceUsd;
        private decimal _stopPriceUsd;
        private PriceItem _currentPriceItem;
        private DateTimeOffset _startDate;
        private StateEnum _state;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        public event EventHandler OnStateChanged;

        public Trade(IConfig config)
        {
            Config = config;
            State = StateEnum.INIT;
            Mode = config.Mode;

            SpeedBuffer = new Queue<PriceItem>();
        }

        private const decimal MINIMAL_PROFIT = 0.1M;

        private void SetDefaultState()
        {
            State = Mode == ModeEnum.BTC ? StateEnum.BTC_DEF : StateEnum.USD_DEF;
        }

        private void SetPrice(PriceItem priceItem)
        {
            _currentPriceItem = priceItem;

            if (SpeedBuffer.Count >= Config.SpeedBufferLength)
            {
                SpeedBuffer.Dequeue();
            }

            SpeedBuffer.Enqueue(_currentPriceItem);

            var first = SpeedBuffer.Peek();
            var period = _currentPriceItem.DateTime - first.DateTime;

            if (period != TimeSpan.Zero)
            {
                Speed = (_currentPriceItem.Price - first.Price) / (decimal) period.TotalMinutes;
            }
            if (Math.Abs(Speed) > Config.SpeedActivateValue)
            {
                Logger.WriteExtra($"{_currentPriceItem.DateTime}; Speed {Speed:F2} USD/min",
                    ConsoleColor.White);
            }
        }

        private void LaunchTheRocket()
        {
            State = StateEnum.UP_SPEED;
            _stopPriceUsd = SpeedBuffer.Peek().Price;

            Logger.WriteExtra($"{_currentPriceItem.DateTime} enter the rocket, countdown ({_currentPriceItem.Price:F2}, speed {Speed:F2})");
        }

        private void DiveTheSubmarine()
        {
            State = StateEnum.DOWN_SPEED;
            _stopPriceUsd = SpeedBuffer.Peek().Price;

            Logger.WriteExtra(
                $"{_currentPriceItem.DateTime} get to the submarine, countdown ({_currentPriceItem.Price:F2}, speed {Speed:F2})");
        }

        private void ProcessSpeed()
        {
            if (Mode == ModeEnum.BTC && Speed > Config.SpeedActivateValue)
            {
                LaunchTheRocket();
            }
            else if (Mode == ModeEnum.USD && 
                     Speed < -Config.SpeedActivateValue * Config.DownRatio)
            {
                DiveTheSubmarine();
            }
        }

        private async Task FixProfitUp()
        {
            Logger.WriteMain(
                $"{_currentPriceItem.DateTime} fix the profit ${InstantProfitUsd:F2} USD ({_currentPriceItem.Price:F2} USD)",
                ConsoleColor.Green);

            var res = await Config.SellBtc(_currentPriceItem.Price, Config.WalletBtc)
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

            SetDefaultState();
        }
        
        private async Task EnterToBtc()
        {
            CheckTheBalance();

            var isMoneyEnough = Config.WalletUsd > Config.TradeVolumeUsd + FromExchangeFee(Config.TradeVolumeUsd);

            if (isMoneyEnough)
            {
                Logger.WriteMain($"{_currentPriceItem.DateTime} buy BTC for ${_currentPriceItem.Price:F2} USD", ConsoleColor.Red);
                var res = await Config.BuyBtc(_currentPriceItem.Price, Config.TradeVolumeUsd)
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

                LaunchTheRocket();
                return;
            }

            SetDefaultState();
        }

        private async Task DecideInner(PriceItem priceItem)
        {
            if (State == StateEnum.INIT)
            {
                var end = priceItem.DateTime.AddMinutes(-1);
                var avgSumDuration = TimeSpan.FromMinutes(
                    Config.DetectDuration.TotalMinutes * Config.SpeedBufferLength);

                var history = await Config.GetHistory(end.Add(-avgSumDuration), end);

                foreach (var historyItem in history)
                {
                    SetPrice(historyItem);
                }
            }
            SetPrice(priceItem);

            switch (State)
            {
                case StateEnum.NO_DATA:
                case StateEnum.ERROR:
                    _startPriceUsd = priceItem.Price;
                    _startDate = priceItem.DateTime;
                    State = StateEnum.INIT;
                    return;

                case StateEnum.BTC_DEF:
                case StateEnum.USD_DEF:
                case StateEnum.INIT:

                    SetDefaultState();
                    ProcessSpeed();

                    _startPriceUsd = priceItem.Price;
                    _startDate = priceItem.DateTime;

                    return;

                case StateEnum.UP_SPEED:
                    var dateUp = _currentPriceItem.DateTime;
                    var priceUp = _currentPriceItem.Price;
                    var canFixProfit = InstantProfitUsd > MINIMAL_PROFIT;

                    if (priceUp <= _startPriceUsd && !canFixProfit)
                    {
                        Logger.WriteExtra(
                            $"{dateUp} ok, cancel the launch, price ${priceUp:F2} is lower than {_stopPriceUsd:F2} and we haven't profit enough");
                        SetDefaultState();
                        return;
                    }

                    if (Speed < 0 && canFixProfit)
                    {
                        var diffUp = priceUp - _startPriceUsd;

                        Logger.WriteExtra(
                            $"{dateUp} SELL: {_startPriceUsd:F2}->{priceUp:F2} ({diffUp:F2})\t{InstantProfitUsd:F2}$\t({_startDate})",
                            ConsoleColor.DarkGreen);

                        await FixProfitUp().ConfigureAwait(false);
                    }
                    return;

                case StateEnum.DOWN_SPEED:
                    var priceDown = _currentPriceItem.Price;
                    var dateDown = _currentPriceItem.DateTime;
                    canFixProfit = //InstantProfitUsd > MINIMAL_PROFIT || 
                                   Config.ZeroProfitPriceUsd < 1;

                    if (priceDown  >= _startPriceUsd && !canFixProfit)
                    {
                        Logger.WriteExtra(
                            $"{dateDown} ok, cancel the dive, price ${priceDown:F2} is higher than {_startPriceUsd:F2} and we haven't profit enough");
                        SetDefaultState();
                        return;
                    }

                    if (Speed > 0)
                    {
                        var diffDown = priceDown - _startPriceUsd;

                        Logger.WriteExtra(
                            $"{dateDown} BUY: {_startPriceUsd:F2}->{priceDown:F2} ({diffDown:F2})\t({_startDate})",
                            ConsoleColor.DarkRed);

                        await EnterToBtc().ConfigureAwait(false);
                    }
                    return;
            }
        }

        private void CheckTheBalance()
        {
            if (Config.TradeVolumeUsd - Config.WalletUsd > 0.1M)
            {
                throw new Exception("Where is the money, Lebowski?");
            }
        }

        public async Task Decide(PriceItem priceItem)
        {
            if (priceItem.DateTime == default)
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
                _startDate = priceItem.DateTime;
            }
            else
            {
                if (_currentPriceItem != null && priceItem.DateTime - _currentPriceItem.DateTime < Config.DetectDuration)
                    return;
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

        private decimal FromPercent(decimal percent, decimal mainValue)
        {
            return mainValue * percent / 100;
        }

        private decimal FromExchangeFee(decimal mainValue)
        {
            return FromPercent(Config.ExchangeFeePercent, mainValue);
        }
    }
}