namespace NeedleBot.Interfaces
{
    public interface IOrderResult
    {
        decimal PriceUsd { get; set; }
        decimal VolumeUsd { get; set; }
        decimal VolumeBtc { get; set; }
        decimal WalletBtc { get; set; }
        decimal WalletUsd { get; set; }
        bool IsOrderSet { get; set; }
    }
}
