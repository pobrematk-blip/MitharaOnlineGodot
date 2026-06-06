using System.Text.Json;

namespace Mithara.Server;

public static class ConfigLoader
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "server_config.json");

    public static ServerConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<ServerConfig>(json) ?? new ServerConfig();
            }
            catch
            {
                Console.WriteLine("[CONFIG] Falha ao ler config, usando padrão.");
            }
        }
        var config = new ServerConfig();
        Save(config);
        return config;
    }

    public static void Save(ServerConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
