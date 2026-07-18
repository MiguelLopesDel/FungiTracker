namespace EcoMyceliumTracker.Models;

public class MyceliumNetwork
{
    public Guid Id { get; set; }
    public string ScientificName { get; set; } = string.Empty;
    public string SoilType { get; set; } = string.Empty;
    public DateTime DiscoveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}