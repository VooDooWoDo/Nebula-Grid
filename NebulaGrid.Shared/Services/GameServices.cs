
using NebulaGrid.Shared.Models;
namespace NebulaGrid.Shared.Services;

public class ShipService
{
    public string CurrentShip { get; set; } = "default";
}

public class PlayerService
{
    public sealed record LevelUpNotification(string Scope, string Name, int NewLevel, int LevelsGained);

    public int AccountProfileId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int AccountLevel { get; set; } = 1;
    public int AccountXP { get; set; }
    public int AccountXPToNextLevel { get; set; } = 150;
    public int CombinedPilotLevels { get; set; }
    public int CombinedCommanderLevels { get; set; }
    public int AccountUnlockedSlotCount { get; set; } = 1;
    public int AccountCreditBonusPercent { get; set; }
    public int AccountFuelBonusFlat { get; set; }
    public string AccountMilestoneTitle { get; set; } = "First Fleet Goal";
    public string AccountMilestoneProgressText { get; set; } = "Reach a combined pilot level of 10.";
    public int AccountMilestoneProgressValue { get; set; }
    public int AccountMilestoneTargetValue { get; set; } = 10;
    public int SelectedPlayerId { get; set; } = 0;
    public string PlayerName { get; set; } = "Pilot";
    public string PilotClass { get; set; } = string.Empty;
    public string PassiveJobKey { get; set; } = string.Empty;
    public int PilotMiningSkill { get; set; }
    public int PilotLogisticsSkill { get; set; }
    public int PilotReactorSkill { get; set; }
    public int PilotMiningTreeLevel { get; set; }
    public int PilotLogisticsTreeLevel { get; set; }
    public int PilotReactorTreeLevel { get; set; }
    public string ActiveCommanderName { get; set; } = "No Commander";
    public string ActiveCommanderClass { get; set; } = "Unassigned";
    public string ActiveCommanderSubclass { get; set; } = "No Specialization";
    public string ActiveCommanderRarity { get; set; } = "Common";
    public string ActiveCommanderTraitName { get; set; } = "Steady Hands";
    public string ActiveCommanderTraitDescription { get; set; } = "Reliable under pressure.";
    public int ActiveCommanderLevel { get; set; } = 1;
    public int ActiveCommanderXP { get; set; }
    public int ActiveCommanderMiningSkill { get; set; }
    public int ActiveCommanderLogisticsSkill { get; set; }
    public int ActiveCommanderReactorSkill { get; set; }
    public int ActiveCommanderMiningTreeLevel { get; set; }
    public int ActiveCommanderLogisticsTreeLevel { get; set; }
    public int ActiveCommanderReactorTreeLevel { get; set; }
    public int CurrentLevel { get; set; } = 1;
    public int CurrentXP { get; set; } = 0;
    public int Resource1 { get; set; }
    public int Resource2 { get; set; }
    public int Resource3 { get; set; }
    public int Resource4 { get; set; }
    public int CurrentShipId { get; set; } = 1;
    public HashSet<int> OwnedShipIds { get; set; } = new() { 1 };
    public int ClickPowerLevel { get; set; } = 1;
    public int AutoKlickerLevel { get; set; } = 0;
    public int CritChanceLevel { get; set; } = 0;
    public int MiningResearchLevel { get; set; } = 0;
    public int LogisticsResearchLevel { get; set; } = 0;
    public int ReactorResearchLevel { get; set; } = 0;
    public int SurveyOfficeLevel { get; set; } = 0;
    public int FreightDepotLevel { get; set; } = 0;
    public int ReactorAnnexLevel { get; set; } = 0;

    public int CommanderMiningBonusPercent => GetPilotMiningBonusPercent();
    public int CommanderCargoBonus => GetPilotCargoBonus();
    public int CommanderCritBonus => GetPilotCritBonus();
    public int ProgressionLevel => CurrentLevel;

