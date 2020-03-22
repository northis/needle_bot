using System;
using System.Threading.Tasks;
using NeedleBot.Dto;
using NeedleBot.Enums;
using NeedleBot.Interfaces;

namespace NeedleBot.Models
{
    public class LocalConfig : IConfig
    {
        private readonly History _history;

        public LocalConfig(History history)
        {
            _history = history;
            WalletUsd = 101;
            DetectDuration = TimeSpan.FromHours(1);
            DownRatio = 3;
            TradeVolumeUsd = 100;
            Mode = ModeEnum.USD;
            ExchangeFeePercent = 0.2M;
            ZeroProfitPriceUsd = 0;
            SpeedBufferLength = 2;
            SpeedActivateValue = 2;
        }
        public decimal DownRatio { get; set; }
        public decimal WalletBtc { get; set; }
        public decimal WalletUsd { get; set; }
        public decimal TradeVolumeUsd { get; set; }
        public decimal ZeroProfitPriceUsd { get; set; }
        public ModeEnum Mode { get; set; }
        public decimal ExchangeFeePercent { get; set; }
        public TimeSpan DetectDuration { get; set; }
        public int SpeedBufferLength { get; set; }
        public decimal SpeedActivateValue { get; set; }

        public async Task<IOrderResult> SellBtc(decimal priceUsd, decimal volumeBtc)
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

        public async Task<IOrderResult> BuyBtc(decimal price, decimal volumeUsd)
        {
            return await Task.Run(() =>
            {
                var volumeBtc = volumeUsd / price;
                var walletBtc = WalletBtc + volumeBtc;
                var walletUsd = WalletUsd - volumeUsd - volumeUsd * ExchangeFeePercent / 100;

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

        public async Task<PriceItem[]> GetHistory(DateTimeOffset start, DateTimeOffset end)
        {
            await _history.LoadPrices(start, end).ConfigureAwait(false);
            return _history.GetPrices();
        }
    }
}
