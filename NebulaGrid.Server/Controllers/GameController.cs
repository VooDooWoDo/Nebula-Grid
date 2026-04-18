using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NebulaGrid.Server.Data;
using NebulaGrid.Shared.Models;
using Microsoft.Extensions.Logging;

namespace NebulaGrid.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private const int MaxCharacterSlots = 3;
    private static readonly string[] PilotClasses = ["Prospector", "Quartermaster", "Vanguard"];
    private readonly GameDbContext _dbContext;
    private readonly ILogger<GameController> _logger;

    public GameController(GameDbContext dbContext, ILogger<GameController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private static readonly string[] DefaultResearchTypes = ["MiningEfficiency", "LogisticsProtocols", "ReactorTheory"];
    private sealed record PassiveJobResult(int Credits, int Fuel, int PlayerXp, int CommanderXp, string Summary);

    private static List<PlayerTownBuilding> BuildDefaultTownBuildings(Player player)
    {
        return new List<PlayerTownBuilding>
        {
            new() { PlayerID = player.PlayerID, BuildingKey = "survey-office", Level = 1 },
            new() { PlayerID = player.PlayerID, BuildingKey = "freight-depot", Level = player.Level >= 3 ? 1 : 0 },
            new() { PlayerID = player.PlayerID, BuildingKey = "reactor-annex", Level = player.Level >= 4 ? 1 : 0 }
        };
    }

    private async Task EnsurePlayerTownAsync(Player player)
    {
        var existingBuildings = await _dbContext.PlayerTownBuildings
            .Where(x => x.PlayerID == player.PlayerID)
            .ToListAsync();

        var defaultBuildings = BuildDefaultTownBuildings(player);
        var existingKeys = existingBuildings.Select(x => x.BuildingKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingBuildings = defaultBuildings.Where(x => !existingKeys.Contains(x.BuildingKey)).ToList();

        if (missingBuildings.Count == 0)
        {
            return;
        }

        _dbContext.PlayerTownBuildings.AddRange(missingBuildings);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Dictionary<string, int>> GetTownLevelMapAsync(int playerId)
    {
        var buildings = await _dbContext.PlayerTownBuildings
            .Where(x => x.PlayerID == playerId)
            .ToListAsync();

        return buildings.ToDictionary(x => x.BuildingKey, x => x.Level, StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetTownUpgradeRequirement(string buildingKey, int targetLevel, IReadOnlyDictionary<string, int> townLevels) => buildingKey switch
    {
        "freight-depot" when townLevels.GetValueOrDefault("survey-office", 0) < targetLevel => $"Requires Survey Office LVL {targetLevel}.",
        "reactor-annex" when townLevels.GetValueOrDefault("freight-depot", 0) < targetLevel => $"Requires Freight Depot LVL {targetLevel}.",
        _ => null
    };

    private static void GrantPlayerProgress(Player player, int xpGain)
    {
        player.XP += xpGain;
        while (player.XP >= player.Level * 100)
        {
            player.XP -= player.Level * 100;
            player.Level++;
        }
    }

    private static bool IsPassiveJobUnlocked(string jobKey, IReadOnlyDictionary<string, int> townLevels) => jobKey switch
    {
        "hauling" => townLevels.GetValueOrDefault("freight-depot", 0) > 0,
        "reactor" => townLevels.GetValueOrDefault("reactor-annex", 0) > 0,
        _ => townLevels.GetValueOrDefault("survey-office", 0) > 0
    };

    private static PassiveJobResult ResolvePassiveJob(Player pilot, int offlineMinutes, IReadOnlyDictionary<string, int> townLevels)
    {
        if (string.IsNullOrWhiteSpace(pilot.PilotClass))
        {
            return new PassiveJobResult(0, 0, 0, 0, $"{pilot.PlayerName} is still in training.");
        }

        var passiveJobKey = string.IsNullOrWhiteSpace(pilot.PassiveJobKey)
            ? GetDefaultPassiveJobForPilotClass(pilot.PilotClass)
            : pilot.PassiveJobKey.ToLowerInvariant();

        if (!IsPassiveJobUnlocked(passiveJobKey, townLevels))
        {
            return new PassiveJobResult(0, 0, 0, 0, $"{pilot.PlayerName} stayed idle. Upgrade the required town building first.");
        }

        return passiveJobKey switch
        {
            "hauling" => ResolveHaulingJob(pilot, offlineMinutes, townLevels),
            "reactor" => ResolveReactorJob(pilot, offlineMinutes, townLevels),
            _ => ResolveSurveyJob(pilot, offlineMinutes, townLevels)
        };
    }

    private static PassiveJobResult ResolveSurveyJob(Player pilot, int offlineMinutes, IReadOnlyDictionary<string, int> townLevels)
    {
        var buildingLevel = townLevels.GetValueOrDefault("survey-office", 0);
        var creditRate = 5 + pilot.Level + (pilot.MiningSkillLevel * 2) + (pilot.MiningTreeLevel * 4) + (buildingLevel * 3);
        var credits = offlineMinutes * creditRate;
        var reserveXp = Math.Max(1, offlineMinutes / 20) * (1 + pilot.MiningSkillLevel + pilot.MiningTreeLevel + buildingLevel);
        return new PassiveJobResult(credits, 0, 0, reserveXp, $"{pilot.PlayerName} ran Survey Sweep: +{credits} Credits");
    }

    private static PassiveJobResult ResolveHaulingJob(Player pilot, int offlineMinutes, IReadOnlyDictionary<string, int> townLevels)
    {
        var buildingLevel = townLevels.GetValueOrDefault("freight-depot", 0);
        var convoyCycles = Math.Max(1, offlineMinutes / 18);
        var fuelPerCycle = 1 + (pilot.Level / 3) + pilot.LogisticsSkillLevel + (pilot.LogisticsTreeLevel * 2) + buildingLevel;
        var fuel = convoyCycles * fuelPerCycle;
        var reserveXp = Math.Max(1, offlineMinutes / 25) * (1 + pilot.LogisticsSkillLevel + pilot.LogisticsTreeLevel + buildingLevel);
        return new PassiveJobResult(0, fuel, 0, reserveXp, $"{pilot.PlayerName} ran Freight Convoy: +{fuel} Fuel");
    }

    private static PassiveJobResult ResolveReactorJob(Player pilot, int offlineMinutes, IReadOnlyDictionary<string, int> townLevels)
    {
        var buildingLevel = townLevels.GetValueOrDefault("reactor-annex", 0);
        var drillCycles = Math.Max(1, offlineMinutes / 12);
        var playerXp = drillCycles * (1 + (pilot.Level / 2) + pilot.ReactorSkillLevel + (pilot.ReactorTreeLevel * 2) + (buildingLevel * 2));
        var reserveXp = playerXp + (drillCycles * (1 + pilot.ReactorSkillLevel + buildingLevel));
        return new PassiveJobResult(0, 0, playerXp, reserveXp, $"{pilot.PlayerName} ran Reactor Drill: +{playerXp} Player XP");
    }

    private static int GetTownUpgradeCreditCost(string buildingKey, int currentLevel) => buildingKey switch
    {
        "survey-office" => 700 * (currentLevel + 1),
        "freight-depot" => 950 * (currentLevel + 1),
        "reactor-annex" => 1200 * (currentLevel + 1),
        _ => 800 * (currentLevel + 1)
    };

    private static int GetTownUpgradeAlloyCost(string buildingKey, int currentLevel) => buildingKey switch
    {
        "survey-office" => 6 * (currentLevel + 1),
        "freight-depot" => 10 * (currentLevel + 1),
        "reactor-annex" => 14 * (currentLevel + 1),
        _ => 8 * (currentLevel + 1)
    };

    private static (int CreditPrice, int FuelPrice, int AlloyPrice, int BiomassPrice) GetShipPurchasePrice(int shipId, int engineLevel) => shipId switch
    {
        1 => (0, 0, 0, 0),
        2 => (600, 0, 60, 0),
        3 => (1800, 0, 180, 0),
        4 => (4200, 260, 420, 220),
        _ => (engineLevel * 150, engineLevel * 20, engineLevel * 18, engineLevel * 16)
    };

    private static void ResetOfflineReport(Player player)
    {
        player.OfflineCreditsGained = 0;
        player.OfflineFuelGained = 0;
        player.OfflinePlayerXpGained = 0;
        player.OfflineReserveXpGained = 0;
        player.OfflineSummary = null;
    }

    private static int GetCharacterSlotUnlockLevel(int slotId) => slotId switch
    {
        1 => 1,
        2 => 2,
        3 => 5,
        _ => int.MaxValue
    };

    private static int GetAccountLevelThreshold(int level)
    {
        return 150 + ((level - 1) * 90);
    }

    private static int GetUnlockedSlotCount(int accountLevel)
    {
        if (accountLevel >= GetCharacterSlotUnlockLevel(3))
        {
            return 3;
        }

        return accountLevel >= GetCharacterSlotUnlockLevel(2) ? 2 : 1;
    }

    private static AccountProfile ApplyAccountProgress(AccountProfile account, IReadOnlyCollection<Player> players)
    {
        var combinedPilotLevels = players.Sum(player => Math.Max(1, player.Level));
        var combinedCommanderLevels = players
            .Where(player => !string.IsNullOrWhiteSpace(player.PilotClass))
            .Select(player => player.PilotClass)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var pilotProgress = players.Sum(player => Math.Max(0, ((Math.Max(1, player.Level) - 1) * 100) + player.XP));
        var accountProgress = pilotProgress;

        var accountLevel = 1;
        var xpRemainder = accountProgress;
        var xpToNextLevel = GetAccountLevelThreshold(accountLevel);

        while (xpRemainder >= xpToNextLevel)
        {
            xpRemainder -= xpToNextLevel;
            accountLevel++;
            xpToNextLevel = GetAccountLevelThreshold(accountLevel);
        }

        var unlockedSlotCount = Math.Max(GetUnlockedSlotCount(accountLevel), players.Count == 0 ? 1 : players.Max(player => Math.Max(1, player.CharacterSlot)));
        var nextMilestoneTarget = Math.Max(10, ((combinedPilotLevels / 10) + 1) * 10);
        var creditBonusPercent = Math.Min(24, Math.Max(0, (accountLevel - 1) * 4));
        const int fuelBonusFlat = 0;

        account.AccountLevel = accountLevel;
        account.AccountXP = xpRemainder;
        account.AccountXPToNextLevel = xpToNextLevel;
        account.CombinedPilotLevels = combinedPilotLevels;
        account.CombinedCommanderLevels = combinedCommanderLevels;
        account.UnlockedSlotCount = unlockedSlotCount;
        account.CreditBonusPercent = creditBonusPercent;
        account.FuelBonusFlat = fuelBonusFlat;
        account.MilestoneTitle = unlockedSlotCount < MaxCharacterSlots ? "Next Hangar Slot" : "Fleet Cohesion";
        account.MilestoneTargetValue = nextMilestoneTarget;
        account.MilestoneProgressValue = combinedPilotLevels;
        account.MilestoneProgressText = unlockedSlotCount < MaxCharacterSlots
            ? $"Reach Account LVL {GetCharacterSlotUnlockLevel(unlockedSlotCount + 1)} to unlock Slot {unlockedSlotCount + 1}. Current Account XP: {xpRemainder}/{xpToNextLevel}. Combined pilot levels: {combinedPilotLevels}/{nextMilestoneTarget}."
            : $"All hangar slots are online. Current Account XP: {xpRemainder}/{xpToNextLevel}. Push your fleet to a combined pilot level of {nextMilestoneTarget}.";

        return account;
    }

    private static string GetCharacterSlotUnlockText(int slotId, int accountLevel, int combinedPilotLevels, bool isOccupied)
    {
        if (isOccupied)
        {
            return "Character online.";
        }

        if (slotId == 1)
        {
            return "Create your first pilot. Pilot class unlocks at Level 3.";
        }

        var requiredLevel = GetCharacterSlotUnlockLevel(slotId);
        return accountLevel >= requiredLevel
            ? $"Slot unlocked at Account LVL {requiredLevel}. Ready for a new pilot."
            : $"Unlocks at Account LVL {requiredLevel}. Current Account LVL {accountLevel}. Combined pilot levels: {combinedPilotLevels}.";
    }

    private static string NormalizePilotClass(string? pilotClass) => pilotClass switch
    {
        "Quartermaster" => "Quartermaster",
        "Vanguard" => "Vanguard",
        _ => "Prospector"
    };

    private static string GetDefaultPassiveJobForPilotClass(string pilotClass) => pilotClass switch
    {
        "Quartermaster" => "hauling",
        "Vanguard" => "reactor",
        _ => "survey"
    };

    private static string GetSkillTypeForPilotClass(string pilotClass) => pilotClass switch
    {
        "Quartermaster" => "logistics",
        "Vanguard" => "reactor",
        _ => "mining"
    };

    private static int GetPilotSkillCost(string skillType, int currentLevel) => skillType switch
    {
        "mining" => 220 * (currentLevel + 1),
        "logistics" => 260 * (currentLevel + 1),
        "reactor" => 320 * (currentLevel + 1),
        _ => 250 * (currentLevel + 1)
    };

    private static (int Credits, int Fuel) GetPilotTalentCost(string talentType, int currentLevel) => talentType switch
    {
        "mining" => (750 * (currentLevel + 1), 12 * (currentLevel + 1)),
        "logistics" => (820 * (currentLevel + 1), 14 * (currentLevel + 1)),
        "reactor" => (900 * (currentLevel + 1), 16 * (currentLevel + 1)),
        _ => (800 * (currentLevel + 1), 10 * (currentLevel + 1))
    };

    private async Task<List<string>> GetAvailablePilotClassesAsync(Player player)
    {
        var usedClasses = await _dbContext.Players
            .Where(otherPlayer => otherPlayer.AccountProfileID == player.AccountProfileID && otherPlayer.PlayerID != player.PlayerID && !string.IsNullOrWhiteSpace(otherPlayer.PilotClass))
            .Select(otherPlayer => otherPlayer.PilotClass)
            .ToListAsync();

        return PilotClasses.Where(pilotClass => !usedClasses.Contains(pilotClass, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    private void AddBasePlayerState(Player player)
    {
        _dbContext.PlayerStats.Add(new PlayerStats { PlayerID = player.PlayerID });
        _dbContext.OwnedShips.Add(new OwnedShip { PlayerID = player.PlayerID, ShipID = 1 });
        _dbContext.PlayerUpgrades.AddRange(
            new PlayerUpgrade { PlayerID = player.PlayerID, UpgradeType = "ClickPower", Level = 1 },
            new PlayerUpgrade { PlayerID = player.PlayerID, UpgradeType = "Auto-Klicker", Level = 0 },
            new PlayerUpgrade { PlayerID = player.PlayerID, UpgradeType = "Crit-Chance", Level = 0 }
        );
        _dbContext.PlayerResearch.AddRange(
            new PlayerResearch { PlayerID = player.PlayerID, ResearchType = "MiningEfficiency", Level = 0 },
            new PlayerResearch { PlayerID = player.PlayerID, ResearchType = "LogisticsProtocols", Level = 0 },
            new PlayerResearch { PlayerID = player.PlayerID, ResearchType = "ReactorTheory", Level = 0 }
        );
        _dbContext.PlayerTownBuildings.AddRange(BuildDefaultTownBuildings(player));
    }

    private async Task ResetCharacterDataAsync()
    {
        _dbContext.PlayerTownBuildings.RemoveRange(_dbContext.PlayerTownBuildings);
        _dbContext.PlayerResearch.RemoveRange(_dbContext.PlayerResearch);
        _dbContext.PlayerUpgrades.RemoveRange(_dbContext.PlayerUpgrades);
        _dbContext.PlayerStats.RemoveRange(_dbContext.PlayerStats);
        _dbContext.OwnedShips.RemoveRange(_dbContext.OwnedShips);
        _dbContext.FarmPlots.RemoveRange(_dbContext.FarmPlots);
        _dbContext.Players.RemoveRange(_dbContext.Players);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<AccountProfile?> GetAccountAsync(int accountId)
    {
        var account = await _dbContext.AccountProfiles.FirstOrDefaultAsync(existingAccount => existingAccount.AccountProfileID == accountId);
        if (account is null)
        {
            return null;
        }

        var players = await _dbContext.Players
            .Where(player => player.AccountProfileID == accountId)
            .OrderBy(player => player.PlayerID)
            .ToListAsync();

        return ApplyAccountProgress(account, players);
    }

    private async Task<List<AccountProfile>> GetAccountsAsync()
    {
        var accounts = await _dbContext.AccountProfiles
            .OrderBy(account => account.AccountProfileID)
            .ToListAsync();

        if (accounts.Count == 0)
        {
            return accounts;
        }

        var accountIds = accounts.Select(account => account.AccountProfileID).ToList();
        var players = await _dbContext.Players
            .Where(player => accountIds.Contains(player.AccountProfileID))
            .OrderBy(player => player.PlayerID)
            .ToListAsync();
        var playersByAccount = players.GroupBy(player => player.AccountProfileID).ToDictionary(group => group.Key, group => (IReadOnlyCollection<Player>)group.ToList());

        foreach (var account in accounts)
        {
            ApplyAccountProgress(
                account,
                playersByAccount.GetValueOrDefault(account.AccountProfileID, Array.Empty<Player>()));
        }

        return accounts;
    }

    private async Task<List<CharacterSlotSummary>> BuildCharacterSlotsAsync(int accountId)
    {
        var account = await GetAccountAsync(accountId);
        if (account is null)
        {
            return Enumerable.Range(1, MaxCharacterSlots)
                .Select(slotId => new CharacterSlotSummary
                {
                    SlotId = slotId,
                    IsUnlocked = slotId == 1,
                    UnlockLevelRequirement = GetCharacterSlotUnlockLevel(slotId),
                    UnlockText = slotId == 1
                        ? "Create an account first, then build your first pilot."
                        : $"Unlocks when any character reaches LVL {GetCharacterSlotUnlockLevel(slotId)}.",
                        Player = null
                })
                .ToList();
        }

        var players = await _dbContext.Players
            .OrderBy(player => player.PlayerID)
            .Where(player => player.AccountProfileID == accountId)
            .ToListAsync();

        foreach (var player in players)
        {
            await EnsurePlayerResearchAsync(player.PlayerID);
            await EnsurePlayerTownAsync(player);
            await ApplyOfflineProgressAsync(player);
        }

        ApplyAccountProgress(account, players);
        var slots = new List<CharacterSlotSummary>();

        for (var slotId = 1; slotId <= MaxCharacterSlots; slotId++)
        {
            var player = players.FirstOrDefault(existingPlayer => existingPlayer.CharacterSlot == slotId);
            var isUnlocked = slotId <= account.UnlockedSlotCount || player is not null;
            slots.Add(new CharacterSlotSummary
            {
                SlotId = slotId,
                IsUnlocked = isUnlocked,
                UnlockLevelRequirement = GetCharacterSlotUnlockLevel(slotId),
                UnlockText = GetCharacterSlotUnlockText(slotId, account.AccountLevel, account.CombinedPilotLevels, player is not null),
                Player = player
            });
        }

        return slots;
    }

    private async Task<int> GetCreditsPerSecondAsync(int playerId)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        if (player.Level < 5)
        {
            return 0;
        }

        var upgrades = await _dbContext.PlayerUpgrades
            .Where(x => x.PlayerID == playerId)
            .ToListAsync();
        var research = await _dbContext.PlayerResearch
            .Where(x => x.PlayerID == playerId)
            .ToListAsync();

        var clickPowerLevel = upgrades.FirstOrDefault(x => x.UpgradeType == "ClickPower")?.Level ?? 1;
        var autoKlickerLevel = upgrades.FirstOrDefault(x => x.UpgradeType == "Auto-Klicker")?.Level ?? 0;
        var miningResearchLevel = research.FirstOrDefault(x => x.ResearchType == "MiningEfficiency")?.Level ?? 0;
        var pilotMiningBonus = string.Equals(player.PilotClass, "Prospector", StringComparison.OrdinalIgnoreCase)
            ? 20 + (player.MiningSkillLevel * 6) + (player.MiningTreeLevel * 10)
            : 0;
        var multiplier = (1 + (miningResearchLevel * 0.15)) * (1 + (pilotMiningBonus / 100.0));

        var baseCreditsPerSecond = autoKlickerLevel * Math.Max(1, clickPowerLevel);
        if (baseCreditsPerSecond <= 0)
        {
            return 0;
        }

        return (int)Math.Max(0, Math.Floor(baseCreditsPerSecond * multiplier));
    }

    private async Task EnsurePlayerResearchAsync(int playerId)
    {
        var existingTypes = await _dbContext.PlayerResearch
            .Where(x => x.PlayerID == playerId)
            .Select(x => x.ResearchType)
            .ToListAsync();

        var missingTypes = DefaultResearchTypes
            .Where(type => !existingTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingTypes.Count == 0)
        {
            return;
        }

        foreach (var missingType in missingTypes)
        {
            _dbContext.PlayerResearch.Add(new PlayerResearch
            {
                PlayerID = playerId,
                ResearchType = missingType,
                Level = 0
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private static (int FuelCost, int BiomassCost) GetResearchCost(string researchType, int currentLevel) => researchType switch
    {
        "MiningEfficiency" => (25 * (currentLevel + 1), 10 * (currentLevel + 1)),
        "LogisticsProtocols" => (40 * (currentLevel + 1), 14 * (currentLevel + 1)),
        "ReactorTheory" => (55 * (currentLevel + 1), 18 * (currentLevel + 1)),
        _ => (100 * (currentLevel + 1), 20 * (currentLevel + 1))
    };

    private async Task ApplyOfflineProgressAsync(Player player)
    {
        ResetOfflineReport(player);
        await EnsurePlayerTownAsync(player);

        var now = DateTime.UtcNow;
        if (player.LastActiveUtc == default)
        {
            player.LastActiveUtc = now;
            await _dbContext.SaveChangesAsync();
            return;
        }

        var offlineDuration = now - player.LastActiveUtc;
        if (offlineDuration.TotalSeconds < 1)
        {
            player.LastActiveUtc = now;
            await _dbContext.SaveChangesAsync();
            return;
        }

        var cappedSeconds = (int)Math.Floor(Math.Min(offlineDuration.TotalSeconds, TimeSpan.FromHours(8).TotalSeconds));
        var offlineMinutes = Math.Max(0, cappedSeconds / 60);
        var creditsPerSecond = await GetCreditsPerSecondAsync(player.PlayerID);
        if (creditsPerSecond > 0)
        {
            var earnedCredits = cappedSeconds * creditsPerSecond;
            player.Resource1 += earnedCredits;
            player.OfflineCreditsGained += earnedCredits;
        }

        if (offlineMinutes > 0)
        {
            var townLevels = await GetTownLevelMapAsync(player.PlayerID);
            var reservePilots = await _dbContext.Players
                .Where(x => x.AccountProfileID == player.AccountProfileID && x.PlayerID != player.PlayerID && !string.IsNullOrWhiteSpace(x.PilotClass))
                .OrderBy(x => x.CharacterSlot)
                .ToListAsync();

            var dutySummaries = new List<string>();
            foreach (var reservePilot in reservePilots)
            {
                var result = ResolvePassiveJob(reservePilot, offlineMinutes, townLevels);
                if (result.Credits > 0)
                {
                    player.Resource1 += result.Credits;
                    player.OfflineCreditsGained += result.Credits;
                }

                if (result.Fuel > 0)
                {
                    player.Resource2 += result.Fuel;
                    player.OfflineFuelGained += result.Fuel;
                }

                if (result.PlayerXp > 0)
                {
                    GrantPlayerProgress(player, result.PlayerXp);
                    player.OfflinePlayerXpGained += result.PlayerXp;
                }

                if (result.CommanderXp > 0)
                {
                    GrantPlayerProgress(reservePilot, result.CommanderXp);
                    player.OfflineReserveXpGained += result.CommanderXp;
                }

                dutySummaries.Add(result.Summary);
            }

            var summaryParts = new List<string>();
            if (player.OfflineCreditsGained > 0)
            {
                summaryParts.Add($"+{player.OfflineCreditsGained} Credits");
            }

            if (player.OfflineFuelGained > 0)
            {
                summaryParts.Add($"+{player.OfflineFuelGained} Fuel");
            }

            if (player.OfflinePlayerXpGained > 0)
            {
                summaryParts.Add($"+{player.OfflinePlayerXpGained} Player XP");
            }

            if (player.OfflineReserveXpGained > 0)
            {
                summaryParts.Add($"+{player.OfflineReserveXpGained} Reserve Pilot XP");
            }

            if (summaryParts.Count > 0)
            {
                player.OfflineSummary = $"Offline report ({offlineMinutes} min): {string.Join(" • ", summaryParts)}. {string.Join(" | ", dutySummaries.Take(2))}";
            }
        }

        player.LastActiveUtc = now;
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Player> GetOrCreatePlayerAsync(int playerId)
    {
        var player = await _dbContext.Players.FirstOrDefaultAsync(x => x.PlayerID == playerId);
        if (player is not null)
        {
            return player;
        }

        player = new Player
        {
            PlayerID = playerId,
            PlayerName = $"Pilot {playerId}",
            AccountProfileID = 0,
            CharacterSlot = 0,
            PilotClass = string.Empty,
            Resource1 = 0,
            Resource2 = 0,
            Resource3 = 0,
            Resource4 = 0,
            XP = 0,
            Level = 1,
            PlayerShip = 1,
            PassiveJobKey = string.Empty,
            LastActiveUtc = DateTime.UtcNow
        };

        _dbContext.Players.Add(player);
        await _dbContext.SaveChangesAsync();
        AddBasePlayerState(player);
        await _dbContext.SaveChangesAsync();

        return player;
    }

    [HttpGet]
    public async Task<ActionResult<IdleGameState>> GetGameState()
    {
        _logger.LogInformation("Game state loaded");
        var state = await _dbContext.IdleGameStates.FirstOrDefaultAsync();
        if (state is null)
        {
            state = new IdleGameState();
            await _dbContext.IdleGameStates.AddAsync(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }

    [HttpPost]
    public async Task<ActionResult<IdleGameState>> SaveGameState(IdleGameState state)
    {
        if (state == null)
        {
            return BadRequest("State is required.");
        }

        var existing = await _dbContext.IdleGameStates.FirstOrDefaultAsync();
        state.LastSaved = DateTime.UtcNow;

        if (existing is null)
        {
            await _dbContext.IdleGameStates.AddAsync(state);
            await _dbContext.SaveChangesAsync();
            return CreatedAtAction(nameof(GetGameState), state);
        }

        existing.Currency = state.Currency;
        existing.IncomePerSecond = state.IncomePerSecond;
        existing.ClickPower = state.ClickPower;
        existing.UpgradeLevel = state.UpgradeLevel;
        existing.ClickUpgradeLevel = state.ClickUpgradeLevel;
        existing.TotalClicks = state.TotalClicks;
        existing.LastSaved = state.LastSaved;

        await _dbContext.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPost("reset")]
    public async Task<ActionResult<IdleGameState>> ResetGameState()
    {
        var existing = await _dbContext.IdleGameStates.FirstOrDefaultAsync();
        if (existing is null)
        {
            existing = new IdleGameState();
            await _dbContext.IdleGameStates.AddAsync(existing);
        }
        else
        {
            existing.Currency = 0;
            existing.IncomePerSecond = 1;
            existing.ClickPower = 1;
            existing.UpgradeLevel = 0;
            existing.ClickUpgradeLevel = 0;
            existing.TotalClicks = 0;
            existing.LastSaved = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPost("lootbox")]
    public async Task<ActionResult<LootboxResult>> OpenLootbox([FromBody] int playerId)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        await ApplyOfflineProgressAsync(player);

        if (player.Resource1 < 100)
        {
            return BadRequest("Not enough credits.");
        }

        player.Resource1 -= 100;

        var rnd = new Random();
        var dice = rnd.Next(1, 101);
        string message;
        string icon;

        if (dice <= 33)
        {
            var creditWin = rnd.Next(30, 96);
            player.Resource1 += creditWin;
            icon = "🔩";
            message = $"Scrap haul: {creditWin} Credits recovered.";
        }
        else if (dice <= 58)
        {
            var creditWin = rnd.Next(180, 361);
            var xpWin = 18;
            player.Resource1 += creditWin;
            player.XP += xpWin;
            while (player.XP >= player.Level * 100)
            {
                player.XP -= player.Level * 100;
                player.Level++;
            }

            icon = "💎";
            message = $"Data cache cracked: +{creditWin} Credits and +{xpWin} Pilot XP.";
        }
        else if (dice <= 76)
        {
            var alloyWin = rnd.Next(12, 31);
            player.Resource3 += alloyWin;
            icon = "🪨";
            message = $"Industrial crate: +{alloyWin} Alloy secured.";
        }
        else if (dice <= 90)
        {
            var biomassWin = rnd.Next(16, 39);
            player.Resource4 += biomassWin;
            icon = "🧬";
            message = $"Bio pod stabilized: +{biomassWin} Biomass harvested.";
        }
        else if (dice <= 95)
        {
            var fuelWin = rnd.Next(8, 21);
            player.Resource2 += fuelWin;
            icon = "🔋";
            message = $"Fuel cells recovered: +{fuelWin} Fuel.";
        }
        else
        {
            const int creditWin = 900;
            const int fuelWin = 35;
            const int alloyWin = 70;
            const int biomassWin = 90;
            const int xpWin = 75;
            player.Resource1 += creditWin;
            player.Resource2 += fuelWin;
            player.Resource3 += alloyWin;
            player.Resource4 += biomassWin;
            player.XP += xpWin;
            while (player.XP >= player.Level * 100)
            {
                player.XP -= player.Level * 100;
                player.Level++;
            }

            icon = "⭐";
            message = $"Legendary cache! +{creditWin} Credits, +{fuelWin} Fuel, +{alloyWin} Alloy, +{biomassWin} Biomass and +{xpWin} Pilot XP.";
        }

        player.LastActiveUtc = DateTime.UtcNow;

        if (player.AccountProfileID > 0)
        {
            var account = await _dbContext.AccountProfiles.FirstOrDefaultAsync(x => x.AccountProfileID == player.AccountProfileID);
            if (account is not null)
            {
                var accountPlayers = await _dbContext.Players
                    .Where(x => x.AccountProfileID == player.AccountProfileID)
                    .ToListAsync();
                ApplyAccountProgress(account, accountPlayers);
            }
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new LootboxResult
        {
            Message = message,
            Icon = icon,
            NewResource1 = player.Resource1,
            NewResource2 = player.Resource2,
            NewResource3 = player.Resource3,
            NewResource4 = player.Resource4,
            NewPilotXP = player.XP,
            NewPilotLevel = player.Level
        });
    }

    [HttpGet("game1")]
    [HttpGet("game1state")]
    public async Task<ActionResult<Game1State>> GetGame1State()
    {
        _logger.LogInformation("Game1 state loaded");
        var state = await _dbContext.Game1States.FirstOrDefaultAsync();
        if (state is null)
        {
            state = new Game1State();
            await _dbContext.Game1States.AddAsync(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }

    [HttpPost("game1")]
    [HttpPost("savegame1state")]
    public async Task<ActionResult<Game1State>> SaveGame1State(Game1State state)
    {
        if (state == null)
        {
            return BadRequest("State is required.");
        }

        var existing = await _dbContext.Game1States.FirstOrDefaultAsync();
        state.LastSaved = DateTime.UtcNow;

        if (existing is null)
        {
            await _dbContext.Game1States.AddAsync(state);
        }
        else
        {
            existing.Currency = state.Currency;
            existing.IncomePerSecond = state.IncomePerSecond;
            existing.ClickPower = state.ClickPower;
            existing.UpgradeLevel = state.UpgradeLevel;
            existing.ClickUpgradeLevel = state.ClickUpgradeLevel;
            existing.TotalClicks = state.TotalClicks;
            existing.LastSaved = state.LastSaved;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(existing ?? state);
    }

    [HttpGet("game2")]
    [HttpGet("game2state")]
    public async Task<ActionResult<Game2State>> GetGame2State()
    {
        _logger.LogInformation("Game2 state loaded");
        var state = await _dbContext.Game2States.FirstOrDefaultAsync();
        if (state is null)
        {
            state = new Game2State();
            await _dbContext.Game2States.AddAsync(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }

    [HttpPost("game2")]
    [HttpPost("savegame2state")]
    public async Task<ActionResult<Game2State>> SaveGame2State(Game2State state)
    {
        if (state == null)
        {
            return BadRequest("State is required.");
        }

        var existing = await _dbContext.Game2States.FirstOrDefaultAsync();
        state.LastSaved = DateTime.UtcNow;

        if (existing is null)
        {
            await _dbContext.Game2States.AddAsync(state);
        }
        else
        {
            existing.Currency = state.Currency;
            existing.IncomePerSecond = state.IncomePerSecond;
            existing.ClickPower = state.ClickPower;
            existing.UpgradeLevel = state.UpgradeLevel;
            existing.ClickUpgradeLevel = state.ClickUpgradeLevel;
            existing.TotalClicks = state.TotalClicks;
            existing.LastSaved = state.LastSaved;
            existing.CargoUsed = state.CargoUsed;
            existing.CargoCapacity = state.CargoCapacity;
            existing.StoredOre = state.StoredOre;
            existing.StoredFuel = state.StoredFuel;
            existing.PendingResource1 = state.PendingResource1;
            existing.PendingResource2 = state.PendingResource2;
            existing.PendingResource3 = state.PendingResource3;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(existing ?? state);
    }

    [HttpGet("game3")]
    [HttpGet("game3state")]
    public async Task<ActionResult<Game3State>> GetGame3State()
    {
        _logger.LogInformation("Game3 state loaded");
        var state = await _dbContext.Game3States.FirstOrDefaultAsync();
        if (state is null)
        {
            state = new Game3State();
            await _dbContext.Game3States.AddAsync(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }

    [HttpPost("game3")]
    [HttpPost("savegame3state")]
    public async Task<ActionResult<Game3State>> SaveGame3State(Game3State state)
    {
        if (state == null)
        {
            return BadRequest("State is required.");
        }

        var existing = await _dbContext.Game3States.FirstOrDefaultAsync();
        state.LastSaved = DateTime.UtcNow;

        if (existing is null)
        {
            await _dbContext.Game3States.AddAsync(state);
        }
        else
        {
            existing.Currency = state.Currency;
            existing.IncomePerSecond = state.IncomePerSecond;
            existing.ClickPower = state.ClickPower;
            existing.UpgradeLevel = state.UpgradeLevel;
            existing.ClickUpgradeLevel = state.ClickUpgradeLevel;
            existing.TotalClicks = state.TotalClicks;
            existing.LastSaved = state.LastSaved;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(existing ?? state);
    }

    [HttpGet("game4")]
    [HttpGet("game4state")]
    public async Task<ActionResult<Game4State>> GetGame4State()
    {
        _logger.LogInformation("Game4 state loaded");
        var state = await _dbContext.Game4States.FirstOrDefaultAsync();
        if (state is null)
        {
            state = new Game4State();
            await _dbContext.Game4States.AddAsync(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }

    [HttpPost("game4")]
    [HttpPost("savegame4state")]
    public async Task<ActionResult<Game4State>> SaveGame4State(Game4State state)
    {
        if (state == null)
        {
            return BadRequest("State is required.");
        }

        var existing = await _dbContext.Game4States.FirstOrDefaultAsync();
        state.LastSaved = DateTime.UtcNow;

        if (existing is null)
        {
            await _dbContext.Game4States.AddAsync(state);
        }
        else
        {
            existing.Currency = state.Currency;
            existing.IncomePerSecond = state.IncomePerSecond;
            existing.ClickPower = state.ClickPower;
            existing.UpgradeLevel = state.UpgradeLevel;
            existing.ClickUpgradeLevel = state.ClickUpgradeLevel;
            existing.TotalClicks = state.TotalClicks;
            existing.LastSaved = state.LastSaved;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(existing ?? state);
    }

    [HttpGet("player/{playerId:int}/game5")]
    [HttpGet("player/{playerId:int}/game5state")]
    public async Task<ActionResult<Game5State>> GetGame5State(int playerId)
    {
        await GetOrCreatePlayerAsync(playerId);
        _logger.LogInformation("Game5 state loaded");
        var state = await _dbContext.Game5States.FirstOrDefaultAsync(x => x.PlayerID == playerId);
        if (state is null)
        {
            state = new Game5State { PlayerID = playerId };
            await _dbContext.Game5States.AddAsync(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }

    [HttpPost("player/{playerId:int}/game5")]
    [HttpPost("player/{playerId:int}/savegame5state")]
    public async Task<ActionResult<Game5State>> SaveGame5State(int playerId, Game5State state)
    {
        if (state == null)
        {
            return BadRequest("State is required.");
        }

        await GetOrCreatePlayerAsync(playerId);

        var existing = await _dbContext.Game5States.FirstOrDefaultAsync(x => x.PlayerID == playerId);
        state.LastSaved = DateTime.UtcNow;
        state.PlayerID = playerId;

        if (existing is null)
        {
            await _dbContext.Game5States.AddAsync(state);
        }
        else
        {
            existing.PlayerID = playerId;
            existing.Currency = state.Currency;
            existing.IncomePerSecond = state.IncomePerSecond;
            existing.ClickPower = state.ClickPower;
            existing.UpgradeLevel = state.UpgradeLevel;
            existing.ClickUpgradeLevel = state.ClickUpgradeLevel;
            existing.TotalClicks = state.TotalClicks;
            existing.LastSaved = state.LastSaved;
            existing.WaveNumber = state.WaveNumber;
            existing.BaseIntegrity = state.BaseIntegrity;
            existing.EnemiesDefeated = state.EnemiesDefeated;
            existing.TowerLayout = state.TowerLayout;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(existing ?? state);
    }

    [HttpGet("players")]
    public async Task<ActionResult<List<Player>>> GetPlayers()
    {
        var players = await _dbContext.Players.OrderBy(player => player.PlayerID).ToListAsync();
        foreach (var player in players)
        {
            await EnsurePlayerResearchAsync(player.PlayerID);
            await EnsurePlayerTownAsync(player);
            await ApplyOfflineProgressAsync(player);
        }
        return Ok(players);
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<List<AccountProfile>>> GetAccounts()
    {
        var accounts = await GetAccountsAsync();
        return Ok(accounts);
    }

    [HttpGet("account/{accountId:int}")]
    public async Task<ActionResult<AccountProfile>> GetAccount(int accountId)
    {
        var account = await GetAccountAsync(accountId);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost("accounts")]
    public async Task<ActionResult<AccountProfile>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.AccountName))
        {
            return BadRequest("Account name is required.");
        }

        var normalizedAccountName = request.AccountName.Trim();
        var duplicateExists = await _dbContext.AccountProfiles.AnyAsync(account => account.AccountName.ToLower() == normalizedAccountName.ToLower());
        if (duplicateExists)
        {
            return BadRequest("An account with this name already exists.");
        }

        var account = new AccountProfile
        {
            AccountName = normalizedAccountName,
            CreatedUtc = DateTime.UtcNow
        };

        if (!await _dbContext.AccountProfiles.AnyAsync())
        {
            await ResetCharacterDataAsync();
        }

        _dbContext.AccountProfiles.Add(account);
        await _dbContext.SaveChangesAsync();
        return Ok(account);
    }

    [HttpDelete("account/{accountId:int}")]
    public async Task<IActionResult> DeleteAccount(int accountId)
    {
        var account = await _dbContext.AccountProfiles.FirstOrDefaultAsync(existingAccount => existingAccount.AccountProfileID == accountId);
        if (account is null)
        {
            return NotFound();
        }

        _dbContext.AccountProfiles.Remove(account);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("account/{accountId:int}/slots")]
    public async Task<ActionResult<List<CharacterSlotSummary>>> GetCharacterSlots(int accountId)
    {
        var slots = await BuildCharacterSlotsAsync(accountId);
        return Ok(slots);
    }

    [HttpPost("account/{accountId:int}/slots/{slotId:int}")]
    public async Task<ActionResult<Player>> CreateCharacterInSlot(int accountId, int slotId, [FromBody] CreateCharacterRequest request)
    {
        var account = await GetAccountAsync(accountId);
        if (account is null)
        {
            return BadRequest("Create an account before creating characters.");
        }

        if (slotId < 1 || slotId > MaxCharacterSlots)
        {
            return BadRequest("Unknown character slot.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Character name is required.");
        }

        var existingPlayer = await _dbContext.Players.FirstOrDefaultAsync(player => player.AccountProfileID == accountId && player.CharacterSlot == slotId);
        if (existingPlayer is not null)
        {
            return BadRequest("Character slot is already occupied.");
        }

        var players = await _dbContext.Players
            .Where(player => player.AccountProfileID == accountId)
            .ToListAsync();
        var highestLevel = players.Count == 0 ? 1 : players.Max(player => player.Level);
        var requiredLevel = GetCharacterSlotUnlockLevel(slotId);
        if (slotId > 1 && highestLevel < requiredLevel)
        {
            return BadRequest($"Slot is locked until a character reaches LVL {requiredLevel}.");
        }

        var player = new Player
        {
            AccountProfileID = accountId,
            CharacterSlot = slotId,
            PlayerName = request.PlayerName.Trim(),
            PilotClass = string.Empty,
            Resource1 = 0,
            Resource2 = 0,
            Resource3 = 0,
            Resource4 = 0,
            XP = 0,
            Level = 1,
            PlayerShip = 1,
            PassiveJobKey = string.Empty,
            LastActiveUtc = DateTime.UtcNow
        };

        _dbContext.Players.Add(player);
        await _dbContext.SaveChangesAsync();
        AddBasePlayerState(player);
        await _dbContext.SaveChangesAsync();

        return Ok(player);
    }

    [HttpGet("player/{playerId:int}")]
    public async Task<ActionResult<Player>> GetPlayer(int playerId)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerResearchAsync(playerId);
        await EnsurePlayerTownAsync(player);
        await ApplyOfflineProgressAsync(player);
        return Ok(player);
    }

    [HttpGet("player")]
    public async Task<ActionResult<Player>> GetPlayerByQuery([FromQuery] int playerId = 1)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerResearchAsync(playerId);
        await EnsurePlayerTownAsync(player);
        await ApplyOfflineProgressAsync(player);
        return Ok(player);
    }

    [HttpPost("player")]
    [HttpPost("saveplayer")]
    public async Task<ActionResult<Player>> SavePlayer(Player player)
    {
        var existing = await _dbContext.Players.FirstOrDefaultAsync(x => x.PlayerID == player.PlayerID);
        player.LastActiveUtc = DateTime.UtcNow;
        var accountProfileId = existing?.AccountProfileID ?? player.AccountProfileID;

        if (existing is null)
        {
            _dbContext.Players.Add(player);
        }
        else
        {
            existing.PlayerName = player.PlayerName;
            existing.Resource1 = player.Resource1;
            existing.Resource2 = player.Resource2;
            existing.Resource3 = player.Resource3;
            existing.Resource4 = player.Resource4;
            existing.XP = player.XP;
            existing.Level = player.Level;
            existing.PlayerShip = player.PlayerShip;
            existing.PilotClass = player.PilotClass;
            existing.PassiveJobKey = player.PassiveJobKey;
            existing.MiningSkillLevel = player.MiningSkillLevel;
            existing.LogisticsSkillLevel = player.LogisticsSkillLevel;
            existing.ReactorSkillLevel = player.ReactorSkillLevel;
            existing.MiningTreeLevel = player.MiningTreeLevel;
            existing.LogisticsTreeLevel = player.LogisticsTreeLevel;
            existing.ReactorTreeLevel = player.ReactorTreeLevel;
            existing.LastActiveUtc = player.LastActiveUtc;
        }

        await _dbContext.SaveChangesAsync();

        if (accountProfileId > 0)
        {
            var account = await _dbContext.AccountProfiles.FirstOrDefaultAsync(x => x.AccountProfileID == accountProfileId);
            if (account is not null)
            {
                var accountPlayers = await _dbContext.Players
                    .Where(x => x.AccountProfileID == accountProfileId)
                    .ToListAsync();

                ApplyAccountProgress(account, accountPlayers);
                await _dbContext.SaveChangesAsync();
            }
        }

        return Ok(existing ?? player);
    }

    [HttpPost("player/{playerId:int}/class/{pilotClass}")]
    public async Task<ActionResult<Player>> ChoosePilotClass(int playerId, string pilotClass)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        if (player.Level < 3)
        {
            return BadRequest("Pilot class unlocks at Level 3.");
        }

        if (!string.IsNullOrWhiteSpace(player.PilotClass))
        {
            return BadRequest("This pilot already has a permanent class.");
        }

        var normalizedClass = NormalizePilotClass(pilotClass);
        var availableClasses = await GetAvailablePilotClassesAsync(player);
        if (!availableClasses.Contains(normalizedClass, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("This class is already assigned to another pilot on the account.");
        }

        player.PilotClass = normalizedClass;
        player.PassiveJobKey = GetDefaultPassiveJobForPilotClass(normalizedClass);

        switch (GetSkillTypeForPilotClass(normalizedClass))
        {
            case "logistics":
                player.LogisticsSkillLevel = Math.Max(1, player.LogisticsSkillLevel);
                break;
            case "reactor":
                player.ReactorSkillLevel = Math.Max(1, player.ReactorSkillLevel);
                break;
            default:
                player.MiningSkillLevel = Math.Max(1, player.MiningSkillLevel);
                break;
        }

        player.LastActiveUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(player);
    }

    [HttpPost("player/{playerId:int}/skills/{skillType}/upgrade")]
    public async Task<ActionResult<Player>> UpgradePilotSkill(int playerId, string skillType)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        if (string.IsNullOrWhiteSpace(player.PilotClass))
        {
            return BadRequest("Choose a pilot class before training skills.");
        }

        var normalizedSkillType = skillType.Trim().ToLowerInvariant();
        if (!string.Equals(normalizedSkillType, GetSkillTypeForPilotClass(player.PilotClass), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("This pilot can only train the skill tree tied to their class.");
        }

        var currentLevel = normalizedSkillType switch
        {
            "logistics" => player.LogisticsSkillLevel,
            "reactor" => player.ReactorSkillLevel,
            _ => player.MiningSkillLevel
        };

        var cost = GetPilotSkillCost(normalizedSkillType, currentLevel);
        if (player.Resource1 < cost)
        {
            return BadRequest($"Not enough Credits. Need {cost}.");
        }

        player.Resource1 -= cost;
        switch (normalizedSkillType)
        {
            case "logistics":
                player.LogisticsSkillLevel++;
                break;
            case "reactor":
                player.ReactorSkillLevel++;
                break;
            default:
                player.MiningSkillLevel++;
                break;
        }

        player.LastActiveUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(player);
    }

    [HttpPost("player/{playerId:int}/talents/{talentType}/upgrade")]
    public async Task<ActionResult<Player>> UpgradePilotTalent(int playerId, string talentType)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        if (string.IsNullOrWhiteSpace(player.PilotClass))
        {
            return BadRequest("Choose a pilot class before unlocking talents.");
        }

        var normalizedTalentType = talentType.Trim().ToLowerInvariant();
        if (!string.Equals(normalizedTalentType, GetSkillTypeForPilotClass(player.PilotClass), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("This pilot can only upgrade the talent tree tied to their class.");
        }

        var currentLevel = normalizedTalentType switch
        {
            "logistics" => player.LogisticsTreeLevel,
            "reactor" => player.ReactorTreeLevel,
            _ => player.MiningTreeLevel
        };

        if (currentLevel >= 3)
        {
            return BadRequest("This pilot talent tree is already maxed.");
        }

        var (creditsCost, fuelCost) = GetPilotTalentCost(normalizedTalentType, currentLevel);
        if (player.Resource1 < creditsCost || player.Resource2 < fuelCost)
        {
            return BadRequest($"Not enough resources. Need {creditsCost} Credits and {fuelCost} Fuel.");
        }

        player.Resource1 -= creditsCost;
        player.Resource2 -= fuelCost;

        switch (normalizedTalentType)
        {
            case "logistics":
                player.LogisticsTreeLevel++;
                break;
            case "reactor":
                player.ReactorTreeLevel++;
                break;
            default:
                player.MiningTreeLevel++;
                break;
        }

        player.LastActiveUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(player);
    }

    [HttpGet("player/{playerId:int}/town")]
    public async Task<ActionResult<List<PlayerTownBuilding>>> GetTownBuildings(int playerId)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerTownAsync(player);

        var buildings = await _dbContext.PlayerTownBuildings
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.BuildingKey)
            .ToListAsync();

        return Ok(buildings);
    }

    [HttpPost("player/{playerId:int}/town/upgrade/{buildingKey}")]
    public async Task<ActionResult<List<PlayerTownBuilding>>> UpgradeTownBuilding(int playerId, string buildingKey)
    {
        var normalizedKey = buildingKey.Trim().ToLowerInvariant();
        if (normalizedKey is not ("survey-office" or "freight-depot" or "reactor-annex"))
        {
            return BadRequest("Unknown town building.");
        }

        var player = await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerTownAsync(player);
        await ApplyOfflineProgressAsync(player);

        var building = await _dbContext.PlayerTownBuildings.FirstOrDefaultAsync(x => x.PlayerID == playerId && x.BuildingKey == normalizedKey);
        if (building is null)
        {
            return NotFound();
        }

        if (building.Level >= 5)
        {
            return BadRequest("Building is already maxed.");
        }

        var townLevels = await GetTownLevelMapAsync(playerId);
        var upgradeRequirement = GetTownUpgradeRequirement(normalizedKey, building.Level + 1, townLevels);
        if (!string.IsNullOrWhiteSpace(upgradeRequirement))
        {
            return BadRequest(upgradeRequirement);
        }

        var creditCost = GetTownUpgradeCreditCost(normalizedKey, building.Level);
        var alloyCost = GetTownUpgradeAlloyCost(normalizedKey, building.Level);
        if (player.Resource1 < creditCost || player.Resource3 < alloyCost)
        {
            return BadRequest("Not enough resources for town upgrade.");
        }

        player.Resource1 -= creditCost;
        player.Resource3 -= alloyCost;
        building.Level++;
        player.LastActiveUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var buildings = await _dbContext.PlayerTownBuildings
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.BuildingKey)
            .ToListAsync();

        return Ok(buildings);
    }

    [HttpGet("player/{playerId:int}/stats")]
    public async Task<ActionResult<PlayerStats>> GetPlayerStats(int playerId)
    {
        await GetOrCreatePlayerAsync(playerId);
        var stats = await _dbContext.PlayerStats.FirstOrDefaultAsync(x => x.PlayerID == playerId);
        if (stats is null)
        {
            stats = new PlayerStats { PlayerID = playerId };
            _dbContext.PlayerStats.Add(stats);
            await _dbContext.SaveChangesAsync();
        }

        return Ok(stats);
    }

    [HttpPost("player/{playerId:int}/stats")]
    public async Task<ActionResult<PlayerStats>> SavePlayerStats(int playerId, PlayerStats stats)
    {
        var existing = await _dbContext.PlayerStats.FirstOrDefaultAsync(x => x.PlayerID == playerId);
        if (existing is null)
        {
            stats.PlayerID = playerId;
            _dbContext.PlayerStats.Add(stats);
            await _dbContext.SaveChangesAsync();
            return Ok(stats);
        }

        existing.TotalClicks = stats.TotalClicks;
        existing.TotalResourcesEarned = stats.TotalResourcesEarned;
        await _dbContext.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpGet("player/{playerId:int}/upgrades")]
    public async Task<ActionResult<List<PlayerUpgrade>>> GetPlayerUpgrades(int playerId)
    {
        await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerResearchAsync(playerId);
        var upgrades = await _dbContext.PlayerUpgrades
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.UpgradeType)
            .ToListAsync();

        return Ok(upgrades);
    }

    [HttpPost("player/{playerId:int}/upgrades")]
    public async Task<ActionResult<List<PlayerUpgrade>>> SavePlayerUpgrades(int playerId, List<PlayerUpgrade> upgrades)
    {
        await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerResearchAsync(playerId);
        var existing = await _dbContext.PlayerUpgrades.Where(x => x.PlayerID == playerId).ToListAsync();
        _dbContext.PlayerUpgrades.RemoveRange(existing);

        foreach (var upgrade in upgrades)
        {
            upgrade.PlayerID = playerId;
        }

        _dbContext.PlayerUpgrades.AddRange(upgrades);
        await _dbContext.SaveChangesAsync();
        return Ok(upgrades.OrderBy(x => x.UpgradeType).ToList());
    }

    [HttpGet("player/{playerId:int}/research")]
    public async Task<ActionResult<List<PlayerResearch>>> GetPlayerResearch(int playerId)
    {
        await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerResearchAsync(playerId);

        var research = await _dbContext.PlayerResearch
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.ResearchType)
            .ToListAsync();

        return Ok(research);
    }

    [HttpPost("player/{playerId:int}/research/buy/{researchType}")]
    public async Task<ActionResult<List<PlayerResearch>>> BuyResearch(int playerId, string researchType)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        await EnsurePlayerResearchAsync(playerId);
        await ApplyOfflineProgressAsync(player);

        var research = await _dbContext.PlayerResearch.FirstOrDefaultAsync(x => x.PlayerID == playerId && x.ResearchType == researchType);
        if (research is null)
        {
            return NotFound();
        }

        var (fuelCost, biomassCost) = GetResearchCost(researchType, research.Level);
        if (player.Resource2 < fuelCost || player.Resource4 < biomassCost)
        {
            return BadRequest("Not enough fuel or biomass.");
        }

        player.Resource2 -= fuelCost;
        player.Resource4 -= biomassCost;
        research.Level++;
        player.LastActiveUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        var updatedResearch = await _dbContext.PlayerResearch
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.ResearchType)
            .ToListAsync();

        return Ok(updatedResearch);
    }

    [HttpGet("player/{playerId:int}/farmplots")]
    public async Task<ActionResult<List<FarmPlot>>> GetFarmPlots(int playerId)
    {
        await GetOrCreatePlayerAsync(playerId);

        var plots = await _dbContext.FarmPlots
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.PlotID)
            .ToListAsync();

        if (plots.Count == 0)
        {
            plots = Enumerable.Range(1, 4)
                .Select(index => new FarmPlot
                {
                    PlotID = index + (playerId * 1000),
                    PlayerID = playerId,
                    PlantName = string.Empty,
                    FinishTime = DateTime.UtcNow,
                    IsPlanted = false,
                    ResourceYield = 0
                })
                .ToList();

            _dbContext.FarmPlots.AddRange(plots);
            await _dbContext.SaveChangesAsync();
        }

        return Ok(plots);
    }

    [HttpPost("player/{playerId:int}/farmplots")]
    public async Task<ActionResult<List<FarmPlot>>> SaveFarmPlots(int playerId, List<FarmPlot> plots)
    {
        await GetOrCreatePlayerAsync(playerId);
        var existing = await _dbContext.FarmPlots
            .Where(x => x.PlayerID == playerId)
            .ToDictionaryAsync(x => x.PlotID);

        foreach (var plot in plots)
        {
            plot.PlayerID = playerId;
            plot.FinishTime = DateTime.SpecifyKind(plot.FinishTime, DateTimeKind.Utc);

            if (existing.TryGetValue(plot.PlotID, out var existingPlot))
            {
                existingPlot.PlantName = plot.PlantName;
                existingPlot.FinishTime = plot.FinishTime;
                existingPlot.IsPlanted = plot.IsPlanted;
                existingPlot.ResourceYield = plot.ResourceYield;
                continue;
            }

            _dbContext.FarmPlots.Add(plot);
        }

        await _dbContext.SaveChangesAsync();
        var savedPlots = await _dbContext.FarmPlots
            .Where(x => x.PlayerID == playerId)
            .OrderBy(x => x.PlotID)
            .ToListAsync();

        return Ok(savedPlots);
    }

    [HttpGet("ships")]
    public async Task<ActionResult<List<Ship>>> GetShips()
    {
        var ships = await _dbContext.Ships.OrderBy(x => x.ShipID).ToListAsync();
        return Ok(ships);
    }

    [HttpGet("player/{playerId:int}/ownedships")]
    public async Task<ActionResult<List<OwnedShip>>> GetOwnedShips(int playerId)
    {
        await GetOrCreatePlayerAsync(playerId);
        var ownedShips = await _dbContext.OwnedShips.Where(x => x.PlayerID == playerId).OrderBy(x => x.ShipID).ToListAsync();
        return Ok(ownedShips);
    }

    [HttpPost("player/{playerId:int}/equip-ship/{shipId:int}")]
    public async Task<ActionResult<Player>> EquipShip(int playerId, int shipId)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        var isOwned = await _dbContext.OwnedShips.AnyAsync(x => x.PlayerID == playerId && x.ShipID == shipId);
        if (!isOwned)
        {
            return BadRequest("Ship is not owned.");
        }

        player.PlayerShip = shipId;
        player.LastActiveUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(player);
    }

    [HttpPost("player/{playerId:int}/buy-ship/{shipId:int}")]
    public async Task<ActionResult<Player>> BuyShip(int playerId, int shipId)
    {
        var player = await GetOrCreatePlayerAsync(playerId);
        await ApplyOfflineProgressAsync(player);
        var ship = await _dbContext.Ships.FirstOrDefaultAsync(x => x.ShipID == shipId);
        if (ship is null)
        {
            return NotFound();
        }

        var alreadyOwned = await _dbContext.OwnedShips.AnyAsync(x => x.PlayerID == playerId && x.ShipID == shipId);
        if (!alreadyOwned)
        {
            var (creditPrice, fuelPrice, alloyPrice, biomassPrice) = GetShipPurchasePrice(shipId, ship.EngineLevel);
            if (player.Resource1 < creditPrice || player.Resource2 < fuelPrice || player.Resource3 < alloyPrice || player.Resource4 < biomassPrice)
            {
                return BadRequest("Not enough resources for this ship.");
            }

            player.Resource1 -= creditPrice;
            player.Resource2 -= fuelPrice;
            player.Resource3 -= alloyPrice;
            player.Resource4 -= biomassPrice;
            _dbContext.OwnedShips.Add(new OwnedShip { PlayerID = playerId, ShipID = shipId });
        }

        player.PlayerShip = shipId;
        player.LastActiveUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(player);
    }

}
