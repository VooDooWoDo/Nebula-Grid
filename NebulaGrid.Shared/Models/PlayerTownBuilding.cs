namespace NebulaGrid.Shared.Models;

public class PlayerTownBuilding
{
    public int PlayerID { get; set; }
    public string BuildingKey { get; set; } = string.Empty;
    public int Level { get; set; }

    public Player? Player { get; set; }
}