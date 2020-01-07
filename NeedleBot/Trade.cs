using System;
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
        public double ThresholdSpeedSec { get; }
        public double InstantProfitUsd { get; set; }
        public double AvgPrice { get; private set; }
        public ModeEnum Mode { get; set; }
        public int SellCount { get; private set; }
        public int BuyCount { get; private set; }
        public double BollingerBandsD { get; set; } = 19;
        public double StopPercent { get; set; } = 3.4;

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
        private double _yDiffSum;
        private double _sigma;
        private double _n;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        public event EventHandler OnStateChanged;

        public Trade(IConfig config, PriceItem[] history = null)
        {
            Config = config;
            State = StateEnum.INIT;
            Mode = config.Mode;
            ThresholdSpeedSec = Config.DetectPriceChangeUsd / Config.DetectDuration.TotalSeconds;

            if (history == null) 
                return;

            foreach (var historyItem in history.OrderBy(a => a.Date))
                SetAveragePrice(historyItem.Price);
        }

        private double Stop(double price)
        {
            return price * StopPercent / 100;
        }

        private void SetDefaultState()
        {
            State = Mode == ModeEnum.BTC ? StateEnum.BTC_DEF : StateEnum.USD_DEF;
        }

        private void SetAveragePrice(double price)
        {
            if (AvgPrice > 0)
                AvgPrice = (AvgPrice + price) / 2;
            else
                AvgPrice = price;
            //prev prices to buffer - n
            _n++;
            var diff = price - AvgPrice;
            var diffSqu = diff * diff;
            _sigma = BollingerBandsD * Math.Sqrt((_yDiffSum + diffSqu) / _n);
            _yDiffSum += diffSqu;
        }

        private void ProcessSpeed(double price, DateTimeOffset dateTime)
        {
            if (Mode == ModeEnum.BTC && price > ThresholdSpeedSec)
            {
                State = StateEnum.UP_SPEED;
                _stopPriceUsd = price - Stop(price);

                Logger.WriteExtra($"{dateTime} enter the rocket, countdown");
            }
            else if (Mode == ModeEnum.USD && price < AvgPrice - _sigma)
            {
                State = StateEnum.DOWN_SPEED;
                _stopPriceUsd = price + Stop(price);
                Logger.WriteExtra($"{dateTime} get to the submarine, countdown");
            }
        }

        private async Task FixProfitUp(double price, DateTimeOffset dateTime)
        {
            if (InstantProfitUsd > 0)
            {
                Logger.WriteMain($"{dateTime} fix the profit ${InstantProfitUsd:F2} USD", ConsoleColor.Green);

                var res = await Config.SellBtc(price, Config.WalletBtc)
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
        
        private async Task EnterToBtc(double price, DateTimeOffset dateTime)
        {
            if (Config.TradeVolumeUsd > Config.WalletUsd)
            {
                throw new Exception("Where is the money, Lebowski?");
            }

            var isMoneyEnough = Config.WalletUsd > Config.TradeVolumeUsd + FromExchangeFee(Config.TradeVolumeUsd);

            if (isMoneyEnough)
            {
                Logger.WriteMain($"{dateTime} buy BTC for ${price:F2} USD", ConsoleColor.Red);
                var res = await Config.BuyBtc(price, Config.TradeVolumeUsd)
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

        private bool SetTrailUp(double price)
        {
            if (price > _stopPriceUsd + Stop(price))
            {
                _stopPriceUsd = price;
                return true;
            } 
            
            if (price > _stopPriceUsd - Stop(price))
            {
                return true;
            }

            return false;
        }

        private bool SetTrailDown(double price)
        {
            if (price < _stopPriceUsd - Stop(price) || _stopPriceUsd <= 0)
            {
                _stopPriceUsd = price;
                return true;
            }

            if (price < _stopPriceUsd + Stop(price))
            {
                return true;
            }

            return false;
        }

        private async Task DecideInner(double price, DateTimeOffset dateTime)
        {
            SetAveragePrice(price);
            if (Mode == ModeEnum.BTC && Config.ZeroProfitPriceUsd > 0)
            {
                var profit = Config.WalletBtc * (price - Config.ZeroProfitPriceUsd);
                InstantProfitUsd = profit - FromExchangeFee(Config.TradeVolumeUsd);
            }

            switch (State)
            {
                case StateEnum.NO_DATA:
                case StateEnum.ERROR:
                    _startPriceUsd = price;
                    _startDate = dateTime;
                    State = StateEnum.INIT;
                    return;

                case StateEnum.BTC_DEF:
                case StateEnum.USD_DEF:
                case StateEnum.INIT:

                    SetDefaultState();
                    ProcessSpeed(price, dateTime);

                    _startPriceUsd = price;
                    _startDate = dateTime;

                    return;

                case StateEnum.UP_SPEED:
                    if (price < _startPriceUsd - _sigma)
                    {
                        Logger.WriteExtra(
                            $"{dateTime} ok, cancel the launch, price ${price:F2} is lower than {_startPriceUsd:F2}");
                        SetDefaultState();
                        return;
                    }

                    if (!SetTrailUp(price))
                    {
                        var diff = _stopPriceUsd - _startPriceUsd;

                        Logger.WriteExtra($"{dateTime} SELL: {_startPriceUsd:F2}->{_stopPriceUsd:F2} ({diff:F2})\t{InstantProfitUsd:F2}$\t({_startDate}->{dateTime})", ConsoleColor.DarkGreen);

                        await FixProfitUp(price, dateTime).ConfigureAwait(false);
                    }

                    return;

                case StateEnum.DOWN_SPEED:
                    if (price > _startPriceUsd + _sigma)
                    {
                        Logger.WriteExtra($"{dateTime} ok, cancel the dive, price ${price:F2} is higher than {_startPriceUsd:F2}");
                        SetDefaultState();
                        return;
                    }

                    var gotTrail = !SetTrailDown(price);
                    if (gotTrail)
                    {

                        var diff = _stopPriceUsd - _startPriceUsd;

                        Logger.WriteExtra($"{dateTime} BUY: {_startPriceUsd:F2}->{_stopPriceUsd:F2} ({diff:F2})\t({_startDate}->{dateTime})", ConsoleColor.DarkRed);

                        await EnterToBtc(price, dateTime).ConfigureAwait(false);
                    }

                    return;
            }
        }

        public async Task Decide(double price, DateTimeOffset dateTime)
        {
            if (dateTime == default)
            {
                Logger.WriteMain("dateTime isn't set");
                State = StateEnum.ERROR;
                return;
            }

            if (price <= 0)
            {
                Logger.WriteMain("price isn't positive");
                State = StateEnum.ERROR;
                return;
            }

            if (_startPriceUsd <= 0 || _startDate == default)
            {
                Logger.WriteMain("setting the integration values");
                _startPriceUsd = price;
                _startDate = dateTime;
            }

            await _semaphoreSlim.WaitAsync();
            try
            {
                await DecideInner(price, dateTime);
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