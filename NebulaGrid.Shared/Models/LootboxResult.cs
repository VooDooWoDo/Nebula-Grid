namespace NebulaGrid.Shared.Models;

public class LootboxResult
{
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int NewResource1 { get; set; }
    public int NewResource2 { get; set; }
    public int NewResource3 { get; set; }
    public int NewResource4 { get; set; }
    public int NewPilotXP { get; set; }
    public int NewPilotLevel { get; set; }
}