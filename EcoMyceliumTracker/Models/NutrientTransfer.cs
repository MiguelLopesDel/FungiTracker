namespace EcoMyceliumTracker.Models;

public class NutrientTransfer
{
    public long Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public int CarbonAmountMg { get; set; }
    public DateTimeOffset TransferredAt { get; set; }
}
