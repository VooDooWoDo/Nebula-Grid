using Microsoft.EntityFrameworkCore;
using NebulaGrid.Shared.Models;

namespace NebulaGrid.Server.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    public DbSet<IdleGameState> IdleGameStates => Set<IdleGameState>();
    public DbSet<AccountProfile> AccountProfiles => Set<AccountProfile>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<PlayerTownBuilding> PlayerTownBuildings => Set<PlayerTownBuilding>();
    public DbSet<PlayerUpgrade> PlayerUpgrades => Set<PlayerUpgrade>();
    public DbSet<PlayerResearch> PlayerResearch => Set<PlayerResearch>();
    public DbSet<Ship> Ships => Set<Ship>();
    public DbSet<OwnedShip> OwnedShips => Set<OwnedShip>();
    public DbSet<FarmPlot> FarmPlots => Set<FarmPlot>();
    public DbSet<Game1State> Game1States => Set<Game1State>();
    public DbSet<Game2State> Game2States => Set<Game2State>();
    public DbSet<Game3State> Game3States => Set<Game3State>();
    public DbSet<Game4State> Game4States => Set<Game4State>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>().ToTable("Player");
        modelBuilder.Entity<AccountProfile>().ToTable("AccountProfiles");
        modelBuilder.Entity<PlayerStats>().ToTable("PlayerStats");
        modelBuilder.Entity<PlayerTownBuilding>().ToTable("PlayerTownBuildings");
        modelBuilder.Entity<PlayerUpgrade>().ToTable("PlayerUpgrades");
        modelBuilder.Entity<PlayerResearch>().ToTable("PlayerResearch");
        modelBuilder.Entity<Ship>().ToTable("Ships");
        modelBuilder.Entity<OwnedShip>().ToTable("OwnedShips");
        modelBuilder.Entity<FarmPlot>().ToTable("FarmPlots");

        modelBuilder.Entity<AccountProfile>()
            .HasKey(account => account.AccountProfileID);

        modelBuilder.Entity<Player>()
            .HasKey(player => player.PlayerID);

        modelBuilder.Entity<Player>()
            .HasOne(player => player.AccountProfile)
            .WithMany()
            .HasForeignKey(player => player.AccountProfileID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Ship>()
            .HasKey(ship => ship.ShipID);

        modelBuilder.Entity<FarmPlot>()
            .HasKey(plot => plot.PlotID);

        modelBuilder.Entity<Player>()
            .HasOne<Ship>()
            .WithMany()
            .HasForeignKey(player => player.PlayerShip)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Player>()
            .HasIndex(player => new { player.AccountProfileID, player.CharacterSlot })
            .IsUnique();

        modelBuilder.Entity<PlayerStats>()
            .HasKey(stats => stats.PlayerID);

        modelBuilder.Entity<PlayerStats>()
            .HasOne(stats => stats.Player)
            .WithOne()
            .HasForeignKey<PlayerStats>(stats => stats.PlayerID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerUpgrade>()
            .HasKey(upgrade => new { upgrade.PlayerID, upgrade.UpgradeType });

        modelBuilder.Entity<PlayerUpgrade>()
            .HasOne(upgrade => upgrade.Player)
            .WithMany()
            .HasForeignKey(upgrade => upgrade.PlayerID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerResearch>()
            .HasKey(research => new { research.PlayerID, research.ResearchType });

        modelBuilder.Entity<PlayerResearch>()
            .HasOne(research => research.Player)
            .WithMany()
            .HasForeignKey(research => research.PlayerID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerTownBuilding>()
            .HasKey(building => new { building.PlayerID, building.BuildingKey });

        modelBuilder.Entity<PlayerTownBuilding>()
            .HasOne(building => building.Player)
            .WithMany()
            .HasForeignKey(building => building.PlayerID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OwnedShip>()
            .HasKey(ownedShip => new { ownedShip.PlayerID, ownedShip.ShipID });

        modelBuilder.Entity<OwnedShip>()
            .HasOne(ownedShip => ownedShip.Player)
            .WithMany()
            .HasForeignKey(ownedShip => ownedShip.PlayerID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OwnedShip>()
            .HasOne(ownedShip => ownedShip.Ship)
            .WithMany()
            .HasForeignKey(ownedShip => ownedShip.ShipID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FarmPlot>()
            .HasOne(plot => plot.Player)
            .WithMany()
            .HasForeignKey(plot => plot.PlayerID)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Ship>().HasData(
            new Ship { ShipID = 1, ModelName = "Starter Rocket", CargoCapacity = 50, EngineLevel = 1 },
            new Ship { ShipID = 2, ModelName = "Silver Glider", CargoCapacity = 85, EngineLevel = 2 },
            new Ship { ShipID = 3, ModelName = "Orbital Carrier", CargoCapacity = 140, EngineLevel = 3 }
        );

    }
}
