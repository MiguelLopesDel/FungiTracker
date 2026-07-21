namespace EcoMyceliumTracker.Contracts;

/// <summary>
/// The criteria for narrowing a transfer listing. Grouping them gives the
/// concept a name and keeps the same five values from being threaded
/// one-by-one through the endpoint, the service and the repository.
/// </summary>
public sealed record TransferFilter
{
    public int? MinimumCarbonMg { get; init; }
    public Guid? SourceNodeId { get; init; }
    public Guid? TargetNodeId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// The same filter with both instants moved to UTC, which is how they are
    /// stored.
    /// </summary>
    public TransferFilter ToUniversalTime() => this with
    {
        From = From?.ToUniversalTime(),
        To = To?.ToUniversalTime(),
    };
}

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
