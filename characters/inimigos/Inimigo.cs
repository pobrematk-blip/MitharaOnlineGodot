using Godot;
using System;

public partial class Inimigo : CharacterBody2D
{
    [Export] public string NomeDoInimigo = "Goblin";
    [Export] public int VidaMaxima = 30;
    [Export] public float Velocidade = 100.0f;

    private int _vidaAtual;
    private CharacterBody2D _player;
    private AnimatedSprite2D _sprite; 

    public override void _Ready()
    {
        GD.Print("[INIMIGO] Inicializando...");

        _vidaAtual = VidaMaxima;

        // PROTEÇÃO 1: Evita que o jogo quebre se o nó do sprite sumir ou mudar de nome
        if (HasNode("AnimatedSprite2D"))
        {
            _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            GD.Print($"[INIMIGO] Sprite encontrado: {_sprite.Name}");

            // Força o sprite e a animação a ficarem ativos logo no nascimento
            Visible = true;
            _sprite.Visible = true;

            // Debug: mostra propriedades importantes para diagnóstico
            GD.Print($"[INIMIGO] Sprite visível: {_sprite.Visible}");
            GD.Print($"[INIMIGO] Nodo visível: {Visible}");
            GD.Print($"[INIMIGO] Modulate: {Modulate}");
            GD.Print($"[INIMIGO] Scale: {Scale}");
            GD.Print($"[INIMIGO] ZIndex: {GetZIndex()}");

            // Lista as animações disponíveis no SpriteFrames (se houver)
            if (_sprite.SpriteFrames != null)
            {
                try
                {
                    var names = _sprite.SpriteFrames.GetAnimationNames();
                    GD.Print("[INIMIGO] Animações disponíveis no SpriteFrames:");
                    foreach (var n in names)
                    {
                        GD.Print($" - {n}");
                    }

                    GD.Print($"[INIMIGO] Animação atual do AnimatedSprite2D: {_sprite.Animation}");

                    // Tenta reproduzir a animação atual, se válida
                    if (!string.IsNullOrEmpty(_sprite.Animation) && _sprite.SpriteFrames.HasAnimation(_sprite.Animation))
                    {
                        _sprite.Play(_sprite.Animation);
                        GD.Print($"[INIMIGO] Tocando animação existente: {_sprite.Animation}");
                    }
                    else
                    {
                        // Tenta possíveis nomes (corrige erro de digitação: goblim vs goblin)
                        if (_sprite.SpriteFrames.HasAnimation("goblim_idle_down"))
                        {
                            _sprite.Play("goblim_idle_down");
                            GD.Print("[INIMIGO] Tocando 'goblim_idle_down' (variante encontrada)");
                        }
                        else if (_sprite.SpriteFrames.HasAnimation("goblin_idle_down"))
                        {
                            _sprite.Play("goblin_idle_down");
                            GD.Print("[INIMIGO] Tocando 'goblin_idle_down' (variante encontrada)");
                        }
                        else
                        {
                            // Toca a primeira animação disponível como fallback
                            if (names.Length > 0)
                            {
                                _sprite.Play(names[0]);
                                GD.Print($"[INIMIGO] Tocando fallback: {names[0]}");
                            }
                            else
                            {
                                GD.PrintErr("[INIMIGO] Nenhuma animação disponível no SpriteFrames!");
                            }
                        }
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
        
        GD.Print($"[INIMIGO] Posição Inicial: {GlobalPosition}");
        GD.Print($"[INIMIGO] Visível no mapa: {Visible}");
        
        // PROTEÇÃO 2: Busca robusta para encontrar o Player independente de onde o Spawner criar o monstro
        if (GetTree() != null && GetTree().CurrentScene != null)
        {
            _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            GD.Print($"[INIMIGO] Buscando Player na cena: {GetTree().CurrentScene.Name}");
        }
        
        if (_player != null)
        {
            GD.Print($"[INIMIGO] ✅ Player encontrado! Posição: {_player.GlobalPosition}");
        }
        else
        {
            GD.PrintErr("[INIMIGO] ❌ AVISO: Player não encontrado na _Ready()! Tentará novamente em _PhysicsProcess.");
        }

        AddToGroup("Inimigos");
        int totalInimigos = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
        GD.Print($"[INIMIGO] Adicionado ao grupo 'Inimigos'. Total agora: {totalInimigos}");
        GD.Print("[INIMIGO] Inicialização concluída!");
    }

    public override void _PhysicsProcess(double delta)
    {
        // PROTEÇÃO 3: Se o player não foi achado no nascimento, continua tentando localizá-lo a cada frame
        if (_player == null) 
        {
            if (GetTree() != null && GetTree().CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
                if (_player != null)
                {
                    GD.Print($"[INIMIGO] ✅ Player encontrado em _PhysicsProcess! Posição: {_player.GlobalPosition}");
                }
            }
            return; // Se ainda não achou, pula este frame para não dar erro
        }

        // ======== MOVIMENTO ========
        Vector2 direcao = (_player.GlobalPosition - GlobalPosition).Normalized();
        Velocity = direcao * Velocidade;
        
        // Move o corpo físico na Godot
        MoveAndSlide();

        // Debug a cada N frames (não a cada frame para não poluir o console)
        if (Engine.GetPhysicsFrames() % 30 == 0)
        {
            GD.Print($"[INIMIGO] Posição: {GlobalPosition}, Direção: {direcao}, Velocidade: {Velocity}");
        }

        // ======== ANIMAÇÃO ========
        AtualizarDirecaoDoSprite(direcao);
    }

    private void AtualizarDirecaoDoSprite(Vector2 direcao)
    {
        if (_sprite == null || _sprite.SpriteFrames == null) 
        {
            GD.PrintErr("[INIMIGO] ERRO: Sprite ou SpriteFrames é null!");
            return;
        }

        string desejada = null;
        float velocidadeMagnitude = Velocity.Length();

        // Se quase parado, toca animação idle
        if (velocidadeMagnitude < 5f) // Mudei de direcao.Length() para Velocity.Length()
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
            // Escolhe animação de caminhada
            if (Math.Abs(direcao.X) > Math.Abs(direcao.Y))
            {
                desejada = direcao.X < 0 ? "goblim_walk_left" : "goblim_walk_right";
            }
            else
            {
                desejada = direcao.Y < 0 ? "goblim_walk_up" : "goblim_walk_down";
            }
        }

        // Sempre toca a animação desejada para garantir transição
        if (!string.IsNullOrEmpty(desejada) && _sprite.SpriteFrames.HasAnimation(desejada))
        {
            _sprite.Play(desejada);
        }
        else if (!string.IsNullOrEmpty(desejada))
        {
            GD.PrintErr($"[INIMIGO] ERRO: Animação '{desejada}' não existe em SpriteFrames!");
        }
    }

    public void LevarDano(int quantidade)
    {
        _vidaAtual -= quantidade;
        GD.Print($"{NomeDoInimigo} levou {quantidade} de dano! Vida: {_vidaAtual}");

        // Efeito visual de piscar em vermelho
        Modulate = Color.FromHtml("ff6666");
        
        if (GetTree() != null)
        {
            GetTree().CreateTimer(0.15f).Timeout += () => Modulate = Color.FromHtml("ffffff");
        }

        if (_vidaAtual <= 0)
        {
            GD.Print($"{NomeDoInimigo} foi derrotado!");
            QueueFree(); // Remove o monstro do jogo com segurança
        }
    }
}