using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

[Tool]
public partial class ExportPvpZones : Node
{
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
        if (Engine.IsEditorHint()) return;
        Export();
        QueueFree();
    }

    private void Export()
    {
        var zones = new JsonArray();
        foreach (string scenePath in ScenesToExport)
            ExportScene(scenePath, zones);

        string projectDir = ProjectSettings.GlobalizePath("res://");
        string configPath = Path.GetFullPath(Path.Combine(projectDir, "server", "server_config.json"));

        if (!File.Exists(configPath))
        {
            GD.PrintErr($"[ExportPvpZones] ERRO: {configPath} nao encontrado");
            return;
        }

        string jsonText = File.ReadAllText(configPath);
        var config = JsonNode.Parse(jsonText)!.AsObject();
        config["PvpZones"] = zones;

        File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        GD.PrintRaw($"[ExportPvpZones] OK! {zones.Count} zona(s) exportada(s) para server_config.json\n");
    }

    private static void ExportScene(string scenePath, JsonArray zones)
    {
        var scene = ResourceLoader.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PrintErr($"[ExportPvpZones] Falha ao carregar: {scenePath}");
            return;
        }

        var instance = scene.Instantiate<Node>();
        string fallbackMap = Path.GetFileNameWithoutExtension(scenePath).ToLowerInvariant();
        var areas = new List<PvPZoneArea>();
        FindAreas(instance, areas);

        foreach (var area in areas)
        {
            Vector2[] points = area.GetWorldPolygon();
            if (points.Length < 3)
                continue;

            var serializedPoints = new JsonArray();
            foreach (Vector2 point in points)
            {
                serializedPoints.Add(new JsonObject
                {
                    ["X"] = point.X,
                    ["Y"] = point.Y,
                });
            }

            string mapName = string.IsNullOrWhiteSpace(area.MapName) ? fallbackMap : area.MapName.Trim().ToLowerInvariant();
            zones.Add(new JsonObject
            {
                ["Map"] = mapName,
                ["Type"] = (int)area.ZoneType,
                ["Points"] = serializedPoints,
            });
        }

        GD.PrintRaw($"[ExportPvpZones] {scenePath}: {areas.Count} zona(s)\n");
        instance.QueueFree();
    }

    private static void FindAreas(Node parent, List<PvPZoneArea> results)
    {
        if (parent is PvPZoneArea area)
            results.Add(area);

        foreach (var child in parent.GetChildren().OfType<Node>())
            FindAreas(child, results);
    }
}
