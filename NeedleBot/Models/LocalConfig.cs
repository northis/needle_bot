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
            DetectPriceChangeUsd = 2.5;
            WalletUsd = 101;
            TradeVolumeUsd = 100;
            Mode = ModeEnum.USD;
            ExchangeFeePercent = 0.2;
            ZeroProfitPriceUsd = 0;
        }

        public TimeSpan DetectDuration { get; set; }
        public double DetectPriceChangeUsd { get; set; }
        public double WalletBtc { get; set; }
        public double WalletUsd { get; set; }
        public double TradeVolumeUsd { get; set; }
        public double ZeroProfitPriceUsd { get; set; }
        public ModeEnum Mode { get; set; }
        public double ExchangeFeePercent { get; set; }

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
                   WalletUsd = walletUsd,
                   IsOrderSet = true
               };
           });
        }

        public async Task<IOrderResult> BuyBtc(double price, double volumeUsd)
        {
            return await Task.Run(() =>
            {
                var volumeBtc = volumeUsd / price;
                var walletBtc = WalletBtc + volumeBtc;
                var walletUsd = WalletUsd - volumeUsd - volumeUsd * ExchangeFeePercent / 100;
                
                if (walletUsd < 0)
                {

                }

                return new OrderResult
                {
                    PriceUsd = price, 
                    VolumeBtc = volumeBtc, 
                    VolumeUsd = volumeUsd, 
                    WalletBtc = walletBtc,
                    WalletUsd = walletUsd,
                    IsOrderSet = true
                };
            });
        }
    }
}
