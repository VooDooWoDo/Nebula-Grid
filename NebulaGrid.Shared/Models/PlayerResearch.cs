namespace NebulaGrid.Shared.Models;

public class PlayerResearch
{
    public int PlayerID { get; set; }
    public string ResearchType { get; set; } = string.Empty;
    public int Level { get; set; }

    public Player? Player { get; set; }
}