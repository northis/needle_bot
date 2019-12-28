using System;
using System.Threading.Tasks;
using NeedleBot.Enums;

namespace NeedleBot.Interfaces
{
    public interface IConfig
    {
        TimeSpan DetectDuration { get; set; }
        double DetectPriceChangePercent { get; set; }
        double WalletBtc { get; set; }
        double WalletUsd { get; set; }
        double TradeVolumeUsd { get; set; }
        double ZeroProfitPriceUsd { get; set; }
        DateTime StartDate { get; set; }
        ModeEnum Mode { get; set; }
        double ExchangeFeePercent { get; set; }
        double DealAllowanceUsd { get; set; }
        double StopPriceAllowanceUsd { get; set; }
        Task<IOrderResult> SellBtc(double priceUsd, double volumeBtc);
        Task<IOrderResult> BuyBtc(double price, double volumeUsd);
    }
}
