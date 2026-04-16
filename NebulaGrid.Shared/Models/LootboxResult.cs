namespace NebulaGrid.Shared.Models;

public class LootboxResult
{
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int NewResource1 { get; set; }
    public int NewResource2 { get; set; }
    public int NewXP { get; set; }
    public int NewLevel { get; set; }
}