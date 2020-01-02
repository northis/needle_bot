namespace NeedleBot.Interfaces
{
    public interface IOrderResult
    {
        double PriceUsd { get; set; }
        double VolumeUsd { get; set; }
        double VolumeBtc { get; set; }
        double WalletBtc { get; set; }
        double WalletUsd { get; set; }
        bool IsOrderSet { get; set; }
    }
}
