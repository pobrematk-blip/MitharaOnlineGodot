using Godot;
using System;

public partial class Inimigo : CharacterBody2D
{
    [Export] public string NomeDoInimigo = "Goblin";
    [Export] public int VidaMaxima = 30;
    [Export] public float Velocidade = 100.0f;
    
    // Configurações do Ataque (Valores calibrados para MMOs 2D)
    [Export] public float DistanciaAtaque = 45.0f;
    [Export] public int DanoDoAtaque = 10;
    [Export] public float TempoEntreAtaques = 1.2f;

    [Export] public bool IsBoss = false;
    [Export] public int ExperienciaDropada = 20;

    // Passive / Aggressive mode
    [Export] public bool EhAgressivo = false;
    [Export] public float RaioPatrulha = 80.0f;
    [Export] public float RaioDetectarPlayer = 200.0f;
    [Export] public float TempoAggroAposDano = 8.0f;

    // Drop configuration
    [Export] public int DropItemID = 0;
    [Export] public float DropChance = 0.0f;

    private int _vidaAtual;
    private CharacterBody2D _player;
    private AnimatedSprite2D _sprite;
    private bool _estaAtacando = false;
    private float _cronometroAtaque = 0f;

    // Patrol state
    private Vector2 _pontoSpawn;
    private Vector2 _pontoPatrulha;
    private float _tempoEsperaPatrulha = 0f;
    private bool _patrulhando = false;
    private bool _posicaoInicializada = false;

    // Aggro state
    private bool _foiAtacado = false;
    private float _tempoAggro = 0f;

    private readonly Vector2 _healthBarSize = new Vector2(50, 6);
    private readonly Vector2 _healthBarOffset = new Vector2(0, -48);
    private readonly Color _healthBarBackground = new Color(0, 0, 0, 0.55f);
    private readonly Color _healthBarForeground = new Color(0.85f, 0.15f, 0.15f, 1);

    // Expose current HP for pet scroll check
    public int VidaAtual => _vidaAtual;
    public int VidaMax => VidaMaxima;

