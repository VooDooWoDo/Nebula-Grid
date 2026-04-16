using System.ComponentModel.DataAnnotations.Schema;

namespace NebulaGrid.Shared.Models;

public class AccountProfile
{
    public int AccountProfileID { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public int AccountLevel { get; set; } = 1;

    [NotMapped]
    public int AccountXP { get; set; }

    [NotMapped]
    public int AccountXPToNextLevel { get; set; } = 150;

    [NotMapped]
    public int CombinedPilotLevels { get; set; }

    [NotMapped]
    public int CombinedCommanderLevels { get; set; }

    [NotMapped]
    public int UnlockedSlotCount { get; set; } = 1;

    [NotMapped]
    public int CreditBonusPercent { get; set; }

    [NotMapped]
    public int FuelBonusFlat { get; set; }

    [NotMapped]
    public string MilestoneTitle { get; set; } = "First Fleet Goal";

    [NotMapped]
    public string MilestoneProgressText { get; set; } = "Reach a combined pilot level of 10.";

    [NotMapped]
    public int MilestoneProgressValue { get; set; }

    [NotMapped]
    public int MilestoneTargetValue { get; set; } = 10;
}
