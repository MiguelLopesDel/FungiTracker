namespace EcoMyceliumTracker.Configuration;

public sealed class TransferOptions
{
    public const string SectionName = "Transfers";
    public int HighEnergyThresholdMg { get; set; } = 500;
}