    public double CreditIncomeMultiplier => (1 + (MiningResearchLevel * 0.15)) * (1 + (CommanderMiningBonusPercent / 100.0)) * (1 + (AccountCreditBonusPercent / 100.0));
    public int BonusCargoCapacity => (LogisticsResearchLevel * 20) + CommanderCargoBonus;
    public int BonusCritChance => (ReactorResearchLevel * 2) + CommanderCritBonus;

    public int CreditsPerSecond => IsAreaUnlocked("game4")
        ? GetModifiedCreditGain((AutoKlickerLevel * Math.Max(1, ClickPowerLevel)) + GetClassCreditFlowBonus())
        : 0;
        public int FuelPerSecond => IsAreaUnlocked("game4")
            ? GetClassFuelFlowBonus()
            : 0;
    public int Alloy
    {
        get => Resource3;
        set => Resource3 = value;
    }

    public int Biomass
    {
        get => Resource4;
        set => Resource4 = value;
    }

    public int ReactorCritMultiplier => GetReactorCritMultiplier();
    public string ActiveClassRoleSummary => GetActiveClassRoleSummary();
    public string AccountProgressSummary => $"Account LVL {AccountLevel} • {AccountXP}/{AccountXPToNextLevel} Account XP";
    public string AccountBonusSummary => $"+{AccountCreditBonusPercent}% Credits • {AccountUnlockedSlotCount}/{3} Slots";
    public int PilotXPToNextLevel => Math.Max(100, CurrentLevel * 100);

    public string CommanderSummary => string.IsNullOrWhiteSpace(PilotClass) ? "Class Locked" : PilotClass;
    public string CommanderTraitSummary => string.IsNullOrWhiteSpace(PilotClass)
        ? "Pilot class unlocks at Level 3"
        : $"Reserve job: {GetPassiveJobLabel(PassiveJobKey)}";
    public bool HasAccount => !string.IsNullOrWhiteSpace(AccountName);
    public bool HasChosenPilotClass => !string.IsNullOrWhiteSpace(PilotClass);
    public bool CanChoosePilotClass => SelectedPlayerId > 0 && CurrentLevel >= 3 && !HasChosenPilotClass;

    public void UpdateAccount(AccountProfile? account)
    {
        var previousAccountLevel = AccountLevel;
        AccountProfileId = account?.AccountProfileID ?? 0;
        AccountName = account?.AccountName?.Trim() ?? string.Empty;
        AccountLevel = account?.AccountLevel ?? 1;
        AccountXP = account?.AccountXP ?? 0;
        AccountXPToNextLevel = account?.AccountXPToNextLevel ?? 150;
        CombinedPilotLevels = account?.CombinedPilotLevels ?? 0;
        CombinedCommanderLevels = account?.CombinedCommanderLevels ?? 0;
        AccountUnlockedSlotCount = account?.UnlockedSlotCount ?? 1;
        AccountCreditBonusPercent = account?.CreditBonusPercent ?? 0;
        AccountFuelBonusFlat = account?.FuelBonusFlat ?? 0;
        AccountMilestoneTitle = account?.MilestoneTitle ?? "First Fleet Goal";
        AccountMilestoneProgressText = account?.MilestoneProgressText ?? "Reach a combined pilot level of 10.";
        AccountMilestoneProgressValue = account?.MilestoneProgressValue ?? 0;
        AccountMilestoneTargetValue = account?.MilestoneTargetValue ?? 10;

        if (AccountLevel > previousAccountLevel)
        {
            OnLevelUp?.Invoke(new LevelUpNotification(
                "Account",
                string.IsNullOrWhiteSpace(AccountName) ? "Account" : AccountName,
                AccountLevel,
                AccountLevel - previousAccountLevel));
        }
    }

