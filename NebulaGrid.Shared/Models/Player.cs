using System.ComponentModel.DataAnnotations.Schema;

namespace NebulaGrid.Shared.Models;

public class Player
{
    public int PlayerID { get; set; }
    public int AccountProfileID { get; set; }
    public int CharacterSlot { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string PilotClass { get; set; } = string.Empty;
    public int Resource1 { get; set; }
    public int Resource2 { get; set; }
    public int Resource3 { get; set; }
    public int Resource4 { get; set; }
    public int XP { get; set; }
    public int Level { get; set; }
    public int? PlayerShip { get; set; }
    public string PassiveJobKey { get; set; } = string.Empty;
    public int MiningSkillLevel { get; set; }
    public int LogisticsSkillLevel { get; set; }
    public int ReactorSkillLevel { get; set; }
    public int MiningTreeLevel { get; set; }
    public int LogisticsTreeLevel { get; set; }
    public int ReactorTreeLevel { get; set; }
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;
    public AccountProfile? AccountProfile { get; set; }

    [NotMapped]
    public int OfflineCreditsGained { get; set; }

    [NotMapped]
    public int OfflineFuelGained { get; set; }

    [NotMapped]
    public int OfflinePlayerXpGained { get; set; }

    [NotMapped]
    public int OfflineReserveXpGained { get; set; }

    [NotMapped]
    public string? OfflineSummary { get; set; }
}