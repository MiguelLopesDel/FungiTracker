using EcoMyceliumTracker.Domain;

namespace EcoMyceliumTracker.Models;

public sealed class SensorNode
{
    public Guid Id { get; set; }
    public Guid NetworkId { get; set; }
    public Coordinates Location { get; set; }
    public decimal MoistureLevel { get; set; }
    public bool IsActive { get; set; }
}
