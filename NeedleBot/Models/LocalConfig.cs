using System;
using System.Threading.Tasks;
using NeedleBot.Enums;
using NeedleBot.Interfaces;

namespace NeedleBot.Models
{
    public class LocalConfig : IConfig
    {
        public LocalConfig()
        {
            DetectDuration = TimeSpan.FromMinutes(1);
            DetectPriceChangePercent = 0.1;
            
            WalletUsd = 0;
            TradeVolumeUsd = 100;
            ZeroProfitPriceUsd = 7200;
            Mode = ModeEnum.BTC;
            DealAllowanceUsd = 0;
            ExchangeFeePercent = 0.2;
            StopPriceAllowanceUsd = 25;

            WalletBtc = Math.Ceiling(1000 * TradeVolumeUsd / ZeroProfitPriceUsd) / 1000;
        }

        public TimeSpan DetectDuration { get; set; }
        public double DetectPriceChangePercent { get; set; }
        public double WalletBtc { get; set; }
        public double WalletUsd { get; set; }
        public double TradeVolumeUsd { get; set; }
        public double ZeroProfitPriceUsd { get; set; }
        public DateTime StartDate { get; set; }
        public ModeEnum Mode { get; set; }
        public double ExchangeFeePercent { get; set; }
        public double DealAllowanceUsd { get; set; }
        public double StopPriceAllowanceUsd { get; set; }

        public async Task<IOrderResult> SellBtc(double priceUsd, double volumeBtc)
        {
           return await Task.Run(() =>
           {
               var volumeUsd = volumeBtc * priceUsd;
               var walletBtc = WalletBtc - volumeBtc;
               var walletUsd = WalletUsd + volumeUsd - volumeUsd * ExchangeFeePercent / 100;

               return new OrderResult
               {
                   PriceUsd = priceUsd, 
                   VolumeBtc = volumeBtc, 
                   VolumeUsd = volumeUsd, 
                   WalletBtc = walletBtc,
                   WalletUsd = walletUsd
               };
           });
        }

        public async Task<IOrderResult> BuyBtc(double price, double volumeUsd)
        {
            return await Task.Run(() =>
            {
                var volumeBtc = volumeUsd / price;
                var walletBtc = WalletBtc + volumeBtc - volumeBtc * ExchangeFeePercent / 100;
                var walletUsd = WalletUsd - volumeUsd;

                return new OrderResult
                {
                    PriceUsd = price, 
                    VolumeBtc = volumeBtc, 
                    VolumeUsd = volumeUsd, 
                    WalletBtc = walletBtc,
                    WalletUsd = walletUsd
                };
            });
        }
    }
}
