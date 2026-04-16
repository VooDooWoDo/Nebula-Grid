namespace NebulaGrid.Shared.Models;

public class Ship
{
    public int ShipID { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int CargoCapacity { get; set; }
    public int EngineLevel { get; set; }

    public double TravelDurationSeconds => Math.Max(0.28, 1.55 - (EngineLevel * 0.4) + (CargoCapacity * 0.003));
}
