namespace EcoMyceliumTracker.Models;

public sealed class SensorNode
{
    public Guid Id { get; set; }
    public Guid NetworkId { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal MoistureLevel { get; set; }
    public bool IsActive { get; set; }
}
