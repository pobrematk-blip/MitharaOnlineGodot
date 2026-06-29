using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

[Tool]
public partial class ExportTileData : Node
{
    private static readonly string[] ScenesToExport = new[]
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
        if (Engine.IsEditorHint()) return;
        Export();
        GD.Print("[TILE EXPORT] Concluido!");
        GetTree().Quit();
    }

    private void Export()
    {
        string projectDir = ProjectSettings.GlobalizePath("res://");
        string serverTilesDir = System.IO.Path.Combine(projectDir, "server", "data", "tiles");
        DirAccess.MakeDirRecursiveAbsolute(serverTilesDir);

        foreach (string scenePath in ScenesToExport)
        {
            ExportScene(scenePath, serverTilesDir);
        }
    }

    private void ExportScene(string scenePath, string outputDir)
    {
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PrintErr($"[TILE EXPORT] Falha ao carregar: {scenePath}");
            return;
        }

        var instance = scene.Instantiate<Node>();
        var markers = FindMarkers(instance);
        var blockedTiles = FindBlockedTiles(instance);
        instance.QueueFree();

        if (markers.Count == 0 && blockedTiles.Count == 0)
        {
            GD.Print($"[TILE EXPORT] {scenePath}: 0 marcadores/colisoes (ignorado)");
            return;
        }

        var outputByTile = new Dictionary<(int X, int Y), Dictionary<string, object>>();
        foreach (var tile in blockedTiles)
        {
            outputByTile[tile] = new Dictionary<string, object>
            {
                { "tileX", tile.X },
                { "tileY", tile.Y },
                { "type", (int)TileMarker.TileType.Block },
                { "source", "godot_collision" }
            };
        }

        foreach (var m in markers)
        {
            int tileX = Mathf.FloorToInt(m.Position.X / 32f);
            int tileY = Mathf.FloorToInt(m.Position.Y / 32f);

            var entry = new Dictionary<string, object>
            {
                { "tileX", tileX },
                { "tileY", tileY },
                { "type", (int)m.Type }
            };

            if (m.Type == TileMarker.TileType.Teleport)
            {
                entry["targetScene"] = m.TargetScene;
                entry["targetX"] = m.TargetX;
                entry["targetY"] = m.TargetY;
            }

            outputByTile[(tileX, tileY)] = entry;
        }

        string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath).ToLower();
        string jsonPath = System.IO.Path.Combine(outputDir, $"{sceneName}.json");
        var output = outputByTile
            .OrderBy(kvp => kvp.Key.Y)
            .ThenBy(kvp => kvp.Key.X)
            .Select(kvp => kvp.Value)
            .ToList();
        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(jsonPath, json);
        GD.Print($"[TILE EXPORT] {scenePath}: {markers.Count} marcadores, {blockedTiles.Count} tiles de colisao -> {jsonPath}");
    }

    private List<TileMarker> FindMarkers(Node node)
    {
        var result = new List<TileMarker>();
        if (node is TileMarker tm)
            result.Add(tm);
        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
                result.AddRange(FindMarkers(childNode));
        }
        return result;
    }

    private HashSet<(int X, int Y)> FindBlockedTiles(Node node)
    {
        var result = new HashSet<(int X, int Y)>();
        CollectBlockedTiles(node, result);
        return result;
    }

    private void CollectBlockedTiles(Node node, HashSet<(int X, int Y)> result)
    {
        if (node is TileMapLayer layer)
            AddTileMapCollisionTiles(layer, result);
        else if (node is CollisionShape2D shape)
            AddCollisionShapeTiles(shape, result);
        else if (node is CollisionPolygon2D polygon)
            AddCollisionPolygonTiles(polygon, result);

        foreach (var child in node.GetChildren())
        {
            if (child is Node childNode)
                CollectBlockedTiles(childNode, result);
        }
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
        int minX = Mathf.FloorToInt(bounds.Position.X / 32f);
        int minY = Mathf.FloorToInt(bounds.Position.Y / 32f);
        int maxX = Mathf.FloorToInt((bounds.Position.X + bounds.Size.X) / 32f);
        int maxY = Mathf.FloorToInt((bounds.Position.Y + bounds.Size.Y) / 32f);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
                result.Add((x, y));
        }
    }

    private static void AddTileAtWorld(Vector2 world, HashSet<(int X, int Y)> result)
    {
        int tileX = Mathf.FloorToInt(world.X / 32f);
        int tileY = Mathf.FloorToInt(world.Y / 32f);
        result.Add((tileX, tileY));
    }
}
