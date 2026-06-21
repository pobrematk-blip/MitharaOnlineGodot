using System.Collections.Generic;
using Mithara.Server.World;
using Mithara.Server.World.Pathfinding;

namespace Mithara.Server;

public class ServerConfig
{
    public int Port { get; set; } = 7777;
    public string BindAddress { get; set; } = "0.0.0.0";
    public int MaxConnections { get; set; } = 500;
    public int ChannelCount { get; set; } = 4;
    public int TickRate { get; set; } = 20;
    public float AoiRadius { get; set; } = 1200f;
    public string DbPath { get; set; } = "data/mithara.db";
    public bool AdminMode { get; set; }
    public string AdminAccounts { get; set; } = "";

    // PostgreSQL settings
    public string PgHost { get; set; } = "localhost";
    public int PgPort { get; set; } = 5432;
    public string PgDatabase { get; set; } = "mithara_db";
    public string PgUser { get; set; } = "mithara";
    public string PgPassword { get; set; } = "";

    // No-mob zones: rectangular areas where monsters cannot enter/spawn
    public List<NoMobZone> NoMobZones { get; set; } = new();

    // Server-authoritative monster spawn points.
    public List<SpawnPoint> SpawnPoints { get; set; } = new();

    // Server-authoritative NPC spawn points.
    public List<NpcSpawnPoint> NpcSpawnPoints { get; set; } = new();

    // Blocked areas: physical obstacles for pathfinding (buildings, walls, water)
    public List<BlockedArea> BlockedAreas { get; set; } = new();

    // Pathfinding grid settings
    public int PathfindingGridWidth { get; set; } = 200;
    public int PathfindingGridHeight { get; set; } = 200;
    public float PathfindingCellSize { get; set; } = 32f;
    public float PathfindingOriginX { get; set; } = 0f;
    public float PathfindingOriginY { get; set; } = 0f;
}
