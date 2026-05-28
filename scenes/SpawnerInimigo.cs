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
        GD.Print("[SISTEMA] Timer de 3 segundos disparou!");

        if (CenaDoInim == null)
        {
            GD.PrintErr("[SISTEMA ERRO] O slot 'Cena Do Inim' perdeu a referência. Arraste o Inimigo.tscn de novo!");
            return;
        }

        // Tenta garantir que temos uma referência ao player no momento do spawn.
        if (_player == null)
        {
            if (GetTree()?.CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
        }

        if (_player == null)
        {
            GD.PrintErr("[SISTEMA ERRO] Não foi possível localizar o Player no momento do spawn. Pulando este ciclo.");
            return;
        }

        // ======== CONTAGEM DE INIMIGOS ========
        int existentes = 0;
        
        try
        {
            var inimigosNoMapa = GetTree()?.GetNodesInGroup("Inimigos");
            if (inimigosNoMapa != null)
            {
                existentes = inimigosNoMapa.Count;
                GD.Print($"[SISTEMA] Inimigos no mapa (CONTAR DIRETO): {existentes}");
                
                // Debug: listar nome de cada inimigo
                foreach (var inimigoVariant in inimigosNoMapa)
                {
                    var inimigo = (Node)inimigoVariant;
                    GD.Print($"  - {inimigo.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SISTEMA] Erro ao contar inimigos: {ex.Message}");
            existentes = 0;
        }

        GD.Print($"[SISTEMA] Total de inimigos: {existentes} / Máximo: {MaxEnemies}");

        if (existentes >= MaxEnemies)
        {
            GD.Print($"[SISTEMA] ⚠️ LIMITE ATINGIDO! Não spawnando novo inimigo. ({existentes}/{MaxEnemies})");
            return;
        }

        // ======== SPAWN NOVO INIMIGO ========
        try
        {
            CharacterBody2D novoInimigo = CenaDoInim.Instantiate<CharacterBody2D>();
            Node parent = GetParent();
            GD.Print($"[SISTEMA] Criando novo inimigo... Parent: {(parent != null ? parent.Name : "<null>")}");
            parent.AddChild(novoInimigo);

            // Nasce logo ao lado do jogador
            novoInimigo.GlobalPosition = _player.GlobalPosition + new Vector2(120, 0);

            GD.Print($"✅ [SUCESSO] {novoInimigo.Name} criado em: {novoInimigo.GlobalPosition}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SISTEMA FALHA] Erro ao instanciar: {e.Message}");
        }
    }
}