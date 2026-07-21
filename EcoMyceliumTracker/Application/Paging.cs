using EcoMyceliumTracker.Domain;
using EcoMyceliumTracker.Validation;

namespace EcoMyceliumTracker.Application;

/// <summary>
/// Paging is not the concern of any one aggregate, so the check that every
/// listing performs lives here rather than on one of the services.
/// </summary>
internal static class Paging
{
    public static void EnsureValid(int page, int pageSize)
    {
        var errors = RequestValidators.ValidatePagination(page, pageSize);
        if (errors.Count > 0)
        {
            throw DomainException.InvalidRequest(errors);
        }
    }
}
