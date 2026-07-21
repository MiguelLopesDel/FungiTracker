using EcoMyceliumTracker.Models;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.Contracts;

/// <summary>
/// Query string of the transfer listing, bound as a single parameter.
/// </summary>
/// <remarks>
/// The defaults sit on constructor parameters rather than on property
/// initializers. [AsParameters] assigns every property from the query string,
/// so an absent value overwrote the initializer with default(int), page became
/// 0 and the listing answered 400 whenever paging was not stated explicitly.
/// Bound through the constructor, an absent value keeps the default.
/// </remarks>
public sealed record TransferListQuery(
    int Page = 1,
    int PageSize = RequestValidators.DefaultPageSize,
    int? MinimumCarbonMg = null,
    Guid? SourceNodeId = null,
    Guid? TargetNodeId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null)
{
    public TransferFilter ToFilter() => new()
    {
        MinimumCarbonMg = MinimumCarbonMg,
        SourceNodeId = SourceNodeId,
        TargetNodeId = TargetNodeId,
        From = From,
        To = To,
    };
}
