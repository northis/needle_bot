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
        public double Speed { get; private set; }
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

        private double _startPriceUsd;
        private double _stopPriceUsd;
        private DateTimeOffset _startSpeedDate;
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

        private const double MINIMAL_PROFIT = 0.1;

        private TimeSpan PriceDirectionDuration => _currentPriceItem.Date - _startSpeedDate;

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
            var period = _currentPriceItem.Date - first.Date;

            if (period != TimeSpan.Zero)
            {
                var oldSpeed = Speed;
                Speed = (_currentPriceItem.Price - first.Price) / period.TotalMinutes;

                if (Speed * oldSpeed < 0)
                {
                    _startSpeedDate = _currentPriceItem.Date;
                }
            }
            if (Math.Abs(Speed) > Config.SpeedActivateValue)
            {
                Logger.WriteExtra($"{_currentPriceItem.Date}; Speed {Speed:F2} USD/min",
                    ConsoleColor.White);
            }
        }

        private void LaunchTheRocket()
        {
            State = StateEnum.UP_SPEED;
            _stopPriceUsd = SpeedBuffer.Peek().Price;

            Logger.WriteExtra($"{_currentPriceItem.Date} enter the rocket, countdown ({_currentPriceItem.Price:F2}, speed {Speed:F2})");
        }

        private void DiveTheSubmarine()
        {
            State = StateEnum.DOWN_SPEED;
            _stopPriceUsd = SpeedBuffer.Peek().Price;

            Logger.WriteExtra(
                $"{_currentPriceItem.Date} get to the submarine, countdown ({_currentPriceItem.Price:F2}, speed {Speed:F2})");
        }

        private void ProcessSpeed()
        {
            if (Mode == ModeEnum.BTC && Speed > Config.SpeedActivateValue)
            {
                LaunchTheRocket();
            }
            else if (Mode == ModeEnum.USD && 
                     Speed < -Config.SpeedActivateValue * Config.DownRatio &&
                     PriceDirectionDuration > Config.OneDirectionSpeedDuration)
            {
                DiveTheSubmarine();
            }
        }

        private async Task FixProfitUp()
        {
            Logger.WriteMain(
                $"{_currentPriceItem.Date} fix the profit ${InstantProfitUsd:F2} USD ({_currentPriceItem.Price:F2} USD)",
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
                Logger.WriteMain($"{_currentPriceItem.Date} buy BTC for ${_currentPriceItem.Price:F2} USD", ConsoleColor.Red);
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
                var end = priceItem.Date.AddMinutes(-1);
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
                    _startDate = priceItem.Date;
                    State = StateEnum.INIT;
                    return;

                case StateEnum.BTC_DEF:
                case StateEnum.USD_DEF:
                case StateEnum.INIT:

                    SetDefaultState();
                    ProcessSpeed();

                    _startPriceUsd = priceItem.Price;
                    _startDate = priceItem.Date;

                    return;

                case StateEnum.UP_SPEED:
                    var dateUp = _currentPriceItem.Date;
                    var priceUp = _currentPriceItem.Price;
                    var canFixProfit = InstantProfitUsd > MINIMAL_PROFIT;

                    if (priceUp <= _stopPriceUsd && !canFixProfit)
                    {
                        Logger.WriteExtra(
                            $"{dateUp} ok, cancel the launch, price ${priceUp:F2} is lower than {_stopPriceUsd:F2} and we haven't profit enough");
                        SetDefaultState();
                        return;
                    }

                    if (_stopPriceUsd < priceUp - Config.StopUsd)
                    {
                        _stopPriceUsd = priceUp - Config.StopUsd;
                        return;
                    }

                    if (priceUp > _stopPriceUsd)
                    {
                        return;
                    }
                    var diffUp = priceUp - _startPriceUsd;

                    Logger.WriteExtra(
                        $"{dateUp} SELL: {_startPriceUsd:F2}->{priceUp:F2} ({diffUp:F2})\t{InstantProfitUsd:F2}$\t({_startDate})",
                        ConsoleColor.DarkGreen);

                    await FixProfitUp().ConfigureAwait(false);
                    return;

                case StateEnum.DOWN_SPEED:
                    var priceDown = _currentPriceItem.Price;
                    var dateDown = _currentPriceItem.Date;

                    //if (Speed > 0)
                    //{
                    //    Logger.WriteExtra(
                    //        $"{dateDown} ok, cancel the dive, price ${priceDown:F2} is higher than {Config.ZeroProfitPriceUsd:F2}");
                    //    SetDefaultState();
                    //    return;
                    //}

                    if (_stopPriceUsd > priceDown + Config.StopUsd)
                    {
                        _stopPriceUsd = priceDown + Config.StopUsd;
                        return;
                    }

                    if (priceDown < _stopPriceUsd)
                    {
                        return;
                    }

                    var diffDown = priceDown - _startPriceUsd;

                    Logger.WriteExtra(
                        $"{dateDown} BUY: {_startPriceUsd:F2}->{priceDown:F2} ({diffDown:F2})\t({_startDate})",
                        ConsoleColor.DarkRed);

                    await EnterToBtc().ConfigureAwait(false);
                    return;
            }
        }

        private void CheckTheBalance()
        {
            if (Config.TradeVolumeUsd - Config.WalletUsd > 0.1)
            {
                throw new Exception("Where is the money, Lebowski?");
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
            else
            {
                if (_currentPriceItem != null && priceItem.Date - _currentPriceItem.Date < Config.DetectDuration)
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