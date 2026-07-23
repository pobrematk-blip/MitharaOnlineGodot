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

db.SeedItemDefinitions();
ItemDefinitions.LoadFromDatabase(db);

if (!ItemDefinitions.Exists(ItemDefinitions.PoeiraEstelar))
{
    ItemDefinitions.Register(new ItemDefinition
    {
        Id = ItemDefinitions.PoeiraEstelar,
        Name = "Poeira Estelar",
        Type = ItemType.Material,
        MaxStack = 99,
        IsStackable = true,
    });
    Logger.Info("Poeira Estelar (ID 107) registrada como item built-in.");
}

void RegisterLojinhaItem(int id, string nome)
{
    if (!ItemDefinitions.Exists(id))
    {
        ItemDefinitions.Register(new ItemDefinition
        {
            Id = id,
            Name = nome,
            Type = ItemType.Consumable,
            MaxStack = 99,
            IsStackable = true,
        });
        Logger.Info($"{nome} (ID {id}) registrada como item built-in.");
    }
}

RegisterLojinhaItem(ItemDefinitions.LojinhaPequena, "Lojinha Pequena");
RegisterLojinhaItem(ItemDefinitions.LojinhaMedia, "Lojinha Média");
RegisterLojinhaItem(ItemDefinitions.LojinhaGrande, "Lojinha Grande");

if (!ItemDefinitions.Exists(ItemDefinitions.PergaminhoDoPet5))
{
    ItemDefinitions.Register(new ItemDefinition
    {
        Id = ItemDefinitions.PergaminhoDoPet5,
        Name = "Pergaminho do Pet (5 Tentativas)",
        Type = ItemType.Consumable,
        MaxStack = 99,
        IsStackable = true,
    });
    Logger.Info("Pergaminho do Pet (5 Tentativas) (ID 114) registrada como item built-in.");
}

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
