namespace NeedleBot.Interfaces
{
    public interface IOrderResult
    {
        double PriceUsd { get; }
        double VolumeUsd { get; }
        double VolumeBtc { get; }
        double WalletBtc { get; }
        double WalletUsd { get; }
    }
}
