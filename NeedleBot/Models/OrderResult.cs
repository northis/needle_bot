using NeedleBot.Interfaces;

namespace NeedleBot.Models
{
    public class OrderResult : IOrderResult
    {
        public decimal PriceUsd { get; set; }
        public decimal VolumeUsd { get; set; }
        public decimal VolumeBtc { get; set; }
        public decimal WalletBtc { get; set; }
        public decimal WalletUsd { get; set; }
        public bool IsOrderSet { get; set; }
    }
}
