using System.Diagnostics;
using Mithara.Server;
using Mithara.Server.Database;
using Mithara.Server.Network;

Console.WriteLine("=== Mithara MMO Server ===");
Console.WriteLine();

var config = ConfigLoader.Load();
Console.WriteLine($"Porta: {config.Port}");
Console.WriteLine($"Canais: {config.ChannelCount}");
Console.WriteLine($"Tick Rate: {config.TickRate} Hz");
Console.WriteLine($"Max Conexões: {config.MaxConnections}");
Console.WriteLine($"DB: {config.DbPath}");
Console.WriteLine();

var dbPath = Path.Combine(AppContext.BaseDirectory, config.DbPath);
var db = new DatabaseManager(dbPath);
db.Initialize();

var server = new GameServer(config, db);
server.Start();

float tickInterval = 1f / config.TickRate;
var stopwatch = Stopwatch.StartNew();
double accumulator = 0;

Console.WriteLine("Server rodando. Pressione Ctrl+C para parar.");
Console.WriteLine();

Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    server.Stop();
};

while (server.IsRunning)
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

    Thread.Sleep(1);
}

Console.WriteLine("Server finalizado.");
