using System.Diagnostics;
using Mithara.Server;
using Mithara.Server.Database;
using Mithara.Server.Entities;
using Mithara.Server.Network;

Logger.Info("=== Mithara MMO Server ===");
Logger.Info("");

var config = ConfigLoader.Load();
Logger.Info($"Porta: {config.Port}");
Logger.Info($"Canais: {config.ChannelCount}");
Logger.Info($"Tick Rate: {config.TickRate} Hz");
Logger.Info($"Max Conexões: {config.MaxConnections}");
Logger.Info($"PostgreSQL: {config.PgUser}@{config.PgHost}:{config.PgPort}/{config.PgDatabase}");
Logger.Info("");

var db = new DatabaseManager(config.PgHost, config.PgPort, config.PgDatabase, config.PgUser, config.PgPassword);
db.Initialize();

// Auto-migrate from SQLite if data exists there
var sqlitePath = Path.Combine(AppContext.BaseDirectory, config.DbPath);
DatabaseMigration.MigrateFromSqlite(sqlitePath, db);

db.SeedItemDefinitions();
ItemDefinitions.LoadFromDatabase(db);

var server = new GameServer(config, db);
server.Start();

float tickInterval = 1f / config.TickRate;
var stopwatch = Stopwatch.StartNew();
double accumulator = 0;

Logger.Info($"Server rodando. logs em: {AppContext.BaseDirectory}logs");
Logger.Info("");

Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    server.Stop();
};

while (server.IsRunning)
{
    try
    {
        double frameTime = stopwatch.Elapsed.TotalSeconds;
        stopwatch.Restart();
        accumulator += frameTime;

        server.PollEvents();

        while (accumulator >= tickInterval)
        {
            server.Update((float)tickInterval);
            accumulator -= tickInterval;
        }
    }
    catch (Exception ex)
    {
        Logger.Error("Erro no loop principal do servidor", ex);
    }

    Thread.Sleep(1);
}

Logger.Info("Server finalizado.");
