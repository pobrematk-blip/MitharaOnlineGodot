using Godot;
using System.Collections.Generic;
using System.IO;
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

		var markers = new List<NoMobZoneMarker>();
		FindMarkers(main, markers);
		GD.PrintRaw($"[ExportNoMobZones] Encontrou {markers.Count} NoMobZoneMarker(s)\n");

		if (markers.Count == 0)
		{
			GD.PrintRaw("[ExportNoMobZones] Nenhum marcador encontrado. Nada exportado.\n");
			main.QueueFree();
			QueueFree();
			return;
		}

		var zones = new JsonArray();
		foreach (var m in markers)
		{
			var rect = m.GetZoneRect();
			var obj = new JsonObject
			{
				["X"] = (int)rect.Position.X,
				["Y"] = (int)rect.Position.Y,
				["Width"] = (int)rect.Size.X,
				["Height"] = (int)rect.Size.Y,
			};
			zones.Add(obj);
			GD.PrintRaw($"  Zona: X={rect.Position.X:F0} Y={rect.Position.Y:F0} W={rect.Size.X:F0} H={rect.Size.Y:F0}\n");
		}

		string projectDir = ProjectSettings.GlobalizePath("res://");
		string configPath = Path.GetFullPath(Path.Combine(projectDir, "..", "server", "server_config.json"));

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

		GD.PrintRaw($"[ExportNoMobZones] OK! {markers.Count} zona(s) exportada(s) para server_config.json\n");

		main.QueueFree();
		QueueFree();
	}
	private static void FindMarkers(Node parent, List<NoMobZoneMarker> results)
	{
		if (parent is NoMobZoneMarker marker)
		{
			results.Add(marker);
			return;
		}
		foreach (var child in parent.GetChildren())
			FindMarkers(child, results);
	}
}
