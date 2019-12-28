using NeedleBot.Interfaces;

namespace NeedleBot.Models
{
    public class OrderResult : IOrderResult
    {
        public double PriceUsd { get; set; }
        public double VolumeUsd { get; set; }
        public double VolumeBtc { get; set; }
        public double WalletBtc { get; set; }
        public double WalletUsd { get; set; }
    }
}
