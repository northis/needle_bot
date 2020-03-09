using System;
using System.Threading.Tasks;
using NeedleBot.Dto;
using NeedleBot.Enums;

namespace NeedleBot.Interfaces
{
    public interface IConfig
    {
        TimeSpan DetectDuration { get; set; }
        public double DownRatio { get; set; }
        public int SpeedBufferLength { get; set; }
        double WalletBtc { get; set; }
        double WalletUsd { get; set; }
        double TradeVolumeUsd { get; set; }
        double ZeroProfitPriceUsd { get; set; }
        ModeEnum Mode { get; set; }
        double ExchangeFeePercent { get; set; }
        public double SpeedActivateValue { get; set; }
        Task<IOrderResult> SellBtc(double priceUsd, double volumeBtc);
        Task<IOrderResult> BuyBtc(double price, double volumeUsd);
        Task<PriceItem[]> GetHistory(DateTimeOffset start, DateTimeOffset end);
    }
}
