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
    /// Field-level problems with the criteria themselves. These are invariants
    /// of the filter, so they travel with it rather than with any one caller.
    /// </summary>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (MinimumCarbonMg < 0)
        {
            // Keys match the query string parameter names, not the property names.
            errors["minimumCarbonMg"] = ["O valor mínimo de carbono não pode ser negativo."];
        }

        if (From > To)
        {
            errors["from"] = ["A data inicial deve ser anterior à data final."];
        }

        return errors;
    }

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
