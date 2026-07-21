namespace EcoMyceliumTracker.Contracts;

public sealed record CreateMyceliumNetworkRequest(
    string? ScientificName,
    string? SoilType,
    DateTimeOffset DiscoveredAt);

public sealed record UpdateMyceliumNetworkRequest(
    string? ScientificName,
    string? SoilType,
    DateTimeOffset DiscoveredAt);

public sealed record CreateSensorNodeRequest(
    Guid NetworkId,
    string? Location,
    decimal MoistureLevel,
    bool IsActive);

public sealed record UpdateSensorNodeRequest(
    string? Location,
    decimal MoistureLevel,
    bool IsActive);

public sealed record SetSensorStatusRequest(bool IsActive);

public sealed record CreateNutrientTransferRequest(
    Guid SourceNodeId,
    Guid TargetNodeId,
    int CarbonAmountMg,
    DateTimeOffset? TransferredAt = null);
