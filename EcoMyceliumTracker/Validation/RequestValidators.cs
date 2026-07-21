using EcoMyceliumTracker.Contracts;

namespace EcoMyceliumTracker.Validation;

public static class RequestValidators
{
    public const int MaximumPageSize = 100;
    public const int DefaultPageSize = 20;

    // These bounds are also enforced by the database. Any change here has to be
    // matched by a migration, otherwise the same input starts failing as a
    // constraint violation (422) instead of a validation error (400).
    // See Infrastructure/Persistence/Migrations/001_initial_schema.sql.
    public const int MaximumScientificNameLength = 200;
    public const int MaximumSoilTypeLength = 100;
    public const decimal MinimumMoistureLevel = 0;
    public const decimal MaximumMoistureLevel = 100;

    public static Dictionary<string, string[]> Validate(
        CreateMyceliumNetworkRequest request,
        DateTimeOffset now) =>
        ValidateNetwork(request.ScientificName, request.SoilType, request.DiscoveredAt, now);

    public static Dictionary<string, string[]> Validate(
        UpdateMyceliumNetworkRequest request,
        DateTimeOffset now) =>
        ValidateNetwork(request.ScientificName, request.SoilType, request.DiscoveredAt, now);

    public static Dictionary<string, string[]> Validate(CreateSensorNodeRequest request)
    {
        var errors = ValidateSensor(request.Location, request.MoistureLevel);
        if (request.NetworkId == Guid.Empty)
        {
            errors[nameof(request.NetworkId)] = ["O identificador da rede é obrigatório."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(UpdateSensorNodeRequest request) =>
        ValidateSensor(request.Location, request.MoistureLevel);

    public static Dictionary<string, string[]> Validate(
        CreateNutrientTransferRequest request,
        DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.SourceNodeId == Guid.Empty)
        {
            errors[nameof(request.SourceNodeId)] = ["O sensor de origem é obrigatório."];
        }

        if (request.TargetNodeId == Guid.Empty)
        {
            errors[nameof(request.TargetNodeId)] = ["O sensor de destino é obrigatório."];
        }

        if (request.SourceNodeId == request.TargetNodeId && request.SourceNodeId != Guid.Empty)
        {
            errors[nameof(request.TargetNodeId)] = ["O sensor de destino deve ser diferente do sensor de origem."];
        }

        if (request.CarbonAmountMg <= 0)
        {
            errors[nameof(request.CarbonAmountMg)] = ["A quantidade de carbono deve ser maior que zero."];
        }

        if (request.TransferredAt > now)
        {
            errors[nameof(request.TransferredAt)] = ["A data da transferência não pode estar no futuro."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidatePagination(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors[nameof(page)] = ["A página deve ser maior que zero."];
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            errors[nameof(pageSize)] = [$"O tamanho da página deve estar entre 1 e {MaximumPageSize}."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateNetwork(
        string? scientificName,
        string? soilType,
        DateTimeOffset discoveredAt,
        DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(scientificName))
        {
            errors[nameof(scientificName)] = ["O nome científico é obrigatório."];
        }
        else if (scientificName.Length > MaximumScientificNameLength)
        {
            errors[nameof(scientificName)] = [$"O nome científico deve ter no máximo {MaximumScientificNameLength} caracteres."];
        }

        if (string.IsNullOrWhiteSpace(soilType))
        {
            errors[nameof(soilType)] = ["O tipo de solo é obrigatório."];
        }
        else if (soilType.Length > MaximumSoilTypeLength)
        {
            errors[nameof(soilType)] = [$"O tipo de solo deve ter no máximo {MaximumSoilTypeLength} caracteres."];
        }

        if (discoveredAt == default)
        {
            errors[nameof(discoveredAt)] = ["A data de descoberta é obrigatória."];
        }
        else if (discoveredAt > now)
        {
            errors[nameof(discoveredAt)] = ["A data de descoberta não pode estar no futuro."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateSensor(string? location, decimal moistureLevel)
    {
        var errors = new Dictionary<string, string[]>();

        if (!CoordinatesParser.TryParse(location, out _))
        {
            errors[nameof(location)] = ["A localização deve usar o formato 'x,y', com ponto como separador decimal."];
        }

        if (moistureLevel < MinimumMoistureLevel || moistureLevel > MaximumMoistureLevel)
        {
            errors[nameof(moistureLevel)] = [$"O nível de umidade deve estar entre {MinimumMoistureLevel} e {MaximumMoistureLevel}."];
        }

        return errors;
    }
}
