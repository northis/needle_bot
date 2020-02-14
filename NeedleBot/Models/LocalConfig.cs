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
            WalletUsd = 0;
            DetectDuration = TimeSpan.FromMinutes(1);
            OneDirectionSpeedDuration = TimeSpan.FromMinutes(1);
            DownRatio = 3;
            TradeVolumeUsd = 100;
            Mode = ModeEnum.BTC;
            ExchangeFeePercent = 0.2;
            ZeroProfitPriceUsd = 0;
            StopUsd = 70;
            SpeedBufferLength = 2;
            SpeedActivateValue = 6;
        }
        public double DownRatio { get; set; }
        public double WalletBtc { get; set; }
        public double WalletUsd { get; set; }
        public double TradeVolumeUsd { get; set; }
        public double ZeroProfitPriceUsd { get; set; }
        public ModeEnum Mode { get; set; }
        public double ExchangeFeePercent { get; set; }
        public TimeSpan DetectDuration { get; set; }
        public TimeSpan OneDirectionSpeedDuration { get; set; }
        public double StopUsd { get; set; }
        public int SpeedBufferLength { get; set; }
        public double SpeedActivateValue { get; set; }

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
