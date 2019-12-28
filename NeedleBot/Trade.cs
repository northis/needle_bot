using System;
using System.Threading.Tasks;
using NeedleBot.Enums;
using NeedleBot.Interfaces;

namespace NeedleBot
{
    public class Trade
    {
        public IConfig Config { get; }
        public double ThresholdSpeedSec { get; }
        public double InstantProfitUsd { get; set; }

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

        public ModeEnum Mode { get; set; }

        private double _startPriceUsd;
        private double _stopPriceUsd;

        private DateTime _startDate;
        private StateEnum _state;

        public event EventHandler OnStateChanged;

        public Trade(IConfig config)
        {
            Config = config;
            ThresholdSpeedSec = Config.DetectPriceChangePercent / Config.DetectDuration.TotalSeconds;
            State = StateEnum.INIT;
            Mode = ModeEnum.BTC;
        }

        private void SetDefaultState()
        {
            State = Mode == ModeEnum.BTC ? StateEnum.BTC_DEF : StateEnum.USD_DEF;
        }

        private bool ProcessSpeed(double speed)
        {
            var isInSpeed = false;
            if (Mode == ModeEnum.BTC && speed > ThresholdSpeedSec)
            {
                State = StateEnum.UP_SPEED;
                //Console.WriteLine("enter the rocket, countdown");
                isInSpeed = true;
            }
            else if (Mode == ModeEnum.USD && -1 * speed > ThresholdSpeedSec)
            {
                State = StateEnum.DOWN_SPEED;
                //Console.WriteLine("get to the submarine, countdown");
                isInSpeed = true;
            }

            return isInSpeed;
        }

        private double GetSpeed(double priceEnd, DateTime dateEnd)
        {
            var duration = (dateEnd - _startDate).TotalSeconds;
            if (duration > 0)
            {
                var diff = priceEnd - _startPriceUsd;
                return diff / duration;
            }

            return 0D;
        }

        private async Task FixProfitUp(double price)
        {
            if (InstantProfitUsd > 0)
            {
                Console.WriteLine($"fix the profit ${InstantProfitUsd} USD");

                var res = await Config.SellBtc(price, Config.WalletBtc)
                    .ConfigureAwait(false);

                Config.WalletUsd = res.WalletUsd;
                Config.WalletBtc = res.WalletBtc;
                Mode = ModeEnum.USD;
            }

            SetDefaultState();
        }
        
        private async Task EnterToBtc(double price)
        {
            if (Config.TradeVolumeUsd > Config.WalletUsd)
            {
                throw new Exception("Where is the money, Lebowski?");
            }

            if (price > _stopPriceUsd)
            {

                Console.WriteLine($"buy BTC for ${_stopPriceUsd} USD");
                var res = await Config.BuyBtc(
                        _stopPriceUsd, Config.TradeVolumeUsd)
                    .ConfigureAwait(false);

                Config.WalletUsd = res.WalletUsd;
                Config.WalletBtc = res.WalletBtc;
                Config.ZeroProfitPriceUsd = res.PriceUsd;
                State = StateEnum.DOWN_TRAIL_BUY;
                Mode = ModeEnum.BTC;
            }

            SetDefaultState();
        }

        private bool SetTrailUp(double price)
        {
            if (price > _stopPriceUsd + Config.StopPriceAllowanceUsd)
            {
                _stopPriceUsd = price;
                State = StateEnum.UP_TRAIL_SET;
                return false;
            } 
            
            if (price > _stopPriceUsd - Config.StopPriceAllowanceUsd)
            {
                return false;
            }

            return true;
        }

        private bool SetTrailDown(double price)
        {
            if (price < _stopPriceUsd - Config.StopPriceAllowanceUsd)
            {
                _stopPriceUsd = price;
                State = StateEnum.DOWN_TRAIL_SET;
                return false;
            }

            if (price < _stopPriceUsd + Config.StopPriceAllowanceUsd)
            {
                return false;
            }

            return true;
        }

        private async Task DecideInner(double price, DateTime dateTime)
        {
            if (Mode == ModeEnum.BTC && Config.ZeroProfitPriceUsd > 0)
            {
                var profit = Config.WalletBtc * (price - Config.ZeroProfitPriceUsd);
                InstantProfitUsd = profit - FromExchangeFee(profit);
            }

            switch (State)
            {
                case StateEnum.NO_DATA:
                case StateEnum.ERROR:
                    _startPriceUsd = price;
                    _startDate = dateTime;
                    State = StateEnum.INIT;
                    return;

                case StateEnum.UP_TRAIL_SELL:
                case StateEnum.DOWN_TRAIL_BUY:
                    Console.WriteLine(
                        $"{dateTime}: still no answer from trade company, price = ${price}");
                    return;

                case StateEnum.BTC_DEF:
                case StateEnum.USD_DEF:
                case StateEnum.INIT:
                    if (dateTime - _startDate >= Config.DetectDuration)
                    {
                        SetDefaultState();
                        var speed = GetSpeed(price, dateTime);

                        var isInSpeed = ProcessSpeed(speed);
                        if (isInSpeed)
                        {
                            _startPriceUsd = price;
                        }
                        _startDate = dateTime;
                    }

                    return;

                case StateEnum.UP_SPEED:
                    SetTrailUp(price);
                    return;

                case StateEnum.UP_TRAIL_SET:
                    if (price < _startPriceUsd - Config.StopPriceAllowanceUsd)
                    {
                        //Console.WriteLine($"ok, cancel the launch, price ${price} is lower than {_startPriceUsd}");
                        SetDefaultState();
                        return;
                    }

                    if (SetTrailUp(price))
                    {
                        Console.WriteLine($"Fix datetime is {dateTime}");
                        await FixProfitUp(price).ConfigureAwait(false);
                    }

                    return;

                case StateEnum.DOWN_SPEED:
                    SetTrailDown(price);
                    return;

                case StateEnum.DOWN_TRAIL_SET:
                    if (price > _startPriceUsd + Config.StopPriceAllowanceUsd)
                    {
                        //Console.WriteLine($"ok, cancel the dive, price ${price} is higher than {_startPriceUsd}");
                        SetDefaultState();
                        return;
                    }

                    if (SetTrailDown(price))
                    {
                        Console.WriteLine($"Enter to BTC datetime is {dateTime}");
                        await EnterToBtc(price).ConfigureAwait(false);;
                    }

                    return;
            }
        }

        public async Task Decide(double price, DateTime dateTime)
        {
            if (dateTime == default)
            {
                Console.WriteLine("dateTime isn't set");
                State = StateEnum.ERROR;
                return;
            }

            if (price <= 0)
            {
                Console.WriteLine("price isn't positive");
                State = StateEnum.ERROR;
                return;
            }

            if (_startPriceUsd <= 0 || _startDate == default)
            {
                Console.WriteLine("setting the integration values");
                _startPriceUsd = price;
                _startDate = dateTime;
            }

            await DecideInner(price, dateTime);
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