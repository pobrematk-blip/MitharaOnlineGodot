using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

[Tool]
public partial class ExportTileData : Node
{
    private const float MapTileSize = 16f;

    private sealed class SceneExportData
    {
        public string ScenePath { get; set; } = "";
        public string SceneName { get; set; } = "";
        public List<TileMarker> Markers { get; set; } = new();
        public HashSet<(int X, int Y)> BlockedTiles { get; set; } = new();
        public HashSet<(int X, int Y)> MobBlockedTiles { get; set; } = new();
        public List<TeleportPairPoint> PairTeleports { get; set; } = new();
    }

    private sealed class TeleportPairPoint
    {
        public int PairId { get; set; }
        public string SceneName { get; set; } = "";
        public int TileX { get; set; }
        public int TileY { get; set; }
        public Vector2 Destination { get; set; }
    }

    private static readonly string[] ScenesToExport =
    {
        "res://scenes/Main.tscn",
        "res://scenes/Interiors/Alfaiataria.tscn",
        "res://scenes/Interiors/Taverna.tscn",
        "res://scenes/Interiors/Banco.tscn",
        "res://scenes/Interiors/Igreja.tscn",
        "res://scenes/Interiors/FerrariaEArtesao.tscn",
        "res://scenes/Interiors/AlquimistasECozinheiro.tscn",
        "res://scenes/Interiors/GuildaDosAventureiros.tscn",
        "res://scenes/Interiors/Prefeitura.tscn",
        "res://scenes/Interiors/PetsMontariasECompanias.tscn",
        "res://scenes/Interiors/PoraoDaIgreja.tscn",
        "res://scenes/Interiors/CavernaDosPerdidos.tscn",
        "res://scenes/Interiors/DungeonDoAnciao.tscn",
    };

    public override void _Ready()
    {
        if (Engine.IsEditorHint() && !OS.GetCmdlineArgs().Contains("--export-tile-data")) return;
        Export();
        GD.Print("[TILE EXPORT] Concluido!");
        GetTree().Quit();
    }

    private void Export()
    {
        string projectDir = ProjectSettings.GlobalizePath("res://");
        string serverTilesDir = Path.Combine(projectDir, "server", "data", "tiles");
        DirAccess.MakeDirRecursiveAbsolute(serverTilesDir);

        var scenes = new List<SceneExportData>();
        foreach (string scenePath in ScenesToExport)
        {
            var data = CollectSceneData(scenePath);
            if (data != null)
                scenes.Add(data);
        }

        var pairedTeleports = BuildPairedTeleports(scenes);
        foreach (var sceneData in scenes)
            ExportScene(sceneData, serverTilesDir, pairedTeleports);
    }

    private SceneExportData CollectSceneData(string scenePath)
    {
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PrintErr($"[TILE EXPORT] Falha ao carregar: {scenePath}");
            return null;
        }

