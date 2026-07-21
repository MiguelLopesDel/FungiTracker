namespace EcoMyceliumTracker.Models;

/// <summary>
/// A stored transfer plus the location of each sensor, which is how every
/// transfer endpoint represents one. Named after NutrientTransfer, the row it
/// reads from, so the pair is recognisable as write model and read model.
/// </summary>
public sealed class NutrientTransferDetails
{
    public long Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public int CarbonAmountMg { get; set; }
    public DateTimeOffset TransferredAt { get; set; }
    public string SourceLocation { get; set; } = string.Empty;
    public string TargetLocation { get; set; } = string.Empty;
}
