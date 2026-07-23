namespace EcoMyceliumTracker.Domain;

/// <summary>
/// The criteria for narrowing a transfer listing.
/// </summary>
/// <remarks>
/// Lives in Domain, which has no dependencies of its own, because both the
/// endpoint and the repository refer to it. Application cannot host it: the
/// services depend on the repositories, so a repository reaching back into
/// Application would close a cycle.
/// </remarks>
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
