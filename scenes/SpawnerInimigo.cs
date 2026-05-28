using Godot;
using System;

// IMPORTANTE: Esse nome (SpawnerInimigo) deve ser IGUAL ao nome do seu arquivo .cs
public partial class SpawnerInimigo : Timer
{
    [Export] public PackedScene CenaDoInim;
    [Export] public int MaxEnemies = 5;

    private CharacterBody2D _player;
    private bool _inicializadoComSucesso = false;
    private float _tempoDecorrido = 0f; // Para contar tempo de spawn inicial

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
        // A cada 3 segundos, verifica se pode spawnar 1 inimigo
        // MAS NUNCA vai spawnar se tiver >= 5 inimigos no mapa
        
        if (CenaDoInim == null)
        {
            GD.PrintErr("[SPAWNER] ERRO: CenaDoInim não foi setada!");
            return;
        }

        // Localiza Player se não encontrado
        if (_player == null)
        {
            if (GetTree()?.CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
            
            if (_player == null)
            {
                GD.PrintErr("[SPAWNER] ERRO: Player não encontrado!");
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

        // ======== VERIFICAÇÃO RIGOROSA: NÃO SPAWNA SE >= MaxEnemies ========
        if (inimigosVivos >= MaxEnemies)
        {
            GD.Print($"[SPAWNER] ⚠️ LIMITE JÁ ATINGIDO ({inimigosVivos}/{MaxEnemies}). NÃO SPAWNANDO.");
            return; // NUNCA spawna se tem 5 ou mais
        }

        // ======== SPAWNAR 1 INIMIGO ========
        GD.Print($"[SPAWNER] ✅ Spawnando novo inimigo (faltam {MaxEnemies - inimigosVivos} para atingir máximo)");
        SpawnarUmInimigo();
    }

    private void SpawnarUmInimigo()
    {
        try
        {
            // Contagem FINAL antes de spawnar (double check)
            int verificacaoFinal = 0;
            try
            {
                var grupoFinal = GetTree()?.GetNodesInGroup("Inimigos");
                verificacaoFinal = grupoFinal?.Count ?? 0;
            }
            catch { }

            // Se já tem 5 ou mais, NÃO SPAWNA (proteção extra)
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

            // Adiciona à cena
            parent.AddChild(novoInimigo);

            // Define posição
            float offsetX = GD.Randf() > 0.5f ? 120 : -120;
            novoInimigo.GlobalPosition = _player.GlobalPosition + new Vector2(offsetX, 0);
            
            // Conta DEPOIS de spawnar
            int totalAposSpawn = 0;
            try
            {
                totalAposSpawn = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
            }
            catch { }

            GD.Print($"✅ [SPAWNER] Novo inimigo criado! Total agora: {totalAposSpawn}/{MaxEnemies}");

            // Se ultrapassou, algo está errado!
            if (totalAposSpawn > MaxEnemies)
            {
                GD.PrintErr($"⚠️⚠️⚠️ ERRO CRÍTICO! Total após spawn é {totalAposSpawn}, ultrapassou limite {MaxEnemies}!");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SPAWNER] ERRO ao spawnar: {e}");
        }
    }
}