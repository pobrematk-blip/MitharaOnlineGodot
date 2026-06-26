using Godot;
using System.Collections.Generic;
using System.IO;
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
        instance.QueueFree();

        if (markers.Count == 0)
        {
            GD.Print($"[TILE EXPORT] {scenePath}: 0 marcadores (ignorado)");
            return;
        }

        var output = new List<Dictionary<string, object>>();
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

            output.Add(entry);
        }

        string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath).ToLower();
        string jsonPath = System.IO.Path.Combine(outputDir, $"{sceneName}.json");
        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(jsonPath, json);
        GD.Print($"[TILE EXPORT] {scenePath}: {markers.Count} marcadores -> {jsonPath}");
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
}
