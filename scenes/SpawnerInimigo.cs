using Godot;
using System;

public partial class SpawnerInimigo : Timer
{
    [Export] public PackedScene CenaDoInim;
    [Export] public int MaxEnemies = 5;
    [Export] public int MaxTotalSpawns = 0; // 0 = infinito
    [Export] public bool Pausado { get; set; } = true;

    private CharacterBody2D _player;
    private bool _inicializadoComSucesso = false;
    private int _totalSpawned = 0;

    public override void _Ready()
    {
        GD.Print("=========================================");
        GD.Print("[SISTEMA CRÍTICO] SPAWNER ACORDOU NA MEMÓRIA!");
        GD.Print("=========================================");

        // Conecta o sinal nativo do Timer por código
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
            GD.PrintErr("[SISTEMA ERRO] Spawner não encontrou o Player no mapa. Tentará novamente no ciclo.");
        }

        Start();
    }

    public override void _Process(double delta)
    {
        // Mantemos desativado por padrão
    }

    public void _OnTimeout()
    {
        if (Pausado) return;

        // Limite de spawn total
        if (MaxTotalSpawns > 0 && _totalSpawned >= MaxTotalSpawns)
        {
            Pausado = true;
            Stop();
            GD.Print($"[SPAWNER] Limite total de {MaxTotalSpawns} spawns atingido. Parando.");
            return;
        }

        if (CenaDoInim == null)
        {
            GD.PrintErr("[SPAWNER] ERRO: CenaDoInim não foi setada no Inspetor!");
            return;
        }

        // Tenta remapear o Player caso ele tenha nascido atrasado
        if (_player == null)
        {
            if (GetTree()?.CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
            
            if (_player == null)
            {
                GD.PrintErr("[SPAWNER] ERRO: Player não encontrado! Abortando ciclo.");
                return;
            }
        }

        // ======== CONTAR INIMIGOS VIVOS (CHECK RIGOROSO) ========
        int inimigosVivos = 0;
        try
        {
            var grupoInimigos = GetTree()?.GetNodesInGroup("Inimigos");
            inimigosVivos = grupoInimigos?.Count ?? 0;
        }
        catch
        {
            inimigosVivos = 0;
        }

        GD.Print($"[SPAWNER] TICK - Total de inimigos vivos: {inimigosVivos}/{MaxEnemies}");

        // ======== VERIFICAÇÃO RIGOROSA: TRAVA NO LIMITE ========
        if (inimigosVivos >= MaxEnemies)
        {
            GD.Print($"[SPAWNER] ✘ LIMITE JÁ ATINGIDO ({inimigosVivos}/{MaxEnemies}). MANTENDO APENAS OS 5.");
            return; 
        }

        // ======== SPAWNAR 1 INIMIGO ========
        GD.Print($"[SPAWNER] ✓ Spawnando novo inimigo (Faltam {MaxEnemies - inimigosVivos} para o máximo)");
        SpawnarUmInimigo();
    }

    private void SpawnarUmInimigo()
    {
        try
        {
            // Double check de segurança antes de instanciar
            int verificacaoFinal = 0;
            try
            {
                verificacaoFinal = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
            }
            catch { }

            if (verificacaoFinal >= MaxEnemies)
            {
                GD.PrintErr($"[SPAWNER] BLOQUEADO! Já tem {verificacaoFinal} inimigos. Cancelando spawn.");
                return;
            }

            CharacterBody2D novoInimigo = CenaDoInim.Instantiate<CharacterBody2D>();
            
            if (novoInimigo == null)
            {
                GD.PrintErr("[SPAWNER] ERRO: Instantiate retornou null!");
                return;
            }

            Node parent = GetParent();
            if (parent == null)
            {
                GD.PrintErr("[SPAWNER] ERRO: Parent é null!");
                return;
            }

            // MELHORIA DE POSIÇÃO: Sorteia posições em X e Y ao redor do player
            // POSICIONAR ANTES DO AddChild para que Inimigo._Ready() já veja a posição correta
            float offsetX = (float)GD.RandRange(130, 200) * (GD.Randf() > 0.5f ? 1 : -1);
            float offsetY = (float)GD.RandRange(130, 200) * (GD.Randf() > 0.5f ? 1 : -1);
            novoInimigo.GlobalPosition = _player.GlobalPosition + new Vector2(offsetX, offsetY);

            // Adiciona à cena (o próprio Inimigo._Ready() se encarrega de AddToGroup)
            parent.AddChild(novoInimigo);

            // Avisa o inimigo da posição final de spawn (para patrulha correta)
            if (novoInimigo is Inimigo inimigo)
                inimigo.DefinirPosicaoInicial(novoInimigo.GlobalPosition);
            
            _totalSpawned++;
            
            int totalAposSpawn = 0;
            try
            {
                totalAposSpawn = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
            }
            catch { }

            GD.Print($"✓ [SPAWNER] Novo inimigo criado! Total agora: {totalAposSpawn}/{MaxEnemies}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SPAWNER] ERRO ao spawnar: {e}");
        }
    }
}