        var instance = scene.Instantiate<Node>();
        string sceneName = Path.GetFileNameWithoutExtension(scenePath).ToLower();
        var tileBlocks = FindTileBlocks(instance);
        var data = new SceneExportData
        {
            ScenePath = scenePath,
            SceneName = sceneName,
            Markers = FindMarkers(instance),
            BlockedTiles = tileBlocks.FullBlocks,
            MobBlockedTiles = tileBlocks.MobBlocks,
            PairTeleports = FindTeleportPairs(instance, sceneName),
        };
        instance.QueueFree();
        return data;
    }

    private Dictionary<string, Dictionary<(int X, int Y), Dictionary<string, object>>> BuildPairedTeleports(List<SceneExportData> scenes)
    {
        var result = new Dictionary<string, Dictionary<(int X, int Y), Dictionary<string, object>>>();
        var allPairs = scenes.SelectMany(s => s.PairTeleports).GroupBy(p => p.PairId);

        foreach (var pair in allPairs)
        {
            var points = pair.ToList();
            if (points.Count != 2)
            {
                GD.PrintErr($"[TILE EXPORT] Teleporte PairId={pair.Key} precisa ter exatamente 2 marcadores. Encontrado: {points.Count}");
                continue;
            }

            AddPairTeleport(result, points[0], points[1]);
            AddPairTeleport(result, points[1], points[0]);
        }

        return result;
    }

    private static void AddPairTeleport(
        Dictionary<string, Dictionary<(int X, int Y), Dictionary<string, object>>> result,
        TeleportPairPoint from,
        TeleportPairPoint to)
    {
        if (!result.TryGetValue(from.SceneName, out var sceneTeleports))
        {
            sceneTeleports = new Dictionary<(int X, int Y), Dictionary<string, object>>();
            result[from.SceneName] = sceneTeleports;
        }

        sceneTeleports[(from.TileX, from.TileY)] = new Dictionary<string, object>
        {
            { "tileX", from.TileX },
            { "tileY", from.TileY },
            { "tileSize", MapTileSize },
            { "type", (int)TileMarker.TileType.Teleport },
            { "targetScene", to.SceneName },
            { "targetX", to.Destination.X },
            { "targetY", to.Destination.Y },
            { "pairId", from.PairId },
            { "source", "teleport_pair" },
        };
    }

    private void ExportScene(
        SceneExportData sceneData,
        string outputDir,
        Dictionary<string, Dictionary<(int X, int Y), Dictionary<string, object>>> pairedTeleports)
    {
        if (sceneData.Markers.Count == 0
            && sceneData.BlockedTiles.Count == 0
            && sceneData.MobBlockedTiles.Count == 0
            && sceneData.PairTeleports.Count == 0)
        {
            GD.Print($"[TILE EXPORT] {sceneData.ScenePath}: 0 marcadores/colisoes (ignorado)");
            return;
        }

        var outputByTile = new Dictionary<(int X, int Y), Dictionary<string, object>>();
        foreach (var tile in sceneData.MobBlockedTiles)
        {
            outputByTile[tile] = new Dictionary<string, object>
            {
                { "tileX", tile.X },
                { "tileY", tile.Y },
                { "tileSize", MapTileSize },
                { "type", (int)TileMarker.TileType.Npc },
                { "source", "layer_npc_void" },
            };
        }

        foreach (var tile in sceneData.BlockedTiles)
        {
            outputByTile[tile] = new Dictionary<string, object>
            {
                { "tileX", tile.X },
                { "tileY", tile.Y },
                { "tileSize", MapTileSize },
                { "type", (int)TileMarker.TileType.Block },
                { "source", "godot_collision_or_block_layer" },
            };
        }

        foreach (var marker in sceneData.Markers)
        {
            int tileX = Mathf.FloorToInt(marker.GlobalPosition.X / MapTileSize);
            int tileY = Mathf.FloorToInt(marker.GlobalPosition.Y / MapTileSize);

            var entry = new Dictionary<string, object>
            {
                { "tileX", tileX },
                { "tileY", tileY },
                { "tileSize", MapTileSize },
                { "type", (int)marker.Type },
            };

            if (marker.Type == TileMarker.TileType.Teleport)
            {
                entry["targetScene"] = marker.TargetScene;
                entry["targetX"] = marker.TargetX;
                entry["targetY"] = marker.TargetY;
            }

            outputByTile[(tileX, tileY)] = entry;
        }

        if (pairedTeleports.TryGetValue(sceneData.SceneName, out var sceneTeleports))
        {
            foreach (var kvp in sceneTeleports)
                outputByTile[kvp.Key] = kvp.Value;
        }

        string jsonPath = Path.Combine(outputDir, $"{sceneData.SceneName}.json");
        var output = outputByTile
            .OrderBy(kvp => kvp.Key.Y)
            .ThenBy(kvp => kvp.Key.X)
            .Select(kvp => kvp.Value)
            .ToList();
        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        GD.Print($"[TILE EXPORT] {sceneData.ScenePath}: {sceneData.Markers.Count} marcadores, {sceneData.BlockedTiles.Count} bloqueios totais, {sceneData.MobBlockedTiles.Count} bloqueios de mobs, {sceneData.PairTeleports.Count} teleportes de casal -> {jsonPath}");
    }

    private List<TileMarker> FindMarkers(Node node)
    {
        var result = new List<TileMarker>();
        if (node is TileMarker marker)
            result.Add(marker);

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
                result.AddRange(FindMarkers(childNode));
        }

        return result;
    }

    private List<TeleportPairPoint> FindTeleportPairs(Node node, string sceneName)
    {
        var result = new List<TeleportPairPoint>();
        CollectTeleportPairs(node, sceneName, result);
        return result;
    }

    private void CollectTeleportPairs(Node node, string sceneName, List<TeleportPairPoint> result)
    {
        if (node is TeleportPairMarker marker)
        {
            result.Add(new TeleportPairPoint
            {
                PairId = marker.PairId,
                SceneName = sceneName,
                TileX = marker.GetTileX(),
                TileY = marker.GetTileY(),
                Destination = marker.GetDestinationPosition(),
            });
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
                CollectTeleportPairs(childNode, sceneName, result);
        }
    }

    private (HashSet<(int X, int Y)> FullBlocks, HashSet<(int X, int Y)> MobBlocks) FindTileBlocks(Node node)
    {
        var fullBlocks = new HashSet<(int X, int Y)>();
        var mobBlocks = new HashSet<(int X, int Y)>();
        CollectTileBlocks(node, fullBlocks, mobBlocks);
        return (fullBlocks, mobBlocks);
    }

    private void CollectTileBlocks(Node node, HashSet<(int X, int Y)> fullBlocks, HashSet<(int X, int Y)> mobBlocks)
    {
        if (node is TileMapLayer layer)
        {
            if (IsLayerNamed(layer, "Block"))
                AddTileMapUsedCells(layer, fullBlocks);
            else if (IsLayerNamed(layer, "NpcVoid", "Npc Void", "Npc_Void", "MobVoid", "MobBlock"))
                AddTileMapUsedCells(layer, mobBlocks);
            else
                AddTileMapCollisionTiles(layer, fullBlocks);
        }
        else if (node is CollisionShape2D shape)
            AddCollisionShapeTiles(shape, fullBlocks);
        else if (node is CollisionPolygon2D polygon)
            AddCollisionPolygonTiles(polygon, fullBlocks);

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
                CollectTileBlocks(childNode, fullBlocks, mobBlocks);
        }
    }

    private static bool IsLayerNamed(Node node, params string[] names)
    {
        string current = NormalizeLayerName(node.Name.ToString());
        foreach (string name in names)
        {
            if (current == NormalizeLayerName(name))
                return true;
        }

        return false;
    }

    private static string NormalizeLayerName(string name)
    {
        return name
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .ToLowerInvariant();
    }

    private void AddTileMapCollisionTiles(TileMapLayer layer, HashSet<(int X, int Y)> result)
    {
        foreach (Vector2I cell in layer.GetUsedCells())
        {
            TileData data = layer.GetCellTileData(cell);
            if (data == null || !TileHasCollision(data))
                continue;

            Vector2 world = layer.ToGlobal(layer.MapToLocal(cell));
            AddTileAtWorld(world, result);
        }
    }

    private void AddTileMapUsedCells(TileMapLayer layer, HashSet<(int X, int Y)> result)
    {
        foreach (Vector2I cell in layer.GetUsedCells())
        {
            Vector2 world = layer.ToGlobal(layer.MapToLocal(cell));
            AddTileAtWorld(world, result);
        }
    }

    private static bool TileHasCollision(TileData data)
    {
        for (int layer = 0; layer < 8; layer++)
        {
            try
            {
                if (data.GetCollisionPolygonsCount(layer) > 0)
                    return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private void AddCollisionShapeTiles(CollisionShape2D shape, HashSet<(int X, int Y)> result)
    {
        if (shape.Disabled || shape.Shape == null || !IsStaticWorldCollision(shape))
            return;

        Rect2 bounds = GetShapeBounds(shape);
        AddBoundsTiles(bounds, result);
    }

    private void AddCollisionPolygonTiles(CollisionPolygon2D polygon, HashSet<(int X, int Y)> result)
    {
        if (polygon.Disabled || polygon.Polygon.Length == 0 || !IsStaticWorldCollision(polygon))
            return;

        Rect2 bounds = GetPolygonBounds(polygon);
        AddBoundsTiles(bounds, result);
    }

    private static bool IsStaticWorldCollision(Node node)
    {
        Node current = node.GetParent();
        while (current != null)
        {
            if (current is StaticBody2D)
                return true;
            if (current is Area2D || current is CharacterBody2D)
                return false;
            current = current.GetParent();
        }
        return false;
    }

    private static Rect2 GetShapeBounds(CollisionShape2D shape)
    {
        Vector2 half = shape.Shape switch
        {
            RectangleShape2D rect => rect.Size * 0.5f,
            CircleShape2D circle => new Vector2(circle.Radius, circle.Radius),
            CapsuleShape2D capsule => new Vector2(capsule.Radius, capsule.Height * 0.5f),
            _ => new Vector2(16f, 16f),
        };

        Vector2 center = shape.GlobalPosition;
        return new Rect2(center - half, half * 2f);
    }

    private static Rect2 GetPolygonBounds(CollisionPolygon2D polygon)
    {
        Vector2 first = polygon.ToGlobal(polygon.Polygon[0]);
        float minX = first.X;
        float maxX = first.X;
        float minY = first.Y;
        float maxY = first.Y;

        foreach (Vector2 point in polygon.Polygon)
        {
            Vector2 world = polygon.ToGlobal(point);
            minX = Mathf.Min(minX, world.X);
            maxX = Mathf.Max(maxX, world.X);
            minY = Mathf.Min(minY, world.Y);
            maxY = Mathf.Max(maxY, world.Y);
        }

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private static void AddBoundsTiles(Rect2 bounds, HashSet<(int X, int Y)> result)
    {
        int minX = Mathf.FloorToInt(bounds.Position.X / MapTileSize);
        int minY = Mathf.FloorToInt(bounds.Position.Y / MapTileSize);
        int maxX = Mathf.FloorToInt((bounds.Position.X + bounds.Size.X) / MapTileSize);
        int maxY = Mathf.FloorToInt((bounds.Position.Y + bounds.Size.Y) / MapTileSize);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
                result.Add((x, y));
        }
    }

    private static void AddTileAtWorld(Vector2 world, HashSet<(int X, int Y)> result)
    {
        int tileX = Mathf.FloorToInt(world.X / MapTileSize);
        int tileY = Mathf.FloorToInt(world.Y / MapTileSize);
        result.Add((tileX, tileY));
    }
}
