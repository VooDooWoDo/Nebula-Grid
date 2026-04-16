namespace NebulaGrid.Shared.Models;

public class IdleGameState
{
    public int Id { get; set; }
    public long Currency { get; set; }
    public int IncomePerSecond { get; set; } = 1;
    public int ClickPower { get; set; } = 1;
    public int UpgradeLevel { get; set; }
    public int ClickUpgradeLevel { get; set; }
    public long TotalClicks { get; set; }
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
}
