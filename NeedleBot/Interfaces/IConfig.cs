using System;
using System.Threading.Tasks;
using NeedleBot.Dto;
using NeedleBot.Enums;

namespace NeedleBot.Interfaces
{
    public interface IConfig
    {
        TimeSpan DetectDuration { get; set; }
        TimeSpan AverageDuration { get; set; }
        public double BollingerBandsD { get; set; }
        public double StopUsd { get; set; }
        public double OrderStopMarginPercent { get; set; }
        public double ThresholdSpeedMin { get; set; }
        public int AvgBufferLength { get; set; }
        double WalletBtc { get; set; }
        double WalletUsd { get; set; }
        double TradeVolumeUsd { get; set; }
        double ZeroProfitPriceUsd { get; set; }
        ModeEnum Mode { get; set; }
        double ExchangeFeePercent { get; set; }
        Task<IOrderResult> SellBtc(double priceUsd, double volumeBtc);
        Task<IOrderResult> BuyBtc(double price, double volumeUsd);
        Task<PriceItem[]> GetHistory(DateTimeOffset start, DateTimeOffset end);
    }
}
