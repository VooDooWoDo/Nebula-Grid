namespace NebulaGrid.Shared.Models;

public class FarmPlot
{
    public int PlotID { get; set; }
    public int PlayerID { get; set; }
    public string PlantName { get; set; } = string.Empty;
    public DateTime FinishTime { get; set; }
    public bool IsPlanted { get; set; }
    public int ResourceYield { get; set; }

    public Player? Player { get; set; }
}
