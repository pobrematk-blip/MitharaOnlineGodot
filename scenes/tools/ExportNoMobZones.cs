using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

[Tool]
public partial class ExportNoMobZones : Node
{
    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        Export();
    }

    private void Export()
    {
        GD.PrintRaw("[ExportNoMobZones] Carregando Main.tscn...\n");

        var mainScene = ResourceLoader.Load<PackedScene>("res://scenes/Main.tscn");
        if (mainScene == null)
        {
            GD.PrintErr("[ExportNoMobZones] ERRO: nao encontrou res://scenes/Main.tscn");
            QueueFree();
            return;
        }

        var main = mainScene.Instantiate();
        AddChild(main);

        var areas = new List<NoMobZoneArea>();
        FindAreas(main, areas);
        GD.PrintRaw($"[ExportNoMobZones] Encontrou {areas.Count} NoMobZoneArea(s)\n");

        if (areas.Count == 0)
        {
            GD.PrintRaw("[ExportNoMobZones] Nenhuma area encontrada. Nada exportado.\n");
            main.QueueFree();
            QueueFree();
            return;
        }

        var zones = new JsonArray();
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

            zones.Add(new JsonObject { ["Points"] = serializedPoints });
            GD.PrintRaw($"  Zona poligonal: {points.Length} ponto(s)\n");
        }

        string projectDir = ProjectSettings.GlobalizePath("res://");
        string configPath = Path.GetFullPath(Path.Combine(projectDir, "server", "server_config.json"));

        if (!File.Exists(configPath))
        {
            GD.PrintErr($"[ExportNoMobZones] ERRO: {configPath} nao encontrado");
            main.QueueFree();
            QueueFree();
            return;
        }

        string jsonText = File.ReadAllText(configPath);
        var config = JsonNode.Parse(jsonText)!.AsObject();
        config["NoMobZones"] = zones;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(configPath, config.ToJsonString(options));

        GD.PrintRaw($"[ExportNoMobZones] OK! {zones.Count} zona(s) exportada(s) para server_config.json\n");

        main.QueueFree();
        QueueFree();
    }

    private static void FindAreas(Node parent, List<NoMobZoneArea> results)
    {
        if (parent is NoMobZoneArea area)
            results.Add(area);

        foreach (var child in parent.GetChildren().OfType<Node>())
            FindAreas(child, results);
    }
}
