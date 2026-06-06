namespace Mithara.Server;

public class ServerConfig
{
    public int Port { get; set; } = 7777;
    public string BindAddress { get; set; } = "0.0.0.0";
    public int MaxConnections { get; set; } = 500;
    public int ChannelCount { get; set; } = 4;
    public int TickRate { get; set; } = 20;
    public float AoiRadius { get; set; } = 600f;
    public string DbPath { get; set; } = "data/mithara.db";
    public bool AdminMode { get; set; }
    public string AdminAccounts { get; set; } = "";
}
