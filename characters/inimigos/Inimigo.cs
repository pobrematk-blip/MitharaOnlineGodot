using Godot;
using System;
using System.Collections.Generic;

public partial class Inimigo : CharacterBody2D
{
    public enum TipoComportamento
    {
        Passivo,
        Agressivo,
        Boss
    }

    public enum EstadoInimigo
    {
        Idle,
        Patrol,
        Chase,
        Attack,
        Dead
    }

    [Export] public string NomeDoInimigo = "Goblin";
    [Export] public int VidaMaxima = 30;
    [Export] public float Velocidade = 100.0f;
    
    [Export] public float DistanciaAtaque = 45.0f;
    [Export] public int DanoDoAtaque = 10;
    [Export] public float TempoEntreAtaques = 1.2f;

    [Export] public bool IsBoss = false;
    [Export] public int ExperienciaDropada = 20;

    [Export] public TipoComportamento Comportamento = TipoComportamento.Passivo;
    [Export] public float RaioPatrulha = 80.0f;
    [Export] public float RaioDetectarPlayer = 200.0f;
    [Export] public float RaioBoss = 300.0f;
    [Export] public float TempoAggroAposDano = 8.0f;

    [Export] public Godot.Collections.Array<DropEntry> DropTable = new();

    [Export] public int PetID = 0;

    [Export] public string AnimPrefix = "goblin_";

    [Export] public string MobType = "goblin";

    private bool _mostrouBarraBoss = false;
    private Label _bossNameLabel = null;
    private ProgressBar _bossHealthBar = null;

    private string RemapearDirecao(string cardinal)
    {
        return cardinal;
    }