    public void ClearAccountContext()
    {
        AccountName = string.Empty;
        AccountProfileId = 0;
        AccountLevel = 1;
        AccountXP = 0;
        AccountXPToNextLevel = 150;
        CombinedPilotLevels = 0;
        CombinedCommanderLevels = 0;
        AccountUnlockedSlotCount = 1;
        AccountCreditBonusPercent = 0;
        AccountFuelBonusFlat = 0;
        AccountMilestoneTitle = "First Fleet Goal";
        AccountMilestoneProgressText = "Reach a combined pilot level of 10.";
        AccountMilestoneProgressValue = 0;
        AccountMilestoneTargetValue = 10;
        SelectedPlayerId = 0;
        PlayerName = "Pilot";
        PilotClass = string.Empty;
        PassiveJobKey = string.Empty;
        PilotMiningSkill = 0;
        PilotLogisticsSkill = 0;
        PilotReactorSkill = 0;
        PilotMiningTreeLevel = 0;
        PilotLogisticsTreeLevel = 0;
        PilotReactorTreeLevel = 0;
        Resource1 = 0;
        Resource2 = 0;
        Resource3 = 0;
        Resource4 = 0;
        CurrentXP = 0;
        CurrentLevel = 1;
        CurrentShipId = 1;
        OwnedShipIds = new HashSet<int> { 1 };
        ClickPowerLevel = 1;
        AutoKlickerLevel = 0;
        CritChanceLevel = 0;
        MiningResearchLevel = 0;
        LogisticsResearchLevel = 0;
        ReactorResearchLevel = 0;
        SurveyOfficeLevel = 0;
        FreightDepotLevel = 0;
        ReactorAnnexLevel = 0;
        SyncActivePilotPresentation();
    }

    public int AddXP(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        var startingLevel = CurrentLevel;
        CurrentXP += amount;
        var levelsGained = 0;

        while (CurrentXP >= CurrentLevel * 100)
        {
            CurrentXP -= CurrentLevel * 100;
            CurrentLevel++;
            levelsGained++;
        }

        if (levelsGained > 0)
        {
            OnLevelUp?.Invoke(new LevelUpNotification(
                "Pilot",
                string.IsNullOrWhiteSpace(PlayerName) ? "Pilot" : PlayerName,
                CurrentLevel,
                CurrentLevel - startingLevel));
        }

        return levelsGained;
    }

    public bool ApplyPassiveIncome()
    {
        if (CreditsPerSecond <= 0 && FuelPerSecond <= 0)
        {
            return false;
        }

        Resource1 += CreditsPerSecond;
        Resource2 += FuelPerSecond;
        return true;
    }

    public int GetModifiedCreditGain(int baseValue)
    {
        if (baseValue <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(baseValue * CreditIncomeMultiplier));
    }

    public bool IsAreaUnlocked(string areaKey)
    {
        return ProgressionLevel >= GetRequiredLevelForArea(areaKey);
    }

    public int GetRequiredLevelForArea(string areaKey) => areaKey switch
    {
        "game1" => 1,
        "game2" => 1,
        "town" => 2,
        "ships" => 2,
        "lootbox" => 2,
        "game3" => 3,
        "research" => 4,
        "game4" => 5,
        "game5" => 6,
        _ => 1
    };

    public string GetUnlockText(string areaKey) => $"Unlocks at Pilot Level {GetRequiredLevelForArea(areaKey)}";

    public void UpdateUpgrades(IEnumerable<PlayerUpgrade> upgrades)
    {
        var upgradeMap = upgrades.ToDictionary(upgrade => upgrade.UpgradeType, upgrade => upgrade.Level, StringComparer.OrdinalIgnoreCase);
        ClickPowerLevel = upgradeMap.GetValueOrDefault("ClickPower", 1);
        AutoKlickerLevel = upgradeMap.GetValueOrDefault("Auto-Klicker", 0);
        CritChanceLevel = upgradeMap.GetValueOrDefault("Crit-Chance", 0);
    }

    public void UpdateResearch(IEnumerable<PlayerResearch> research)
    {
        var researchMap = research.ToDictionary(item => item.ResearchType, item => item.Level, StringComparer.OrdinalIgnoreCase);
        MiningResearchLevel = researchMap.GetValueOrDefault("MiningEfficiency", 0);
        LogisticsResearchLevel = researchMap.GetValueOrDefault("LogisticsProtocols", 0);
        ReactorResearchLevel = researchMap.GetValueOrDefault("ReactorTheory", 0);
    }

