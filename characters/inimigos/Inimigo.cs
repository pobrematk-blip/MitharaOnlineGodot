using Godot;
using System;

public partial class Inimigo : CharacterBody2D
{
    [Export] public string NomeDoInimigo = "Goblin";
    [Export] public int VidaMaxima = 30;
    [Export] public float Velocidade = 100.0f;
    
    // Configurações do Ataque (Valores calibrados para MMOs 2D)
    [Export] public float DistanciaAtaque = 45.0f; // Aumentado para casar perfeitamente com o raio de colisão
    [Export] public int DanoDoAtaque = 10;
    [Export] public float TempoEntreAtaques = 1.2f; // Cooldown do ataque em segundos

    private int _vidaAtual;
    private CharacterBody2D _player;
    private AnimatedSprite2D _sprite; 
    private bool _estaAtacando = false;
    private float _cronometroAtaque = 0f;

    public override void _Ready()
    {
        GD.Print("[INIMIGO] Inicializando...");
        _vidaAtual = VidaMaxima;

        // PROTEÇÃO 1: Garante o nó do sprite
        if (HasNode("AnimatedSprite2D"))
        {
            _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            Visible = true;
            _sprite.Visible = true;

            if (_sprite.SpriteFrames != null)
            {
                try
                {
                    var names = _sprite.SpriteFrames.GetAnimationNames();
                    if (!string.IsNullOrEmpty(_sprite.Animation) && _sprite.SpriteFrames.HasAnimation(_sprite.Animation))
                    {
                        _sprite.Play(_sprite.Animation);
                    }
                    else if (_sprite.SpriteFrames.HasAnimation("goblim_idle_down"))
                    {
                        _sprite.Play("goblim_idle_down");
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[INIMIGO] Erro ao listar animações: {e.Message}");
                }
            }
        }
        else
        {
            GD.PrintErr("[INIMIGO] ERRO CRÍTICO: O nó filho chamado 'AnimatedSprite2D' não foi encontrado!");
        }
        
        // PROTEÇÃO 2: Busca robusta para encontrar o Player
        if (GetTree() != null && GetTree().CurrentScene != null)
        {
            _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
        }

        // CORREÇÃO DO SPAWNER: O próprio monstro se adiciona ao grupo assim que nasce!
        AddToGroup("Inimigos");
        int totalInimigos = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
        GD.Print($"[INIMIGO] ✅ ADICIONADO ao grupo 'Inimigos'. Total no mapa AGORA: {totalInimigos}");
    }

    public override void _PhysicsProcess(double delta)
    {
        // Garante que o player existe
        if (_player == null) 
        {
            if (GetTree() != null && GetTree().CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
            return;
        }

        // Gerencia o tempo de recarga do ataque
        if (_estaAtacando)
        {
            _cronometroAtaque -= (float)delta;
            if (_cronometroAtaque <= 0f)
            {
                _estaAtacando = false; // Pronto para agir de novo após o fim do cooldown
            }
        }

        // Calcula a distância real até o jogador
        float distanciaAoplayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        Vector2 direcao = (_player.GlobalPosition - GlobalPosition).Normalized();

        // ======== INTELIGÊNCIA DE ATAQUE VS MOVIMENTO ========
        if (distanciaAoplayer <= DistanciaAtaque)
        {
            // ALINHAMENTO MMO: Em vez de travar o movimento do nada (o que causa o empilhamento),
            // fazemos ele deslizar suavemente até parar na distância correta, respeitando os outros monstros.
            Velocity = Velocity.MoveToward(Vector2.Zero, Velocidade * 0.2f);
            MoveAndSlide();
            
            if (!_estaAtacando)
            {
                IniciarAtaque(direcao);
            }
        }
        else if (!_estaAtacando)
        {
            // SEGUIR O PLAYER (Apenas se não estiver executando um ataque)
            Velocity = direcao * Velocidade;
            MoveAndSlide();
            
            // Atualiza animação de caminhada normal
            AtualizarDirecaoDoSprite(direcao);
        }

        // Debug controlado
        if (Engine.GetPhysicsFrames() % 45 == 0 && !_estaAtacando)
        {
            GD.Print($"[INIMIGO] Caçando Player. Distância: {distanciaAoplayer}px");
        }
    }

    private void IniciarAtaque(Vector2 direcaoDoPlayer)
    {
        _estaAtacando = true;
        _cronometroAtaque = TempoEntreAtaques;

        // Define a animação baseada para onde o jogador está em relação ao monstro
        string animacaoAtaque = "goblim_idle_down"; // Fallback seguro

        if (Math.Abs(direcaoDoPlayer.X) > Math.Abs(direcaoDoPlayer.Y))
        {
            animacaoAtaque = direcaoDoPlayer.X < 0 ? "goblim_attack_left" : "goblim_attack_right";
        }
        else
        {
            // CORRIGIDO: Agora verifica corretamente Y < 0 para "up" e Y > 0 para "down"
            animacaoAtaque = direcaoDoPlayer.Y < 0 ? "goblim_attack_up" : "goblim_attack_down";
        }

        // Toca a animação de combate
        if (_sprite != null && _sprite.SpriteFrames.HasAnimation(animacaoAtaque))
        {
            _sprite.Play(animacaoAtaque);
            GD.Print($"[ATAQUE] {NomeDoInimigo} atacou na direção: {animacaoAtaque}!");
        }
        else
        {
            GD.PrintErr($"[INIMIGO] ⚠️ Animação de ataque '{animacaoAtaque}' não encontrada no AnimatedSprite2D.");
        }

        // APLICAR DANO NO PLAYER
        // Se o seu script do Player tiver uma função pública chamada 'LevarDano', descomente a linha abaixo:
        // if (_player.HasMethod("LevarDano")) { _player.Call("LevarDano", DanoDoAtaque); }
    }

    private void AtualizarDirecaoDoSprite(Vector2 direcao)
    {
        if (_sprite == null || _sprite.SpriteFrames == null) return;

        string desejada = null;
        float velocidadeMagnitude = Velocity.Length();

        if (velocidadeMagnitude < 5f)
        {
            if (Math.Abs(Velocity.X) > Math.Abs(Velocity.Y))
            {
                desejada = Velocity.X < 0 ? "goblim_idle_left" : "goblim_idle_right";
            }
            else
            {
                desejada = Velocity.Y < 0 ? "goblim_idle_up" : "goblim_idle_down";
            }
        }
        else
        {
            if (Math.Abs(direcao.X) > Math.Abs(direcao.Y))
            {
                desejada = direcao.X < 0 ? "goblim_walk_left" : "goblim_walk_right";
            }
            else
            {
                desejada = direcao.Y < 0 ? "goblim_walk_up" : "goblim_walk_down";
            }
        }

        if (!string.IsNullOrEmpty(desejada) && _sprite.SpriteFrames.HasAnimation(desejada))
        {
            _sprite.Play(desejada);
        }
    }

    public void LevarDano(int quantidade)
    {
        _vidaAtual -= quantidade;
        GD.Print($"{NomeDoInimigo} levou {quantidade} de dano! Vida: {_vidaAtual}");

        Modulate = Color.FromHtml("ff6666");
        if (GetTree() != null)
        {
            GetTree().CreateTimer(0.15f).Timeout += () => Modulate = Color.FromHtml("ffffff");
        }

        if (_vidaAtual <= 0)
        {
            GD.Print($"💀 {NomeDoInimigo} foi derrotado! Removendo do mapa...");
            QueueFree(); 
        }
    }

    public override void _ExitTree()
    {
        int totalAntes = 0;
        try { totalAntes = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0; } catch { }
        GD.Print($"[INIMIGO REMOVER] {NomeDoInimigo} removido da cena. Inimigos restantes no grupo: {totalAntes}");
        base._ExitTree();
    }
}