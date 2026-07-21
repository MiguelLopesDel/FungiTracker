namespace EcoMyceliumTracker.Models;

public sealed class MyceliumNetwork
{
    public Guid Id { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string SoilType { get; set; } = string.Empty;
    public DateTimeOffset DiscoveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