    private string VectorToCardinal4(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.001f) return "down";
        dir = dir.Normalized();
        float angle = Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X));
        if (angle < 0) angle += 360f;

        if (angle >= 315f || angle < 45f) return "right";
        if (angle >= 45f && angle < 135f) return "down";
        if (angle >= 135f && angle < 225f) return "left";
        return "up";
    }

    private int _vidaAtual;
    private bool _vidaInicializada;
    private CharacterBody2D _player;
    private AnimatedSprite2D _sprite;
    private Vector2 _facingDirection = Vector2.Down;
    private Vector2 _pontoSpawn;
    private Vector2 _pontoPatrulha;
    private bool _posicaoInicializada = false;
    private bool _foiAtacado = false;
    private float _tempoAggro = 0f;

    private EstadoInimigo _estado = EstadoInimigo.Idle;
    private float _patrolTimer = 0f;
    private const float PatrolDurationMin = 15f;
    private const float PatrolDurationMax = 20f;
    private float _idleTimer = 0f;
    private const float IdleDuration = 3f;
    private float _attackCooldown = 0f;

    private readonly Vector2 _healthBarSize = new Vector2(80, 6);
    private readonly Vector2 _healthBarOffset = new Vector2(0, -48);
    private readonly Color _healthBarBackground = new Color(0, 0, 0, 0.35f);
    private readonly Color _healthBarForeground = new Color(0.85f, 0.15f, 0.15f, 1);
    private const int HealthBarRaio = 3;

    public int VidaAtual => _vidaAtual;
    public int VidaMax => VidaMaxima;

    public void DefinirPosicaoInicial(Vector2 posicao)
    {
        _pontoSpawn = posicao;
        _posicaoInicializada = true;
        _estado = EstadoInimigo.Patrol;
        _patrolTimer = (float)GD.RandRange(PatrolDurationMin, PatrolDurationMax);
        EscolherNovoPontoPatrulha();
    }

    public bool IsNetworked => HasMeta("network_id");
    public Vector2 NetworkTargetPos { get; set; }

    private void EscolherNovoPontoPatrulha()
    {
        float angulo = (float)(GD.Randf() * Math.PI * 2);
        float distancia = (float)GD.RandRange(100, 400);
        _pontoPatrulha = GlobalPosition + new Vector2(
            (float)Math.Cos(angulo) * distancia,
            (float)Math.Sin(angulo) * distancia
        );
    }

    public override void _Ready()
    {
        GD.Print($"[INIMIGO] Inicializando... MobType={MobType}");

        if (HasNode("AnimatedSprite2D"))
        {
            _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
            Visible = true;
            _sprite.Visible = true;

            if (_sprite.SpriteFrames != null)
            {
                string idleDown = $"{AnimPrefix}idle_down";
                if (_sprite.SpriteFrames.HasAnimation(idleDown))
                    _sprite.Play(idleDown);
                else
                {
                    string[] anims = _sprite.SpriteFrames.GetAnimationNames();
                    if (anims.Length > 0)
                        _sprite.Play(anims[0]);
                }
                _sprite.AnimationFinished += OnAnimationFinished;
            }
        }
        else
        {
            GD.PrintErr("[INIMIGO] ERRO CRÍTICO: O nó filho chamado 'AnimatedSprite2D' não foi encontrado!");
        }

        if (GetTree() != null && GetTree().CurrentScene != null)
        {
            _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
        }

        if (IsBoss && Comportamento != TipoComportamento.Boss)
            Comportamento = TipoComportamento.Boss;

        if (Comportamento == TipoComportamento.Boss)
        {
            CriarBarraBoss();
        }

        AddToGroup("Inimigos");
        int totalInimigos = GetTree()?.GetNodesInGroup("Inimigos").Count ?? 0;
        GD.Print($"[INIMIGO] ✓ ADICIONADO ao grupo 'Inimigos'. Total no mapa AGORA: {totalInimigos}");

        if (!_vidaInicializada)
            _vidaAtual = VidaMaxima;

        InicializarDropPadrao();

        QueueRedraw();
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null) return;

        string currentAnim = _sprite.Animation.ToString();

        if (currentAnim.Contains("attack"))
        {
            if (_estado == EstadoInimigo.Attack)
            {
                _estado = EstadoInimigo.Chase;
                AtualizarAnimacaoDoEstado();
            }
        }
    }

    private void CriarBarraBoss()
    {
        var canvas = new CanvasLayer();
        canvas.Name = "BossHUD";
        canvas.Layer = 100;

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        vbox.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        vbox.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        vbox.OffsetBottom = 80;

        _bossNameLabel = new Label
        {
            Text = NomeDoInimigo,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _bossNameLabel.AddThemeFontSizeOverride("font_size", 24);
        _bossNameLabel.AddThemeColorOverride("font_color", Colors.White);
        _bossNameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bossNameLabel.AddThemeConstantOverride("outline_size", 2);

        _bossHealthBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = VidaMaxima,
            Value = VidaAtual,
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 24),
        };
        _bossHealthBar.AddThemeStyleboxOverride("progress", new StyleBoxFlat
        {
            BgColor = new Color(0.9f, 0.1f, 0.1f, 1f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
        });
        _bossHealthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.0f, 0.0f, 0.8f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
        });

        vbox.AddChild(_bossNameLabel);
        vbox.AddChild(_bossHealthBar);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 200);
        margin.AddThemeConstantOverride("margin_right", 200);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddChild(vbox);

        canvas.AddChild(margin);
        AddChild(canvas);
        _mostrouBarraBoss = true;
    }

    private void AtualizarBarraBoss()
    {
        if (!_mostrouBarraBoss || _bossHealthBar == null) return;
        _bossHealthBar.MaxValue = VidaMaxima;
        _bossHealthBar.Value = _vidaAtual;
        if (_bossNameLabel != null)
            _bossNameLabel.Text = $"{NomeDoInimigo}  [{_vidaAtual}/{VidaMaxima}]";
    }

    private void InicializarDropPadrao()
    {
        if (DropTable.Count > 0) return;
        PreencherDropTable(MobType, DropTable);

        if (PetID <= 0)
        {
            PetID = MobType switch
            {
                "goblin" => 3,
                "lobo" => 2,
                _ => 0,
            };
        }
    }

    public static void PreencherDropTable(string mobType, Godot.Collections.Array<DropEntry> table)
    {
        if (table.Count > 0) return;

        switch (mobType)
        {
            case "goblin":
                table.Add(new DropEntry { ItemId = 1, Chance = 0.05, MinQty = 1, MaxQty = 2 });
                table.Add(new DropEntry { ItemId = 2, Chance = 0.05 });
                break;
            case "lobo":
                table.Add(new DropEntry { ItemId = 1, Chance = 0.05 });
                table.Add(new DropEntry { ItemId = 2, Chance = 0.05, MinQty = 1, MaxQty = 2 });
                break;
            case "porco":
                table.Add(new DropEntry { ItemId = 1, Chance = 0.05 });
                table.Add(new DropEntry { ItemId = 2, Chance = 0.05 });
                break;
            case "minotauro":
                table.Add(new DropEntry { ItemId = 1, Chance = 0.05, MinQty = 2, MaxQty = 4 });
                table.Add(new DropEntry { ItemId = 2, Chance = 0.05, MinQty = 1, MaxQty = 3 });
                break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null) 
        {
            if (GetTree() != null && GetTree().CurrentScene != null)
            {
                _player = GetTree().CurrentScene.FindChild("Player", true, false) as CharacterBody2D;
            }
        }

        if (IsNetworked)
        {
            if (_sprite != null && _sprite.SpriteFrames != null)
            {
                string currentAnim = _sprite.Animation.ToString();
                if (currentAnim.Contains("attack") && !_sprite.IsPlaying())
                {
                    string idleAnim = currentAnim.Replace("attack", "idle");
                    if (_sprite.SpriteFrames.HasAnimation(idleAnim))
                        _sprite.Play(idleAnim);
                }
            }

            Vector2 toTarget = NetworkTargetPos - GlobalPosition;
            float dist = toTarget.Length();
            if (dist > 2f)
            {
                float speed = Mathf.Clamp(dist * 15f, 50f, 400f);
                Velocity = toTarget / dist * speed;
            }
            else
            {
                Velocity = Vector2.Zero;
            }
            MoveAndSlide();
            return;
        }

        if (_foiAtacado)
        {
            _tempoAggro -= (float)delta;
            if (_tempoAggro <= 0f)
                _foiAtacado = false;
        }

        if (_player == null) return;

        _attackCooldown -= (float)delta;

        Vector2 dirToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
        if (dirToPlayer.LengthSquared() > 0.001f)
            _facingDirection = dirToPlayer;

        switch (_estado)
        {
            case EstadoInimigo.Idle:
                AtualizarIdle((float)delta);
                break;
            case EstadoInimigo.Patrol:
                AtualizarPatrulha((float)delta);
                break;
            case EstadoInimigo.Chase:
                AtualizarChase(dirToPlayer);
                break;
            case EstadoInimigo.Attack:
                AtualizarAtaque(dirToPlayer);
                break;
        }
    }

    private bool DeveIniciarChase()
    {
        if (_player == null) return false;

        switch (Comportamento)
        {
            case TipoComportamento.Agressivo:
                float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
                return dist <= RaioDetectarPlayer;
            case TipoComportamento.Passivo:
            case TipoComportamento.Boss:
                return _foiAtacado;
        }
        return false;
    }

    private void AtualizarIdle(float delta)
    {
        if (DeveIniciarChase())
        {
            _estado = EstadoInimigo.Chase;
            return;
        }

        _idleTimer -= delta;

        if (_idleTimer <= 0f)
        {
            _estado = EstadoInimigo.Patrol;
            _patrolTimer = (float)GD.RandRange(PatrolDurationMin, PatrolDurationMax);
            EscolherNovoPontoPatrulha();
        }

        Velocity = Velocity.MoveToward(Vector2.Zero, Velocidade * 2f);
        MoveAndSlide();

        string idleAnim = $"{AnimPrefix}idle_{CardinalDirection(_facingDirection)}";
            if (_sprite?.SpriteFrames?.HasAnimation(idleAnim) == true && _sprite.Animation != idleAnim)
                _sprite.Play(idleAnim);
        }

        private void AtualizarPatrulha(float delta)
    {
        if (DeveIniciarChase())
        {
            _estado = EstadoInimigo.Chase;
            return;
        }

        if (!_posicaoInicializada)
        {
            _pontoSpawn = GlobalPosition;
            _posicaoInicializada = true;
            _patrolTimer = (float)GD.RandRange(PatrolDurationMin, PatrolDurationMax);
            EscolherNovoPontoPatrulha();
        }

        _patrolTimer -= delta;

        if (_patrolTimer <= 0f)
        {
            _estado = EstadoInimigo.Idle;
            _idleTimer = IdleDuration;
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        float distAoPonto = GlobalPosition.DistanceTo(_pontoPatrulha);

        if (distAoPonto < 10f)
        {
            _estado = EstadoInimigo.Idle;
            _idleTimer = IdleDuration;
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        Vector2 direcao = (_pontoPatrulha - GlobalPosition).Normalized();
        _facingDirection = direcao;
        Velocity = direcao * Velocidade;
        MoveAndSlide();

        string walkAnim = $"{AnimPrefix}walk_{CardinalDirection(_facingDirection)}";
        if (_sprite?.SpriteFrames?.HasAnimation(walkAnim) == true && _sprite.Animation != walkAnim)
            _sprite.Play(walkAnim);
    }

    private void AtualizarChase(Vector2 dirPlayer)
    {
        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

        bool devePerseguir = false;

        switch (Comportamento)
        {
            case TipoComportamento.Agressivo:
                devePerseguir = dist <= RaioDetectarPlayer;
                break;
            case TipoComportamento.Passivo:
            case TipoComportamento.Boss:
                devePerseguir = _foiAtacado;
                break;
        }

        if (!devePerseguir)
        {
            _estado = EstadoInimigo.Idle;
            _idleTimer = IdleDuration;
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }

        if (dist <= DistanciaAtaque && _attackCooldown <= 0f)
        {
            _estado = EstadoInimigo.Attack;
            IniciarAtaque();
            return;
        }

        _facingDirection = dirPlayer;
        Velocity = dirPlayer * Velocidade;
        MoveAndSlide();

        string walkAnim = $"{AnimPrefix}walk_{CardinalDirection(_facingDirection)}";
        if (_sprite?.SpriteFrames?.HasAnimation(walkAnim) == true && _sprite.Animation != walkAnim)
            _sprite.Play(walkAnim);
    }

    private void AtualizarAtaque(Vector2 dirPlayer)
    {
        if (_sprite != null && !_sprite.IsPlaying())
        {
            string currentAnim = _sprite.Animation.ToString();
            if (!currentAnim.Contains("attack"))
            {
                _estado = EstadoInimigo.Chase;
                return;
            }
        }
    }

    private void IniciarAtaque()
    {
        _attackCooldown = TempoEntreAtaques;

        string cardinal = CardinalDirection(_facingDirection);
        string animacaoAtaque = $"{AnimPrefix}attack_{cardinal}";

        if (_sprite?.SpriteFrames?.HasAnimation(animacaoAtaque) == true)
        {
            _sprite.Play(animacaoAtaque);
        }
        else
        {
            string fallback = $"{AnimPrefix}idle_down";
            if (_sprite?.SpriteFrames?.HasAnimation(fallback) == true)
                _sprite.Play(fallback);
        }

        if (_player is Player player)
        {
            player.LevarDano(DanoDoAtaque);
        }
        else if (_player != null && _player.HasMethod("LevarDano"))
        {
            _player.Call("LevarDano", DanoDoAtaque);
        }
    }

    private string CardinalDirection(Vector2 dir)
    {
        return VectorToCardinal4(dir);
    }

    private void AtualizarAnimacaoDoEstado()
    {
        string anim = "";

        switch (_estado)
        {
            case EstadoInimigo.Idle:
                anim = $"{AnimPrefix}idle_{CardinalDirection(_facingDirection)}";
                break;
            case EstadoInimigo.Patrol:
                anim = $"{AnimPrefix}walk_{CardinalDirection(_facingDirection)}";
                break;
            case EstadoInimigo.Chase:
                anim = $"{AnimPrefix}walk_{CardinalDirection(_facingDirection)}";
                break;
            case EstadoInimigo.Attack:
                anim = $"{AnimPrefix}attack_{CardinalDirection(_facingDirection)}";
                break;
        }

        if (_sprite?.SpriteFrames?.HasAnimation(anim) == true && !string.IsNullOrEmpty(anim))
            _sprite.Play(anim);
    }

    public void TriggerAttackAnimation(Vector2 direction)
    {
        if (_sprite == null || _sprite.SpriteFrames == null) return;

        string cardinal = VectorToCardinal4(direction);
        string attackAnim = $"{AnimPrefix}attack_{cardinal}";

        if (_sprite?.SpriteFrames?.HasAnimation(attackAnim) == true)
            _sprite.Play(attackAnim);
    }

    public void AtualizarAnimacaoDeRede(Vector2 direcao, bool moving)
    {
        if (_sprite == null || _sprite.SpriteFrames == null) return;

        string currentAnim = _sprite.Animation.ToString();
        if (currentAnim.Contains("attack")) return;

        string cardinal = VectorToCardinal4(direcao);
        cardinal = RemapearDirecao(cardinal);

        if (!moving && _player != null)
        {
            float distToPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
            if (distToPlayer <= DistanciaAtaque + 10f)
            {
                Vector2 dirToPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
                string atkCardinal = VectorToCardinal4(dirToPlayer);
                string atkAnim = $"{AnimPrefix}attack_{atkCardinal}";
                if (_sprite?.SpriteFrames?.HasAnimation(atkAnim) == true)
                {
                    _sprite.Play(atkAnim);
                    return;
                }
            }
        }

        string state = moving && direcao.LengthSquared() > 0.01f ? "walk" : "idle";
        string desejada = $"{AnimPrefix}{state}_{cardinal}";

        TocarAnimacao(desejada);
    }

    private void TocarAnimacao(string desejada)
    {
        if (!string.IsNullOrEmpty(desejada) && _sprite?.SpriteFrames?.HasAnimation(desejada) == true)
        {
            if (_sprite.Animation != desejada || !_sprite.IsPlaying())
                _sprite.Play(desejada);
            _sprite.SpeedScale = 1.5f;
        }
    }

    public override void _Draw()
    {
        if (VidaMaxima <= 0) return;
        if (_vidaAtual <= 0) return;

        float w = _healthBarSize.X;
        float h = _healthBarSize.Y;
        float r = HealthBarRaio;
        var topLeft = _healthBarOffset - new Vector2(w / 2f, 0);
        float pct = Mathf.Clamp(_vidaAtual / (float)VidaMaxima, 0, 1);
        float fillW = w * pct;

        DesenharBarraArredondada(topLeft, w, h, r, _healthBarBackground);
        if (fillW > 0)
            DesenharBarraArredondada(topLeft, fillW, h, r, _healthBarForeground);
    }

    private void DesenharBarraArredondada(Vector2 topLeft, float width, float height, float radius, Color color)
    {
        float r = Mathf.Min(Mathf.Min(radius, width / 2f), height / 2f);
        Vector2 tl = topLeft;
        Vector2 br = topLeft + new Vector2(width, height);

        DrawRect(new Rect2(tl + new Vector2(r, 0), new Vector2(width - r * 2, height)), color);
        DrawRect(new Rect2(tl + new Vector2(0, r), new Vector2(width, height - r * 2)), color);
        DrawCircle(tl + new Vector2(r, r), r, color);
        DrawCircle(new Vector2(br.X - r, tl.Y + r), r, color);
        DrawCircle(new Vector2(tl.X + r, br.Y - r), r, color);
        DrawCircle(br - new Vector2(r, r), r, color);
    }

    public void SetVidaAtual(int health, int maxHealth)
    {
        VidaMaxima = maxHealth;
        _vidaAtual = Mathf.Clamp(health, 0, maxHealth);
        _vidaInicializada = true;
        AtualizarBarraBoss();
        QueueRedraw();
    }

    public void LevarDano(int quantidade)
    {
        _vidaAtual -= quantidade;

        _foiAtacado = true;
        _tempoAggro = TempoAggroAposDano;

        if (_estado != EstadoInimigo.Attack)
        {
            _estado = EstadoInimigo.Chase;
        }

        Modulate = Color.FromHtml("ff6666");
        if (GetTree() != null)
        {
            GetTree().CreateTimer(0.15f).Timeout += () => Modulate = Color.FromHtml("ffffff");
        }

        AtualizarBarraBoss();
        QueueRedraw();

        if (_vidaAtual <= 0)
        {
            _estado = EstadoInimigo.Dead;

            if (!IsNetworked && GetTree()?.CurrentScene != null)
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
        if (DropTable.Count == 0) return;

        var cenaDrop = GD.Load<PackedScene>("res://Itens/ItemColetavel.tscn");
        if (cenaDrop == null)
        {
            GD.PrintErr("[INIMIGO] Cena de drop nao encontrada: res://Itens/ItemColetavel.tscn");
            return;
        }

        int droppedCount = 0;
        foreach (var entry in DropTable)
        {
            if (entry.ItemId <= 0) continue;
            if (entry.Chance <= 0.0) continue;
            if (GD.RandRange(0.0, 1.0) > entry.Chance) continue;

            ItemResource item = CarregarItem(entry.ItemId);
            if (item == null) continue;

            var drop = cenaDrop.Instantiate<ItemColetavel>();
            drop.ItemContido = item;
            drop.GlobalPosition = GlobalPosition;
            GetParent().AddChild(drop);
            droppedCount++;
            GD.Print($"[INIMIGO] Drop: {item.Nome} (ID={entry.ItemId})");
        }

        if (droppedCount > 0)
            GD.Print($"[INIMIGO] Total drops: {droppedCount}");
    }

    private static ItemResource CarregarItem(int itemId)
    {
        string caminho = itemId switch
        {
            21 => "res://Itens/Peitoral.tres",
            100 => "res://Itens/PergaminhoDoPet.tres",
            _ => $"res://Itens/Item_{itemId}.tres",
        };

        if (ResourceLoader.Exists(caminho))
            return ResourceLoader.Load<ItemResource>(caminho);

        GD.PrintErr($"[INIMIGO] Item resource nao encontrado: {caminho} (ID={itemId})");
        return null;
    }

}
