using EcoMyceliumTracker.Contracts;
using EcoMyceliumTracker.Domain;

namespace EcoMyceliumTracker.Validation;

/// <summary>
/// The checked fields of a network, trimmed and non-null.
/// </summary>
public sealed record NetworkFields(string ScientificName, string SoilType, DateTimeOffset DiscoveredAt);

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
        ParseNetwork(request.ScientificName, request.SoilType, request.DiscoveredAt, now).Errors;

    public static Dictionary<string, string[]> Validate(
        UpdateMyceliumNetworkRequest request,
        DateTimeOffset now) =>
        ParseNetwork(request.ScientificName, request.SoilType, request.DiscoveredAt, now).Errors;

    public static Dictionary<string, string[]> Validate(CreateSensorNodeRequest request) =>
        ParseSensor(request).Errors;

    public static Dictionary<string, string[]> Validate(UpdateSensorNodeRequest request) =>
        ParseSensor(request.Location, request.MoistureLevel).Errors;

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

    /// <summary>
    /// Checks the criteria of a transfer listing.
    /// </summary>
    /// <remarks>
    /// Here rather than on TransferFilter itself: the keys are query string
    /// parameter names, which is knowledge of the HTTP boundary that a domain
    /// type has no business carrying.
    /// </remarks>
    public static Dictionary<string, string[]> Validate(TransferFilter filter)
    {
        var errors = new Dictionary<string, string[]>();

        if (filter.MinimumCarbonMg < 0)
        {
            errors["minimumCarbonMg"] = ["O valor mínimo de carbono não pode ser negativo."];
        }

        if (filter.From > filter.To)
        {
            errors["from"] = ["A data inicial deve ser anterior à data final."];
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

    /// <summary>
    /// Checks a network and hands back its values, so the caller can build the
    /// model without asserting a second time that they are present.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, NetworkFields Fields) ParseNetwork(
        string? scientificName,
        string? soilType,
        DateTimeOffset discoveredAt,
        DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();

        var name = RequiredText(
            scientificName,
            nameof(scientificName),
            MaximumScientificNameLength,
            "O nome científico é obrigatório.",
            $"O nome científico deve ter no máximo {MaximumScientificNameLength} caracteres.",
            errors);

        var soil = RequiredText(
            soilType,
            nameof(soilType),
            MaximumSoilTypeLength,
            "O tipo de solo é obrigatório.",
            $"O tipo de solo deve ter no máximo {MaximumSoilTypeLength} caracteres.",
            errors);

        if (discoveredAt == default)
        {
            errors[nameof(discoveredAt)] = ["A data de descoberta é obrigatória."];
        }
        else if (discoveredAt > now)
        {
            errors[nameof(discoveredAt)] = ["A data de descoberta não pode estar no futuro."];
        }

        return (errors, new NetworkFields(name, soil, discoveredAt.ToUniversalTime()));
    }

    /// <summary>
    /// Checks one text field and hands back the trimmed value.
    /// </summary>
    /// <remarks>
    /// Returning the value is what lets the caller build its model without
    /// asserting non-null a second time: when the field is missing this
    /// records the error and returns an empty string, which is never read
    /// because the caller throws as soon as the dictionary is not empty.
    /// </remarks>
    private static string RequiredText(
        string? value,
        string field,
        int maximumLength,
        string missingMessage,
        string tooLongMessage,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [missingMessage];
            return string.Empty;
        }

        if (value.Length > maximumLength)
        {
            errors[field] = [tooLongMessage];
        }

        return value.Trim();
    }

    /// <summary>
    /// Checks a new sensor in one pass, so a request with both a bad location
    /// and a missing network reports both.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, Coordinates Location) ParseSensor(
        CreateSensorNodeRequest request)
    {
        var (errors, location) = ParseSensor(request.Location, request.MoistureLevel);
        if (request.NetworkId == Guid.Empty)
        {
            errors[nameof(request.NetworkId)] = ["O identificador da rede é obrigatório."];
        }

        return (errors, location);
    }

    /// <summary>
    /// Checks a sensor and hands back its location, trimmed and non-null.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, Coordinates Location) ParseSensor(
        string? location,
        decimal moistureLevel)
    {
        var errors = new Dictionary<string, string[]>();

        // The parsed value is kept: this is the only place the text is read.
        if (!CoordinatesParser.TryParse(location, out var parsed))
        {
            errors[nameof(location)] = ["A localização deve usar o formato 'x,y', com ponto como separador decimal."];
        }

        if (moistureLevel < MinimumMoistureLevel || moistureLevel > MaximumMoistureLevel)
        {
            errors[nameof(moistureLevel)] = [$"O nível de umidade deve estar entre {MinimumMoistureLevel} e {MaximumMoistureLevel}."];
        }

        return (errors, parsed);
    }
}