    public void UpdateTownBuildings(IEnumerable<PlayerTownBuilding> buildings)
    {
        var buildingMap = buildings.ToDictionary(item => item.BuildingKey, item => item.Level, StringComparer.OrdinalIgnoreCase);
        SurveyOfficeLevel = buildingMap.GetValueOrDefault("survey-office", 0);
        FreightDepotLevel = buildingMap.GetValueOrDefault("freight-depot", 0);
        ReactorAnnexLevel = buildingMap.GetValueOrDefault("reactor-annex", 0);
    }

    public int GetTownBuildingLevel(string buildingKey) => buildingKey switch
    {
        "freight-depot" => FreightDepotLevel,
        "reactor-annex" => ReactorAnnexLevel,
        _ => SurveyOfficeLevel
    };

    public string? GetTownUpgradeRequirementText(string buildingKey, int targetLevel) => buildingKey switch
    {
        "freight-depot" when SurveyOfficeLevel < targetLevel => $"Requires Survey Office LVL {targetLevel}.",
        "reactor-annex" when FreightDepotLevel < targetLevel => $"Requires Freight Depot LVL {targetLevel}.",
        _ => null
    };

    public bool CanUpgradeTownBuilding(string buildingKey, int targetLevel)
    {
        return string.IsNullOrWhiteSpace(GetTownUpgradeRequirementText(buildingKey, targetLevel));
    }

    public bool IsReserveJobUnlocked(string jobKey) => jobKey switch
    {
        "hauling" => FreightDepotLevel > 0,
        "reactor" => ReactorAnnexLevel > 0,
        _ => SurveyOfficeLevel > 0
    };

    public string GetReserveJobUnlockText(string jobKey) => jobKey switch
    {
        "hauling" => "Upgrade Freight Depot to unlock Freight Convoy. Freight Depot follows Survey Office level for level.",
        "reactor" => "Upgrade Reactor Annex to unlock Reactor Drill. Reactor Annex follows Freight Depot level for level.",
        _ => "Upgrade Survey Office to unlock Survey Sweep."
    };

    public void SyncActivePilotPresentation()
    {
        ActiveCommanderName = PlayerName;
        ActiveCommanderClass = string.IsNullOrWhiteSpace(PilotClass) ? "Class Locked" : PilotClass;
        ActiveCommanderSubclass = "Pilot";
        ActiveCommanderRarity = "Pilot";
        ActiveCommanderTraitName = GetPassiveJobLabel(PassiveJobKey);
        ActiveCommanderTraitDescription = string.IsNullOrWhiteSpace(PilotClass)
            ? "Reach Level 3 to lock in this pilot's class."
            : GetActiveClassRoleSummary();
        ActiveCommanderLevel = CurrentLevel;
        ActiveCommanderXP = 0;
        ActiveCommanderMiningSkill = PilotMiningSkill;
        ActiveCommanderLogisticsSkill = PilotLogisticsSkill;
        ActiveCommanderReactorSkill = PilotReactorSkill;
        ActiveCommanderMiningTreeLevel = PilotMiningTreeLevel;
        ActiveCommanderLogisticsTreeLevel = PilotLogisticsTreeLevel;
        ActiveCommanderReactorTreeLevel = PilotReactorTreeLevel;
    }

    public Player ToPlayer()
    {
        return new Player
        {
            PlayerID = SelectedPlayerId,
            PlayerName = PlayerName,
            PilotClass = PilotClass,
            Resource1 = Resource1,
            Resource2 = Resource2,
            Resource3 = Resource3,
            Resource4 = Resource4,
            XP = CurrentXP,
            Level = CurrentLevel,
            PlayerShip = CurrentShipId,
            PassiveJobKey = PassiveJobKey,
            MiningSkillLevel = PilotMiningSkill,
            LogisticsSkillLevel = PilotLogisticsSkill,
            ReactorSkillLevel = PilotReactorSkill,
            MiningTreeLevel = PilotMiningTreeLevel,
            LogisticsTreeLevel = PilotLogisticsTreeLevel,
            ReactorTreeLevel = PilotReactorTreeLevel,
            LastActiveUtc = DateTime.UtcNow
        };
    }