    public void DefinirPosicaoInicial(Vector2 posicao)
    {
        _pontoSpawn = posicao;
        _posicaoInicializada = true;
        EscolherNovoPontoPatrulha();
    }

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
        GD.Print($"[INIMIGO] ✓ ADICIONADO ao grupo 'Inimigos'. Total no mapa AGORA: {totalInimigos}");
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) 
        {
            if (GetTree() != null && GetTree().CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
            return;
        }

        if (_estaAtacando)
        {
            _cronometroAtaque -= (float)delta;
            if (_cronometroAtaque <= 0f)
                _estaAtacando = false;
            else if (_sprite != null && !_sprite.IsPlaying())
            {
                // Animação de ataque terminou: volta para idle
                string idleAnim = _sprite.Animation.ToString().Replace("attack", "idle");
                if (!string.IsNullOrEmpty(idleAnim) && _sprite.SpriteFrames.HasAnimation(idleAnim))
                    _sprite.Play(idleAnim);
            }
        }

        // Aggro timeout — volta a ser passivo após algum tempo
        if (_foiAtacado)
        {
            _tempoAggro -= (float)delta;
            if (_tempoAggro <= 0f)
                _foiAtacado = false;
        }

        float distanciaAoPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        Vector2 direcaoPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();

        bool deveAtacar = EhAgressivo || _foiAtacado;

        if (deveAtacar && distanciaAoPlayer <= DistanciaAtaque)
        {
            Velocity = Velocity.MoveToward(Vector2.Zero, Velocidade * 0.2f);
            MoveAndSlide();

            if (!_estaAtacando)
                IniciarAtaque(direcaoPlayer);
        }
        else if (deveAtacar && distanciaAoPlayer <= RaioDetectarPlayer)
        {
            Velocity = direcaoPlayer * Velocidade;
            MoveAndSlide();
            AtualizarDirecaoDoSprite(direcaoPlayer, false);
        }
        else if (_foiAtacado && distanciaAoPlayer > RaioDetectarPlayer)
        {
            // Perdeu o player de vista, volta a patrulhar
            _foiAtacado = false;
        }
        else
        {
            // Comportamento passivo / patrulha
            AtualizarPatrulha((float)delta);
        }
    }

    private void EscolherNovoPontoPatrulha()
    {
        float angulo = (float)(GD.Randf() * Math.PI * 2);
        float distancia = (float)GD.RandRange(20, RaioPatrulha);
        _pontoPatrulha = _pontoSpawn + new Vector2(
            (float)Math.Cos(angulo) * distancia,
            (float)Math.Sin(angulo) * distancia
        );
        _patrulhando = true;
        _tempoEsperaPatrulha = 0f;
    }

    private void AtualizarPatrulha(float delta)
    {
        if (!_posicaoInicializada)
        {
            _pontoSpawn = GlobalPosition;
            _posicaoInicializada = true;
            EscolherNovoPontoPatrulha();
        }

        if (_tempoEsperaPatrulha > 0f)
        {
            _tempoEsperaPatrulha -= delta;
            if (_tempoEsperaPatrulha <= 0f)
                EscolherNovoPontoPatrulha();
            else
            {
                Velocity = Velocity.MoveToward(Vector2.Zero, Velocidade * 2f);
                MoveAndSlide();
            }
            return;
        }

        float distAoPonto = GlobalPosition.DistanceTo(_pontoPatrulha);

        if (distAoPonto < 10f)
        {
            _tempoEsperaPatrulha = (float)GD.RandRange(2.0, 5.0);
            _patrulhando = false;
            AtualizarDirecaoDoSprite(Vector2.Zero, true);
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        Vector2 direcao = (_pontoPatrulha - GlobalPosition).Normalized();
        Velocity = direcao * Velocidade * 0.5f;
        MoveAndSlide();
        AtualizarDirecaoDoSprite(direcao, false);
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
            GD.PrintErr($"[INIMIGO] ✘ Animação de ataque '{animacaoAtaque}' não encontrada no AnimatedSprite2D.");
        }

        // APLICAR DANO NO PLAYER
        if (_player is Player player)
        {
            player.LevarDano(DanoDoAtaque);
            GD.Print($"[INIMIGO] {NomeDoInimigo} causou {DanoDoAtaque} de dano ao Player.");
        }
        else if (_player != null && _player.HasMethod("LevarDano"))
        {
            _player.Call("LevarDano", DanoDoAtaque);
            GD.Print($"[INIMIGO] {NomeDoInimigo} causou {DanoDoAtaque} de dano ao Player via Call.");
        }
    }

    private void AtualizarDirecaoDoSprite(Vector2 direcao, bool forcarIdle = false)
    {
        if (_sprite == null || _sprite.SpriteFrames == null) return;

        string desejada = null;
        float velocidadeMagnitude = Velocity.Length();

        if (forcarIdle || velocidadeMagnitude < 5f)
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
            if (_sprite.Animation != desejada)
                _sprite.Play(desejada);
        }
    }

    public override void _Draw()
    {
        if (VidaMaxima <= 0) return;
        if (_vidaAtual <= 0) return;

        var barTopLeft = _healthBarOffset - new Vector2(_healthBarSize.X / 2.0f, 0);
        var fillWidth = Math.Max(0, Math.Min(_healthBarSize.X, (_vidaAtual / (float)VidaMaxima) * _healthBarSize.X));
        var foregroundWidth = Math.Max(0, fillWidth - 2);

        DrawRect(new Rect2(barTopLeft, _healthBarSize), _healthBarBackground);
        DrawRect(new Rect2(barTopLeft + new Vector2(1, 1), new Vector2(foregroundWidth, _healthBarSize.Y - 2)), _healthBarForeground);
    }

    public void LevarDano(int quantidade)
    {
        _vidaAtual -= quantidade;

        // Ao levar dano, fica agressivo por um tempo
        if (!EhAgressivo)
        {
            _foiAtacado = true;
            _tempoAggro = TempoAggroAposDano;
            GD.Print($"[INIMIGO] {NomeDoInimigo} aggro! Player atacou, vai retaliar por {TempoAggroAposDano}s.");
        }

        Modulate = Color.FromHtml("ff6666");
        if (GetTree() != null)
        {
            GetTree().CreateTimer(0.15f).Timeout += () => Modulate = Color.FromHtml("ffffff");
        }

        QueueRedraw();

        if (_vidaAtual <= 0)
        {
            if (GetTree()?.CurrentScene != null)
            {
                var player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
                if (player != null)
                {
                    var xpComp = player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
                    var equipComp = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
                    if (xpComp != null)
                        xpComp.AdicionarExperiencia(ExperienciaDropada, equipComp);
                }
            }

            TentarDrop();
            QueueFree();
        }
    }

    private void TentarDrop()
    {
        if (DropItemID <= 0) return;
        if (GD.Randf() > DropChance) return;

        string[] caminhos =
        {
            $"res://Itens/ItemNovo.tres",
            $"res://Itens/Capacete.tres",
            $"res://Itens/Peitoral.tres",
        };

        string caminho = $"res://Itens/ItemNovo.tres";
        if (DropItemID == 20) caminho = "res://Itens/Capacete.tres";
        else if (DropItemID == 21) caminho = "res://Itens/Peitoral.tres";
        else if (DropItemID == 100) caminho = "res://Itens/PergaminhoDoPet.tres";

        if (!ResourceLoader.Exists(caminho)) return;

        var item = ResourceLoader.Load<ItemResource>(caminho);
        if (item == null) return;

        var cenaDrop = GD.Load<PackedScene>("res://Itens/ItemColetavel.tscn");
        if (cenaDrop == null) return;

        var drop = cenaDrop.Instantiate<ItemColetavel>();
        drop.ItemContido = item;
        drop.GlobalPosition = GlobalPosition;
        GetParent().AddChild(drop);
    }

    public override void _ExitTree()
    {
        base._ExitTree();
    }
}