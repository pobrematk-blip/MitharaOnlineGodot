using Godot;

public partial class SpawnerManager : Node2D
{
    [Export] public PackedScene InimigoScene;
    [Export] public int MaxInimigos = 5;
    [Export] public float IntervalSpawn = 3.0f;
    [Export] public float RaioSpawn = 200.0f;

    private float _tempoProximoSpawn;
    private int _inimigosCriados = 0;

    public override void _Ready()
    {
        GD.Print($"[SPAWNER] Inicializado em {GlobalPosition}");
        GD.Print($"[SPAWNER] Max inimigos: {MaxInimigos}");
        GD.Print($"[SPAWNER] Intervalo: {IntervalSpawn}s");
        
        _tempoProximoSpawn = IntervalSpawn;
    }

    public override void _PhysicsProcess(double delta)
    {
        _tempoProximoSpawn -= (float)delta;

        if (_tempoProximoSpawn <= 0 && _inimigosCriados < MaxInimigos)
        {
            SpawnarInimigo();
            _tempoProximoSpawn = IntervalSpawn;
        }
    }

    private void SpawnarInimigo()
    {
        if (InimigoScene == null)
        {
            GD.PrintErr("[SPAWNER] InimigoScene não está configurada!");
            return;
        }

        // Gera posição aleatória dentro do raio
        Vector2 posicaoAleatoria = GlobalPosition + new Vector2(
            GD.Randf() * RaioSpawn - RaioSpawn / 2,
            GD.Randf() * RaioSpawn - RaioSpawn / 2
        );

        // Instancia o inimigo
        var inimigo = InimigoScene.Instantiate<Inimigo>();
        inimigo.GlobalPosition = posicaoAleatoria;
        
        // Adiciona à cena
        GetParent().AddChild(inimigo);
        
        _inimigosCriados++;
        GD.Print($"[SPAWNER] Inimigo #{_inimigosCriados} spawned em {posicaoAleatoria}");
    }

    public void ResetarSpawner()
    {
        _inimigosCriados = 0;
        _tempoProximoSpawn = IntervalSpawn;
        GD.Print("[SPAWNER] Reset!");
    }
}
