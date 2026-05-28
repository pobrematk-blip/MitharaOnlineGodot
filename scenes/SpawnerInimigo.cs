using Godot;
using System;

// IMPORTANTE: Esse nome (SpawnerInimigo) deve ser IGUAL ao nome do seu arquivo .cs
public partial class SpawnerInimigo : Timer
{
    [Export] public PackedScene CenaDoInim;
    [Export] public int MaxEnemies = 5;

    private CharacterBody2D _player;
    private bool _inicializadoComSucesso = false;

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

        // ======== CONTAR INIMIGOS VIVOS ========
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

        GD.Print($"[SPAWNER TICK] Inimigos vivos: {inimigosVivos} / Max: {MaxEnemies}");

        // ======== SPAW APENAS 1 SE NECESSÁRIO ========
        if (inimigosVivos < MaxEnemies)
        {
            SpawnarUmInimigo();
        }
        else
        {
            GD.Print($"[SPAWNER] Limite mantido em {MaxEnemies} inimigos.");
        }
    }

    private void SpawnarUmInimigo()
    {
        try
        {
            GD.Print("[SPAWNER] Spawning novo inimigo...");
            
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

            // Adiciona à cena PRIMEIRO
            parent.AddChild(novoInimigo);
            GD.Print("[SPAWNER] Inimigo adicionado à cena.");

            // Define posição
            float offsetX = GD.Randf() > 0.5f ? 120 : -120;
            novoInimigo.GlobalPosition = _player.GlobalPosition + new Vector2(offsetX, 0);
            
            GD.Print($"✅ [SPAWNER] Novo inimigo criado em posição: {novoInimigo.GlobalPosition}");
            
            // Conta de novo para confirmar
            var totalAgora = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
            GD.Print($"[SPAWNER] Total após spawn: {totalAgora}/{MaxEnemies}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SPAWNER] ERRO ao spawnar: {e}");
        }
    }
}