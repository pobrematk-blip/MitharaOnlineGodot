using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Packets;
using Mithara.Server.World.Pathfinding;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mithara.Server.Network;

public partial class GameServer
{
    private const byte TileTypeBlock = 0;
    private const byte TileTypeTeleport = 1;
    private const byte TileTypeNpcVoid = 2;
    private const float MapTileSize = 16f;

    private static readonly Dictionary<string, Dictionary<(int X, int Y), byte>> _tileData = new();
    private static readonly Dictionary<string, Dictionary<(int X, int Y), TeleportTileInfo>> _teleportTargets = new();

    private static bool IsFullBlockTileType(byte type) => type == TileTypeBlock;
    private static bool IsMobBlockedTileType(byte type) => type == TileTypeBlock || type == TileTypeNpcVoid;

    public class TeleportTileInfo
    {
        public int PairId { get; set; }
        public string TargetScene { get; set; } = "main";
        public float TargetX { get; set; }
        public float TargetY { get; set; }
    }

    public void LoadTileData()
    {
        _tileData.Clear();
        _teleportTargets.Clear();

        string dataDir = ResolveTileDataDirectory();

        if (!Directory.Exists(dataDir))
        {
            Logger.Info($"[TILE DATA] Diretorio nao encontrado: {dataDir}");
            return;
        }

        Logger.Info($"[TILE DATA] Lendo tiles de: {dataDir}");

        foreach (string file in Directory.GetFiles(dataDir, "*.json"))
        {
            string sceneName = Path.GetFileNameWithoutExtension(file).ToLower();
            string json = File.ReadAllText(file);
            var entries = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (entries == null) continue;

            var tiles = new Dictionary<(int, int), byte>();
            var teleports = new Dictionary<(int, int), TeleportTileInfo>();

            foreach (var entry in entries)
            {
                int tileX = entry.GetProperty("tileX").GetInt32();
                int tileY = entry.GetProperty("tileY").GetInt32();
                byte type = entry.GetProperty("type").GetByte();
                tiles[(tileX, tileY)] = type;

                if (type == TileTypeTeleport && entry.TryGetProperty("targetScene", out var ts))
                {
                    teleports[(tileX, tileY)] = new TeleportTileInfo
                    {
                        PairId = entry.TryGetProperty("pairId", out var pairId) ? pairId.GetInt32() : 0,
                        TargetScene = (ts.GetString() ?? "main").Trim().ToLowerInvariant(),
                        TargetX = (float)entry.GetProperty("targetX").GetDouble(),
                        TargetY = (float)entry.GetProperty("targetY").GetDouble(),
                    };
                }
            }

            _tileData[sceneName] = tiles;
            if (teleports.Count > 0)
                _teleportTargets[sceneName] = teleports;

            Logger.Info($"[TILE DATA] Carregado: {sceneName} ({tiles.Count} tiles, {teleports.Count} teleportes)");
        }

        ApplyTileBlocksToPathGrids();
    }

    private static string ResolveTileDataDirectory()
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string currentDir = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(exeDir, "data", "tiles"),
            Path.Combine(exeDir, "..", "..", "..", "data", "tiles"),
            Path.Combine(exeDir, "..", "..", "..", "..", "server", "data", "tiles"),
            Path.Combine(currentDir, "server", "data", "tiles"),
            Path.Combine(currentDir, "data", "tiles"),
        };

        foreach (string candidate in candidates)
        {
            string fullPath = Path.GetFullPath(candidate);
            if (Directory.Exists(fullPath) && Directory.GetFiles(fullPath, "*.json").Length > 0)
                return fullPath;
        }

        return Path.GetFullPath(Path.Combine(exeDir, "..", "..", "..", "data", "tiles"));
    }

    private void ApplyTileBlocksToPathGrids()
    {
        if (!_tileData.TryGetValue("main", out var mainTiles))
            return;

        var applied = new HashSet<PathfindingGrid>();
        int blockedCount = 0;

        foreach (var channel in _world.GetAllChannels())
        {
            if (channel.PathGrid == null || !applied.Add(channel.PathGrid))
                continue;

            foreach (var kvp in mainTiles)
            {
                if (!IsMobBlockedTileType(kvp.Value))
                    continue;

                ApplyTileBlockToPathGrid(channel.PathGrid, kvp.Key.X, kvp.Key.Y);
                blockedCount++;
            }
        }

        Logger.Info($"[TILE DATA] {blockedCount} tile(s) bloqueados aplicados ao pathfinding dos mobs/bosses.");
    }

    private static void ApplyTileBlockToPathGrid(PathfindingGrid grid, int tileX, int tileY)
    {
        float minX = tileX * MapTileSize;
        float minY = tileY * MapTileSize;
        float maxX = minX + MapTileSize - 0.001f;
        float maxY = minY + MapTileSize - 0.001f;

        var (minGx, minGy) = grid.WorldToGrid(minX, minY);
        var (maxGx, maxGy) = grid.WorldToGrid(maxX, maxY);

        int startGx = Math.Min(minGx, maxGx);
        int endGx = Math.Max(minGx, maxGx);
        int startGy = Math.Min(minGy, maxGy);
        int endGy = Math.Max(minGy, maxGy);

        for (int gy = startGy; gy <= endGy; gy++)
        {
            for (int gx = startGx; gx <= endGx; gx++)
                grid.SetBlocked(gx, gy, true);
        }
    }

    private void HandleMapEditorPlaceTile(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (session.SelectedCharacter == null) return;

        string sceneName = reader.GetString();
        int tileX = reader.GetInt();
        int tileY = reader.GetInt();
        byte type = reader.GetByte();
        bool remove = reader.GetBool();

        if (!_tileData.ContainsKey(sceneName))
            _tileData[sceneName] = new Dictionary<(int, int), byte>();

        var data = _tileData[sceneName];
        var key = (tileX, tileY);

        if (remove)
            data.Remove(key);
        else
            data[key] = type;

        foreach (var kvp in _sessions)
        {
            var s = kvp.Value;
            if (s.SelectedCharacter == null) continue;
            if (s.CurrentMap != sceneName) continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_MapEditorTileUpdate);
            writer.Put(sceneName);
            writer.Put(tileX);
            writer.Put(tileY);
            writer.Put(type);
            writer.Put(remove);
            kvp.Key.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void HandleMapEditorRequestTiles(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (session.SelectedCharacter == null) return;

        string sceneName = reader.GetString();

        if (!_tileData.TryGetValue(sceneName, out var tiles))
        {
            var emptyWriter = PacketSerializer.WritePacket(PacketId.S2C_MapEditorTileData);
            emptyWriter.Put(sceneName);
            emptyWriter.Put(0);
            peer.Send(emptyWriter, DeliveryMethod.ReliableOrdered);
            return;
        }

        var writer = PacketSerializer.WritePacket(PacketId.S2C_MapEditorTileData);
        writer.Put(sceneName);
        writer.Put(tiles.Count);
        foreach (var kvp in tiles)
        {
            writer.Put(kvp.Key.X);
            writer.Put(kvp.Key.Y);
            writer.Put(kvp.Value);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
