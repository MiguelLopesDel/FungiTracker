using System.Diagnostics.CodeAnalysis;

namespace EcoMyceliumTracker.Domain;

/// <summary>
/// A sensor as seen while it is locked for a transfer.
/// </summary>
public sealed record TransferSensor(Guid Id, Guid NetworkId, bool IsActive, string Location);

/// <summary>
/// Whether a transfer between two sensors is allowed.
/// </summary>
/// <remarks>
/// The rule reads only the sensor state it is given, so it can be exercised
/// without a database even though the state it judges has to be read under a
/// lock. Deciding is separate from loading.
/// </remarks>
public static class TransferPolicy
{
    /// <summary>
    /// Throws unless the transfer is allowed.
    /// </summary>
    /// <remarks>
    /// The NotNull annotations let the caller use both sensors afterwards
    /// without asserting again: if this returns rather than throws, neither
    /// was null.
    /// </remarks>
    public static void EnsureAllowed(
        [NotNull] TransferSensor? source,
        [NotNull] TransferSensor? target)
    {
        if (source is null || target is null)
        {
            throw DomainException.NotFound(
                "O sensor de origem ou de destino não existe.",
                "transfer_sensor_not_found");
        }

        if (!source.IsActive || !target.IsActive)
        {
            throw DomainException.Validation(
                "Transferências só podem envolver sensores ativos.",
                "inactive_transfer_sensor");
        }

        if (source.NetworkId != target.NetworkId)
        {
            throw DomainException.Validation(
                "Os sensores de origem e destino devem pertencer à mesma rede.",
                "sensors_from_different_networks");
        }
    }
}
