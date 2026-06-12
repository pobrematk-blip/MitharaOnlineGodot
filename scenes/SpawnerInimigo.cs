using Godot;
using System;

public partial class SpawnerInimigo : Timer
{
    [Export] public PackedScene CenaDoInim;
    [Export] public int MaxEnemies = 5;
    [Export] public int MaxTotalSpawns = 0;
    [Export] public bool Pausado { get; set; } = true;

    private CharacterBody2D _player;
    private bool _inicializadoComSucesso = false;
    private int _totalSpawned = 0;

    public override void _Ready()
    {
        GD.Print("========================================");
        GD.Print("[SISTEMA CRÍTICO] SPAWNER ACORDOU NA MEMÓRIA!");
        GD.Print("========================================");

        var network = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (network != null && network.IsConnected)
        {
            Pausado = true;
            Stop();
            GD.Print("[SPAWNER] Servidor detectado! Spawner local desativado.");
            return;
        }

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
    }

    public void _OnTimeout()
    {
        if (Pausado) return;

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

        if (inimigosVivos >= MaxEnemies)
        {
            GD.Print($"[SPAWNER] ✘ LIMITE JÁ ATINGIDO ({inimigosVivos}/{MaxEnemies}). MANTENDO APENAS OS {MaxEnemies}.");
            return; 
        }

        GD.Print($"[SPAWNER] ✓ Spawnando novo inimigo (Faltam {MaxEnemies - inimigosVivos} para o máximo)");
        SpawnarUmInimigo();
    }

    private void SpawnarUmInimigo()
    {
        try
        {
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

            const int MAX_TENTATIVAS = 8;
            const float DISTANCIA_MINIMA = 100f;
            Vector2 posicaoFinal = Vector2.Zero;
            bool posicaoValida = false;

            var inimigosExistentes = GetTree()?.GetNodesInGroup("Inimigos");

            for (int tentativa = 0; tentativa < MAX_TENTATIVAS; tentativa++)
            {
                float offsetX = (float)GD.RandRange(130, 280) * (GD.Randf() > 0.5f ? 1 : -1);
                float offsetY = (float)GD.RandRange(130, 280) * (GD.Randf() > 0.5f ? 1 : -1);
                posicaoFinal = _player.GlobalPosition + new Vector2(offsetX, offsetY);

                if (inimigosExistentes == null || inimigosExistentes.Count == 0)
                {
                    posicaoValida = true;
                    break;
                }

                bool muitoPerto = false;
                foreach (Node e in inimigosExistentes)
                {
                    if (e is Node2D e2d && e != novoInimigo)
                    {
                        if (e2d.GlobalPosition.DistanceTo(posicaoFinal) < DISTANCIA_MINIMA)
                        {
                            muitoPerto = true;
                            break;
                        }
                    }
                }

                if (!muitoPerto)
                {
                    posicaoValida = true;
                    break;
                }
            }

            novoInimigo.GlobalPosition = posicaoValida ? posicaoFinal : _player.GlobalPosition + new Vector2(300, 300);

            parent.AddChild(novoInimigo);

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