    public void UpdateFromPlayer(Player player)
    {
        SelectedPlayerId = player.PlayerID;
        PlayerName = player.PlayerName;
        PilotClass = player.PilotClass?.Trim() ?? string.Empty;
        PassiveJobKey = player.PassiveJobKey?.Trim().ToLowerInvariant() ?? string.Empty;
        PilotMiningSkill = player.MiningSkillLevel;
        PilotLogisticsSkill = player.LogisticsSkillLevel;
        PilotReactorSkill = player.ReactorSkillLevel;
        PilotMiningTreeLevel = player.MiningTreeLevel;
        PilotLogisticsTreeLevel = player.LogisticsTreeLevel;
        PilotReactorTreeLevel = player.ReactorTreeLevel;
        Resource1 = player.Resource1;
        Resource2 = player.Resource2;
        Resource3 = player.Resource3;
        Resource4 = player.Resource4;
        CurrentXP = player.XP;
        CurrentLevel = player.Level;
        CurrentShipId = player.PlayerShip ?? 1;
        SyncActivePilotPresentation();
    }

    private int GetPilotMiningBonusPercent()
    {
        var classBonus = PilotClass switch
        {
            "Prospector" => 20,
            _ => 0
        };

        return classBonus + (PilotMiningSkill * 6) + (PilotMiningTreeLevel * 10);
    }

    private int GetPilotCargoBonus()
    {
        var classBonus = PilotClass switch
        {
            "Quartermaster" => 28,
            _ => 0
        };

        return classBonus + (PilotLogisticsSkill * 14) + (PilotLogisticsTreeLevel * 20);
    }

    private int GetPilotCritBonus()
    {
        var classBonus = PilotClass switch
        {
            "Vanguard" => 9,
            _ => 0
        };

        return classBonus + (PilotReactorSkill * 4) + (PilotReactorTreeLevel * 7);
    }

    private int GetClassCreditFlowBonus() => PilotClass switch
    {
        "Prospector" => 2 + PilotMiningSkill + (PilotMiningTreeLevel * 2),
        "Quartermaster" => 1,
        _ => 0
    };

    private int GetClassFuelFlowBonus() => PilotClass switch
    {
        "Quartermaster" => 1 + PilotLogisticsSkill + PilotLogisticsTreeLevel,
        _ => 0
    };

    private int GetReactorCritMultiplier() => PilotClass switch
    {
        "Vanguard" => 7 + PilotReactorSkill + (PilotReactorTreeLevel * 2),
        _ => 5
    };

    private string GetActiveClassRoleSummary() => PilotClass switch
    {
        "Quartermaster" => "Quartermaster pilots widen cargo lanes, improve fuel flow and drive the logistics tree.",
        "Vanguard" => "Vanguard pilots focus on crit pressure, reactor bursts and aggressive XP loops.",
        "Prospector" => "Prospector pilots own the strongest early economy, mining output and credit flow.",
        _ => "Classless pilots are still training. Reach Level 3 to choose a permanent class."
    };

    private static string GetPassiveJobLabel(string? jobKey) => (jobKey ?? string.Empty).ToLowerInvariant() switch
    {
        "hauling" => "Freight Convoy",
        "reactor" => "Reactor Drill",
        "survey" => "Survey Sweep",
        _ => "No Reserve Job"
    };

    public event Action<LevelUpNotification>? OnLevelUp;
}

public class GameStatus
{
    public event Action? OnResourceChanged;
    public void NotifyResourceChanged() => OnResourceChanged?.Invoke();
}