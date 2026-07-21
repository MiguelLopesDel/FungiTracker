using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.Contracts;

/// <summary>
/// Query string of the transfer listing, bound as a single parameter.
/// </summary>
public sealed record TransferListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = Validation.RequestValidators.DefaultPageSize;
    public int? MinimumCarbonMg { get; init; }
    public Guid? SourceNodeId { get; init; }
    public Guid? TargetNodeId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    public TransferFilter ToFilter() => new()
    {
        MinimumCarbonMg = MinimumCarbonMg,
        SourceNodeId = SourceNodeId,
        TargetNodeId = TargetNodeId,
        From = From,
        To = To,
    };
}
