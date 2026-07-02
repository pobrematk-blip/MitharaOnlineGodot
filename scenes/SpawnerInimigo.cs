using Godot;

public partial class SpawnerInimigo : Godot.Timer
{
    [Export] public PackedScene CenaDoInim;
    [Export] public int MaxEnemies = 5;
    [Export] public int MaxTotalSpawns = 0;
    [Export] public bool Pausado { get; set; } = true;

    public override void _Ready()
    {
        Pausado = true;
        Stop();
        Timeout -= _OnTimeout;
        GD.Print("[SPAWNER] SpawnerInimigo local desativado. Monstros sao criados exclusivamente pelo servidor.");
    }

    public void _OnTimeout()
    {
        Pausado = true;
        Stop();
    }
}
