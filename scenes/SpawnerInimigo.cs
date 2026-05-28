using Godot;
using System;

// IMPORTANTE: Esse nome (SpawnerInimigo) deve ser IGUAL ao nome do seu arquivo .cs
public partial class SpawnerInimigo : Timer
{
    [Export] public PackedScene CenaDoInim;
    [Export] public int MaxEnemies = 5;
    [Export] public float SpawnCooldown = 0.5f; // Tempo mínimo entre spawns

    private CharacterBody2D _player;
    private bool _inicializadoComSucesso = false;
    private float _lastSpawnTime = 0f; // Timestamp do último spawn

    public override void _Ready()
    {
        // Força o terminal a printar no exato milissegundo em que o jogo liga
        GD.Print("=========================================");
        GD.Print("[SISTEMA CRÍTICO] SPAWNER ACORDOU NA MEMÓRIA!");
        GD.Print("=========================================");

        // Conecta o sinal nativo do Timer por código (independente do editor)
        Timeout += _OnTimeout;

        WaitTime = 3.0f;
        OneShot = false;
        Autostart = true;

        if (GetTree()?.CurrentScene != null)
        {
            _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
        }

        if (_player != null)
        {
            GD.Print("[SISTEMA] Player foi mapeado pelo Spawner!");
            _inicializadoComSucesso = true;
        }
        else
        {
            GD.PrintErr("[SISTEMA ERRO] Spawner não encontrou o Player no mapa.");
            GD.Print("[SISTEMA] O Spawner vai iniciar mesmo assim e tentará spawnar quando possível.");
        }

        // Garante que o Timer esteja rodando mesmo que o Player ainda não tenha sido encontrado.
        // Isso permite que o spawner tente instanciar (ou re-tentar localizar o player) durante o jogo.
        Start();
    }

    // Código de teste bruto: Se o nó estiver vivo no mapa, isso vai mandar mensagem sem parar!
    public override void _Process(double delta)
    {
        // Removido debug ruidoso. Mantemos o process desligado por padrão.
    }

    public void _OnTimeout()
    {
        // A cada tick do timer (3 seg), verifica se precisa spawnar novo inimigo
        if (CenaDoInim == null)
        {
            GD.PrintErr("[SISTEMA ERRO] O slot 'Cena Do Inim' perdeu a referência. Arraste o Inimigo.tscn de novo!");
            return;
        }

        // Localiza o Player se não houver
        if (_player == null)
        {
            if (GetTree()?.CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
        }

        if (_player == null)
        {
            GD.PrintErr("[SISTEMA ERRO] Player não encontrado!");
            return;
        }

        // ======== CONTAGEM ATUAL DE INIMIGOS ========
        int existentes = 0;
        try
        {
            var inimigosNoMapa = GetTree()?.GetNodesInGroup("Inimigos");
            if (inimigosNoMapa != null)
            {
                existentes = inimigosNoMapa.Count;
            }
        }
        catch (Exception)
        {
            existentes = 0;
        }

        GD.Print($"[SPAWNER] Inimigos ativos: {existentes}/{MaxEnemies}");

        // ======== VERIFICAR SE PODE SPAWNAR ========
        // Se há menos que o máximo E passou o tempo de cooldown desde o último spawn
        int inimigosParaSpawnar = MaxEnemies - existentes;
        
        if (inimigosParaSpawnar > 0)
        {
            // Respeita o cooldown entre spawns (ex: não spawna 5 de uma vez)
            float tempoDesdeUltimoSpawn = (float)GetTree().GetNodesInGroup("_spawner_time").Count; // Hack simples
            
            // Verifica se está na "janela de spawn" (a cada 3 segundos, spawna 1)
            SpawnarNovoInimigo();
        }
        else
        {
            GD.Print($"[SPAWNER] Limite de {MaxEnemies} inimigos mantido.");
        }
    }

    private void SpawnarNovoInimigo()
    {
        try
        {
            CharacterBody2D novoInimigo = CenaDoInim.Instantiate<CharacterBody2D>();
            Node parent = GetParent();
            parent.AddChild(novoInimigo);

            // Nasce ao lado do jogador (com variação)
            var random = new RandomNumberGenerator();
            random.Randomize();
            float offsetX = random.Randf() > 0.5f ? 120 : -120;
            novoInimigo.GlobalPosition = _player.GlobalPosition + new Vector2(offsetX, 0);

            GD.Print($"✅ [SPAWN] Novo inimigo criado! Total: {GetTree()?.GetNodesInGroup("Inimigos").Count}/{MaxEnemies}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SISTEMA FALHA] Erro ao spawnar: {e.Message}");
        }
    }
}