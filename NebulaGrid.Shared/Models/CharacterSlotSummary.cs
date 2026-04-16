namespace NebulaGrid.Shared.Models;

public class CharacterSlotSummary
{
    public int SlotId { get; set; }
    public bool IsUnlocked { get; set; }
    public int UnlockLevelRequirement { get; set; }
    public string UnlockText { get; set; } = string.Empty;
    public Player? Player { get; set; }
}
