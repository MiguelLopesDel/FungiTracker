namespace EcoMyceliumTracker.Models;

public class NutrientTransfers
{
    public long Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public int CarbonAmountMg { get; set; }
    public DateTime TransferredAt { get; set; }
}