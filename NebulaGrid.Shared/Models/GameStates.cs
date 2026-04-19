namespace NebulaGrid.Shared.Models;

public class Game1State : IdleGameState { }

public class Game2State : IdleGameState
{
	public int CargoUsed { get; set; }
	public int CargoCapacity { get; set; }
	public int StoredOre { get; set; }
	public int StoredFuel { get; set; }
	public int PendingResource1 { get; set; }
	public int PendingResource2 { get; set; }
	public int PendingResource3 { get; set; }
}

public class Game3State : IdleGameState { }

public class Game4State : IdleGameState { }

public class Game5State : IdleGameState
{
	public int WaveNumber { get; set; } = 1;
	public int BaseIntegrity { get; set; } = 5;
	public int EnemiesDefeated { get; set; }
	public string TowerLayout { get; set; } = string.Empty;
}