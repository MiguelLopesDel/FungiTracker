namespace EcoMyceliumTracker.Models;

/// <summary>
/// How a transfer is represented in responses: the stored row plus the two
/// sensor locations. The same shape is used by the listing and by the single
/// transfer endpoints, so a client never has to handle two variants.
/// </summary>
public sealed class TransferView
{
    public long Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public int CarbonAmountMg { get; set; }
    public DateTimeOffset TransferredAt { get; set; }
    public string SourceLocation { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
}
