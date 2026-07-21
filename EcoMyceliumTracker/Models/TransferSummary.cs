namespace EcoMyceliumTracker.Models;

public class TransferSummary
{
    public long Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public int CarbonAmountMg { get; set; }
    public DateTimeOffset TransferredAt { get; set; }
    public string SourceLocation { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
}
