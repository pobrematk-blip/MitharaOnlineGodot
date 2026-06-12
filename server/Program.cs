using System.Diagnostics;
using Mithara.Server;
using Mithara.Server.Database;
using Mithara.Server.Network;

Logger.Info("=== Mithara MMO Server ===");
Logger.Info("");

var config = ConfigLoader.Load();
Logger.Info($"Porta: {config.Port}");
Logger.Info($"Canais: {config.ChannelCount}");
Logger.Info($"Tick Rate: {config.TickRate} Hz");
Logger.Info($"Max Conexões: {config.MaxConnections}");
Logger.Info($"DB: {config.DbPath}");
Logger.Info("");

var dbPath = Path.Combine(AppContext.BaseDirectory, config.DbPath);
var db = new DatabaseManager(dbPath);
db.Initialize();

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
