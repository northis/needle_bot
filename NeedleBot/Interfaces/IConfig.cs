using System;
using System.Threading.Tasks;
using NeedleBot.Dto;
using NeedleBot.Enums;

namespace NeedleBot.Interfaces
{
    public interface IConfig
    {
        TimeSpan DetectDuration { get; set; }
        public decimal DownRatio { get; set; }
        public int SpeedBufferLength { get; set; }
        decimal WalletBtc { get; set; }
        decimal WalletUsd { get; set; }
        decimal TradeVolumeUsd { get; set; }
        decimal ZeroProfitPriceUsd { get; set; }
        ModeEnum Mode { get; set; }
        decimal ExchangeFeePercent { get; set; }
        public decimal SpeedActivateValue { get; set; }
        Task<IOrderResult> SellBtc(decimal priceUsd, decimal volumeBtc);
        Task<IOrderResult> BuyBtc(decimal price, decimal volumeUsd);
        Task<PriceItem[]> GetHistory(DateTimeOffset start, DateTimeOffset end);
    }
}
