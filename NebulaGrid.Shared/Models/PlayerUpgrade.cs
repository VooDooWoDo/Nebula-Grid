namespace NebulaGrid.Shared.Models;

public class PlayerUpgrade
{
    public int PlayerID { get; set; }
    public string UpgradeType { get; set; } = string.Empty;
    public int Level { get; set; }

    public Player? Player { get; set; }
}
