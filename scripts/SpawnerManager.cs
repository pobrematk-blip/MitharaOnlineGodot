using Godot;

public partial class SpawnerManager : Node2D
{
    [Export] public PackedScene InimigoScene;
    [Export] public int MaxInimigos = 5;
    [Export] public float IntervalSpawn = 3.0f;
    [Export] public float RaioSpawn = 200.0f;

    public override void _Ready()
    {
        SetPhysicsProcess(false);
        GD.Print("[SPAWNER] SpawnerManager local desativado. Monstros sao autoridade do servidor.");
    }

    public void ResetarSpawner()
    {
        GD.Print("[SPAWNER] Reset ignorado: spawner local desativado.");
    }
}
