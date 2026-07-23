using EcoMyceliumTracker.Domain;

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
    public Coordinates SourceLocation { get; set; }
    public Coordinates TargetLocation { get; set; }

    public static NutrientTransferDetails From(
        NutrientTransfer transfer,
        Domain.TransferSensor source,
        Domain.TransferSensor target) => new()
        {
            Id = transfer.Id,
            SourceNodeId = transfer.SourceNodeId,
            TargetNodeId = transfer.TargetNodeId,
            CarbonAmountMg = transfer.CarbonAmountMg,
            TransferredAt = transfer.TransferredAt,
            SourceLocation = source.Location,
            TargetLocation = target.Location,
        };
}
