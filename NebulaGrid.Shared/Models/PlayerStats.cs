namespace NebulaGrid.Shared.Models;

public class PlayerStats
{
    public int PlayerID { get; set; }
    public int TotalClicks { get; set; }
    public int TotalResourcesEarned { get; set; }

    public Player? Player { get; set; }
}
