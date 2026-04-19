using Microsoft.EntityFrameworkCore;
using NebulaGrid.Server.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddLogging(builder => builder.AddConsole());

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<GameDbContext>(options =>
{
    var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "Data");
    Directory.CreateDirectory(dataDirectory);
    var dbPath = Path.Combine(dataDirectory, "nebula.db");
    options.UseSqlite($"Data Source={dbPath}");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    dbContext.Database.EnsureCreated();
    await EnsureSchemaUpdatedAsync(dbContext);
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors();

app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

static async Task EnsureSchemaUpdatedAsync(GameDbContext dbContext)
{
    await EnsureAccountProfilesTableExistsAsync(dbContext);
    await EnsureColumnExistsAsync(dbContext, "AccountProfiles", "AccountXpBank", "INTEGER NOT NULL DEFAULT 0");
    var lastActiveColumnAdded = await EnsureColumnExistsAsync(
        dbContext,
        "Player",
        "LastActiveUtc",
        "TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z'");

    await EnsureColumnExistsAsync(dbContext, "Player", "AccountProfileID", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "CharacterSlot", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "PilotClass", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnExistsAsync(dbContext, "Player", "PassiveJobKey", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnExistsAsync(dbContext, "Player", "Resource3", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "Resource4", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "MiningSkillLevel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "LogisticsSkillLevel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "ReactorSkillLevel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "MiningTreeLevel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "LogisticsTreeLevel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "ReactorTreeLevel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "Player", "OfflinePopupSeenUtc", "TEXT NULL");

    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "CargoUsed", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "CargoCapacity", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "StoredOre", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "StoredFuel", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "PendingResource1", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "PendingResource2", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "PendingResource3", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "WaveNumber", "INTEGER NOT NULL DEFAULT 1");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "BaseIntegrity", "INTEGER NOT NULL DEFAULT 20");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "EnemiesDefeated", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "TowerLayout", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnExistsAsync(dbContext, "IdleGameStates", "PlayerID", "INTEGER NOT NULL DEFAULT 0");
    await EnsurePlayerResearchTableExistsAsync(dbContext);
    await EnsurePlayerTownBuildingsTableExistsAsync(dbContext);
    await EnsureGame5StatesAssignedToPlayersAsync(dbContext);
    await EnsureGame5PlayerIndexAsync(dbContext);
    await EnsureShipsSeededAsync(dbContext);
    await ResetLegacyPlayerDataIfNoAccountAsync(dbContext);
    await BackfillAccountOwnedPlayersAsync(dbContext);
    await BackfillPilotClassDataAsync(dbContext);
    await DropLegacyCommanderProfilesTableAsync(dbContext);

    if (lastActiveColumnAdded)
    {
        var nowIso = DateTime.UtcNow.ToString("O");
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE \"Player\" SET \"LastActiveUtc\" = $lastActiveUtc";

            var parameter = updateCommand.CreateParameter();
            parameter.ParameterName = "$lastActiveUtc";
            parameter.Value = nowIso;
            updateCommand.Parameters.Add(parameter);

            await updateCommand.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

static async Task BackfillAccountOwnedPlayersAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var accountCountCommand = connection.CreateCommand();
        accountCountCommand.CommandText = "SELECT COUNT(*) FROM \"AccountProfiles\"";
        var accountCount = Convert.ToInt64(await accountCountCommand.ExecuteScalarAsync() ?? 0L);
        if (accountCount <= 0)
        {
            return;
        }

        await using var firstAccountCommand = connection.CreateCommand();
        firstAccountCommand.CommandText = "SELECT \"AccountProfileID\" FROM \"AccountProfiles\" ORDER BY \"AccountProfileID\" LIMIT 1";
        var firstAccountId = Convert.ToInt32(await firstAccountCommand.ExecuteScalarAsync() ?? 0);
        if (firstAccountId <= 0)
        {
            return;
        }

        await using var queryCommand = connection.CreateCommand();
        queryCommand.CommandText = "SELECT \"PlayerID\", \"CharacterSlot\", \"AccountProfileID\" FROM \"Player\" ORDER BY \"PlayerID\"";
        var rowsNeedingUpdate = new List<(int PlayerId, int Slot)>();

        await using (var reader = await queryCommand.ExecuteReaderAsync())
        {
            var nextSlot = 1;
            while (await reader.ReadAsync())
            {
                var playerId = reader.GetInt32(0);
                var slot = reader.GetInt32(1);
                var accountId = reader.GetInt32(2);
                if (accountId <= 0 || slot <= 0)
                {
                    rowsNeedingUpdate.Add((playerId, nextSlot));
                    nextSlot++;
                }
            }
        }

        foreach (var row in rowsNeedingUpdate)
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE \"Player\" SET \"AccountProfileID\" = $accountId, \"CharacterSlot\" = $slot WHERE \"PlayerID\" = $playerId";

            var accountParameter = updateCommand.CreateParameter();
            accountParameter.ParameterName = "$accountId";
            accountParameter.Value = firstAccountId;
            updateCommand.Parameters.Add(accountParameter);

            var slotParameter = updateCommand.CreateParameter();
            slotParameter.ParameterName = "$slot";
            slotParameter.Value = row.Slot;
            updateCommand.Parameters.Add(slotParameter);

            var playerParameter = updateCommand.CreateParameter();
            playerParameter.ParameterName = "$playerId";
            playerParameter.Value = row.PlayerId;
            updateCommand.Parameters.Add(playerParameter);

            await updateCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task BackfillPilotClassDataAsync(GameDbContext dbContext)
{
    if (!await TableExistsAsync(dbContext, "CommanderProfiles"))
    {
        return;
    }

    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var queryCommand = connection.CreateCommand();
        queryCommand.CommandText = """
SELECT p."PlayerID", c."ClassType", c."PassiveJobKey", c."MiningSkillLevel", c."LogisticsSkillLevel", c."ReactorSkillLevel", c."MiningTreeLevel", c."LogisticsTreeLevel", c."ReactorTreeLevel"
FROM "Player" p
LEFT JOIN "CommanderProfiles" c ON c."CommanderID" = p."ActiveCommanderId"
WHERE COALESCE(TRIM(p."PilotClass"), '') = '' AND c."CommanderID" IS NOT NULL
""";

        var updates = new List<(int PlayerId, string PilotClass, string PassiveJobKey, int MiningSkill, int LogisticsSkill, int ReactorSkill, int MiningTree, int LogisticsTree, int ReactorTree)>();

        await using (var reader = await queryCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                updates.Add((
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    reader.IsDBNull(8) ? 0 : reader.GetInt32(8)));
            }
        }

        foreach (var update in updates)
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = """
UPDATE "Player"
SET "PilotClass" = $pilotClass,
    "PassiveJobKey" = $passiveJobKey,
    "MiningSkillLevel" = $miningSkill,
    "LogisticsSkillLevel" = $logisticsSkill,
    "ReactorSkillLevel" = $reactorSkill,
    "MiningTreeLevel" = $miningTree,
    "LogisticsTreeLevel" = $logisticsTree,
    "ReactorTreeLevel" = $reactorTree
WHERE "PlayerID" = $playerId
""";

            void AddParameter(string name, object value)
            {
                var parameter = updateCommand.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                updateCommand.Parameters.Add(parameter);
            }

            AddParameter("$pilotClass", update.PilotClass);
            AddParameter("$passiveJobKey", update.PassiveJobKey);
            AddParameter("$miningSkill", update.MiningSkill);
            AddParameter("$logisticsSkill", update.LogisticsSkill);
            AddParameter("$reactorSkill", update.ReactorSkill);
            AddParameter("$miningTree", update.MiningTree);
            AddParameter("$logisticsTree", update.LogisticsTree);
            AddParameter("$reactorTree", update.ReactorTree);
            AddParameter("$playerId", update.PlayerId);

            await updateCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task DropLegacyCommanderProfilesTableAsync(GameDbContext dbContext)
{
    if (!await TableExistsAsync(dbContext, "CommanderProfiles"))
    {
        return;
    }

    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = "DROP TABLE IF EXISTS \"CommanderProfiles\"";
        await dropCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureAccountProfilesTableExistsAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = """
CREATE TABLE IF NOT EXISTS "AccountProfiles" (
    "AccountProfileID" INTEGER NOT NULL CONSTRAINT "PK_AccountProfiles" PRIMARY KEY AUTOINCREMENT,
    "AccountName" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "AccountXpBank" INTEGER NOT NULL DEFAULT 0
);
""";
        await createCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task ResetLegacyPlayerDataIfNoAccountAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        async Task<long> ExecuteScalarAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
        }

        var accountCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM \"AccountProfiles\"");
        if (accountCount > 0)
        {
            return;
        }

        var playerCount = await ExecuteScalarAsync("SELECT COUNT(*) FROM \"Player\"");
        if (playerCount == 0)
        {
            return;
        }

        foreach (var tableName in new[] { "PlayerTownBuildings", "PlayerResearch", "PlayerUpgrades", "PlayerStats", "OwnedShips", "FarmPlots", "Player" })
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = $"DELETE FROM \"{tableName}\"";
            await deleteCommand.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePlayerResearchTableExistsAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = """
CREATE TABLE IF NOT EXISTS "PlayerResearch" (
    "PlayerID" INTEGER NOT NULL,
    "ResearchType" TEXT NOT NULL,
    "Level" INTEGER NOT NULL,
    CONSTRAINT "PK_PlayerResearch" PRIMARY KEY ("PlayerID", "ResearchType"),
    CONSTRAINT "FK_PlayerResearch_Player_PlayerID" FOREIGN KEY ("PlayerID") REFERENCES "Player" ("PlayerID") ON DELETE CASCADE
);
""";
        await createCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePlayerTownBuildingsTableExistsAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = """
CREATE TABLE IF NOT EXISTS "PlayerTownBuildings" (
    "PlayerID" INTEGER NOT NULL,
    "BuildingKey" TEXT NOT NULL,
    "Level" INTEGER NOT NULL,
    CONSTRAINT "PK_PlayerTownBuildings" PRIMARY KEY ("PlayerID", "BuildingKey"),
    CONSTRAINT "FK_PlayerTownBuildings_Player_PlayerID" FOREIGN KEY ("PlayerID") REFERENCES "Player" ("PlayerID") ON DELETE CASCADE
);
""";
        await createCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureGame5StatesAssignedToPlayersAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = """
UPDATE "IdleGameStates"
SET "PlayerID" = COALESCE((SELECT MIN("PlayerID") FROM "Player"), 1)
WHERE "Discriminator" = 'Game5State' AND COALESCE("PlayerID", 0) = 0;
""";
        await updateCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureGame5PlayerIndexAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = """
CREATE UNIQUE INDEX IF NOT EXISTS "IX_IdleGameStates_Game5_PlayerID"
ON "IdleGameStates" ("PlayerID")
WHERE "Discriminator" = 'Game5State' AND "PlayerID" > 0;
""";
        await createCommand.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureShipsSeededAsync(GameDbContext dbContext)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        var ships = new[]
        {
            (ShipId: 1, ModelName: "Starter Rocket", CargoCapacity: 50, EngineLevel: 1),
            (ShipId: 2, ModelName: "Silver Glider", CargoCapacity: 85, EngineLevel: 2),
            (ShipId: 3, ModelName: "Orbital Carrier", CargoCapacity: 140, EngineLevel: 3),
            (ShipId: 4, ModelName: "Apex Leviathan", CargoCapacity: 240, EngineLevel: 5)
        };

        foreach (var ship in ships)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
INSERT INTO "Ships" ("ShipID", "ModelName", "CargoCapacity", "EngineLevel")
VALUES ($shipId, $modelName, $cargoCapacity, $engineLevel)
ON CONFLICT("ShipID") DO UPDATE SET
    "ModelName" = excluded."ModelName",
    "CargoCapacity" = excluded."CargoCapacity",
    "EngineLevel" = excluded."EngineLevel";
""";

            void AddParameter(string name, object value)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            AddParameter("$shipId", ship.ShipId);
            AddParameter("$modelName", ship.ModelName);
            AddParameter("$cargoCapacity", ship.CargoCapacity);
            AddParameter("$engineLevel", ship.EngineLevel);
            await command.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task<bool> EnsureColumnExistsAsync(GameDbContext dbContext, string tableName, string columnName, string columnDefinition)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = $"PRAGMA table_info(\"{tableName}\")";

        await using var reader = await pragmaCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition}";
        await alterCommand.ExecuteNonQueryAsync();
        return true;
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task<bool> TableExistsAsync(GameDbContext dbContext, string tableName)
{
    var connection = dbContext.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;

    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
        return result > 0;
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}
