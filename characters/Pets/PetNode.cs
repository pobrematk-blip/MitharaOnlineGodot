using Godot;
using System;
using System.Collections.Generic;

public enum PetMode
{
    Seguir,
    Guarda,
    Atacar
}

public partial class PetNode : Node2D
{
    public const float TileSize = 32f;
    public const float OwnerAttackLeashRange = TileSize * 20f;
    public const float OneTileAttackRange = 96f;
    public const float PetLootRange = TileSize * 20f;
    public const int PetAttackSkillId = -100;
    private const float LootPickupTouchRange = 28f;
    private const float LootPickupRequestCooldown = 0.35f;

    [Export] public float SeguirDistancia = 80f;
    [Export] public float Velocidade = 300f;
    [Export] public float ColetaRange = PetLootRange;
    [Export] public float AtaqueRange = OneTileAttackRange;
    [Export] public float AtaqueCooldown = 0.8f;
    [Export] public int AtaqueDano = 5;
    [Export] public int VidaMaxima = 200;
    [Export] public int Defesa = 12;
    [Export] public float GuardaRange = 200f;
    [Export] public float EscalaVisual = 1.1f;

    public PetMode ModoAtual { get; private set; } = PetMode.Seguir;
    public int PetID { get; set; }
    public string NomePet { get; set; } = "Goblin";
    public string AnimPrefix { get; set; } = "Goblin";
    private string _animPrefixo
    {
        get
        {
            string prefix = string.IsNullOrWhiteSpace(AnimPrefix) ? NomePet : AnimPrefix;
            return prefix.Trim().TrimEnd('_');
        }
    }
    public TipoPet TipoPet { get; set; }
    public bool Ativo { get; set; } = true;
    public bool ColetaAtiva { get; set; } = true;
    public int VidaAtual { get; private set; }

    private Player _player;
    private AnimatedSprite2D _sprite;
    private Vector2 _direcao;
    private float _ultimoAtaque;
    private Node2D _alvoInimigo;
    private Vector2 _posicaoGuarda;
    private Godot.Timer _coletaTimer;
    private Area2D _areaColeta;
    private Area2D _areaAtaque;
    private Control _vidaBarRoot;
    private ColorRect _vidaFill;
    private Node2D _alvoLoot;
    private float _ultimoPedidoColeta;
    private float _proximaBuscaLoot;
    private float _ataqueAnimAte;
    private string _ultimaDirecaoAnim = "down";
    private Vector2 _posicaoAnimAnterior;
    private bool _temPosicaoAnimAnterior;

    public void DefinirDono(Player player)
    {
        _player = player;
    }

    public void ConfigurarAtributos(int hp, int defesa)
    {
        VidaMaxima = Mathf.Max(1, hp);
        Defesa = Mathf.Max(0, defesa);
        VidaAtual = VidaMaxima;
        AtualizarBarraVida();
    }

    public override void _Ready()
    {
        _player ??= GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
        {
            _sprite.Scale = Vector2.One * EscalaVisual;
            TocarAnimacao($"{_animPrefixo}_idle_down");
        }
        _posicaoGuarda = GlobalPosition;
        _posicaoAnimAnterior = GlobalPosition;
        _temPosicaoAnimAnterior = true;

        _areaColeta = new Area2D();
        var colShape = new CollisionShape2D();
        var circle = new CircleShape2D();
        circle.Radius = ColetaRange;
        colShape.Shape = circle;
        _areaColeta.AddChild(colShape);
        AddChild(_areaColeta);
        _areaColeta.AreaEntered += OnItemEntrouNaArea;
        _areaColeta.Monitoring = true;
        _areaColeta.CollisionLayer = 0;
        _areaColeta.CollisionMask = 0;

        _areaAtaque = new Area2D();
        var atkShape = new CollisionShape2D();
        var atkCircle = new CircleShape2D();
        atkCircle.Radius = AtaqueRange;
        atkShape.Shape = atkCircle;
        _areaAtaque.AddChild(atkShape);
        AddChild(_areaAtaque);
        _areaAtaque.Monitoring = false;
        _areaAtaque.CollisionLayer = 0;
        _areaAtaque.CollisionMask = 0;

        _coletaTimer = new Godot.Timer();
        _coletaTimer.WaitTime = 2.0f;
        _coletaTimer.OneShot = false;
        _coletaTimer.Timeout += OnColetaTimer;
        AddChild(_coletaTimer);
        _coletaTimer.Start();

        if (VidaAtual <= 0)
            VidaAtual = VidaMaxima;
        CriarBarraVida();
        AtualizarBarraVida();
        AtualizarAreaModo();
    }

    public void LevarDano(int danoBruto)
    {
        if (!Ativo || danoBruto <= 0)
            return;

        int danoFinal = Mathf.Max(1, danoBruto - Defesa);
        VidaAtual = Mathf.Max(0, VidaAtual - danoFinal);
        AtualizarBarraVida();
        GD.Print($"[PET] {NomePet} levou {danoFinal} de dano. Vida: {VidaAtual}/{VidaMaxima}");

        if (VidaAtual <= 0)
        {
            Ativo = false;
            QueueFree();
        }
    }

    private void CriarBarraVida()
    {
        if (_vidaBarRoot != null && IsInstanceValid(_vidaBarRoot))
            return;

        _vidaBarRoot = new Control
        {
            Name = "PetHealthBar",
            Position = new Vector2(-28f, -48f),
            Size = new Vector2(56f, 5f),
            CustomMinimumSize = new Vector2(56f, 5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 20,
        };

        var bg = new ColorRect
        {
            Name = "Background",
            Color = new Color(0.02f, 0.02f, 0.025f, 0.88f),
            Position = Vector2.Zero,
            Size = new Vector2(56f, 5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _vidaFill = new ColorRect
        {
            Name = "Fill",
            Color = new Color(0.86f, 0.09f, 0.10f, 0.95f),
            Position = new Vector2(1f, 1f),
            Size = new Vector2(54f, 3f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        _vidaBarRoot.AddChild(bg);
        _vidaBarRoot.AddChild(_vidaFill);
        AddChild(_vidaBarRoot);
    }

    private void AtualizarBarraVida()
    {
        if (_vidaFill == null || !IsInstanceValid(_vidaFill))
            return;

        float pct = VidaMaxima > 0 ? Mathf.Clamp(VidaAtual / (float)VidaMaxima, 0f, 1f) : 0f;
        _vidaFill.Size = new Vector2(54f * pct, 3f);
        _vidaBarRoot.Visible = VidaAtual > 0;
    }

    public void DefinirModo(PetMode novoModo)
    {
        ModoAtual = novoModo;
        _posicaoGuarda = GlobalPosition;
        _alvoInimigo = null;
        AtualizarAreaModo();
    }

    private void AtualizarAreaModo()
    {
        switch (ModoAtual)
        {
            case PetMode.Seguir:
                _areaAtaque.Monitoring = false;
                break;
            case PetMode.Guarda:
                _areaAtaque.Monitoring = TipoPet == TipoPet.Combate;
                break;
            case PetMode.Atacar:
                _areaAtaque.Monitoring = TipoPet == TipoPet.Combate;
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (!Ativo || _player == null) return;

        bool coletando = ModoAtual != PetMode.Atacar && AtualizarColeta(delta);
        if (coletando)
        {
            AtualizarAnimacao();
            return;
        }

        switch (ModoAtual)
        {
            case PetMode.Seguir:
                AtualizarSeguir(delta);
                break;
            case PetMode.Guarda:
                AtualizarGuarda(delta);
                break;
            case PetMode.Atacar:
                AtualizarAtacar(delta);
                break;
        }

        AtualizarAnimacao();
    }

    private void AtualizarSeguir(double delta)
    {
        if (!IsInstanceValid(_player)) return;

        if (TipoPet == TipoPet.Combate)
        {
            if (_alvoInimigo == null || !IsInstanceValid(_alvoInimigo))
                ProcurarAlvoProximo();

            if (_alvoInimigo != null && IsInstanceValid(_alvoInimigo))
            {
                float distAlvoDoPlayer = _player.GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
                if (distAlvoDoPlayer <= OwnerAttackLeashRange)
                {
                    float distAlvo = GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
                    if (distAlvo <= AtaqueRange)
                        AtacarAlvo();
                    else
                    {
                        _direcao = (_alvoInimigo.GlobalPosition - GlobalPosition).Normalized();
                        GlobalPosition += _direcao * Velocidade * (float)delta;
                    }
                    return;
                }

                _alvoInimigo = null;
            }
        }

        float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

        if (dist > SeguirDistancia + 10f)
        {
            _direcao = (_player.GlobalPosition - GlobalPosition).Normalized();
            GlobalPosition += _direcao * Velocidade * (float)delta;
        }
        else if (dist < SeguirDistancia - 20f)
        {
            _direcao = (GlobalPosition - _player.GlobalPosition).Normalized();
            GlobalPosition += _direcao * Velocidade * 0.3f * (float)delta;
        }
        else
        {
            _direcao = Vector2.Zero;
        }
    }

    private void AtualizarGuarda(double delta)
    {
        if (!IsInstanceValid(_player)) return;

        float distPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        float distGuarda = GlobalPosition.DistanceTo(_posicaoGuarda);

        if (_alvoInimigo != null && IsInstanceValid(_alvoInimigo))
        {
            float distAlvo = GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
            float distAlvoDoPlayer = _player.GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
            if (distAlvoDoPlayer > OwnerAttackLeashRange)
            {
                _alvoInimigo = null;
            }
            else if (distAlvo <= AtaqueRange)
            {
                AtacarAlvo();
            }
            else
            {
                _direcao = (_alvoInimigo.GlobalPosition - GlobalPosition).Normalized();
                GlobalPosition += _direcao * Velocidade * (float)delta;
            }
        }
        else
        {
            ProcurarAlvoProximo();
            if (_alvoInimigo != null && IsInstanceValid(_alvoInimigo))
            {
                float distAlvo = GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
                if (distAlvo <= AtaqueRange)
                    AtacarAlvo();
                else
                {
                    _direcao = (_alvoInimigo.GlobalPosition - GlobalPosition).Normalized();
                    GlobalPosition += _direcao * Velocidade * (float)delta;
                }
                return;
            }
        }

        if (distPlayer > SeguirDistancia * 2f)
        {
            _direcao = (_player.GlobalPosition - GlobalPosition).Normalized();
            GlobalPosition += _direcao * Velocidade * (float)delta;
        }
        else if (distGuarda > 20f)
        {
            _direcao = (_posicaoGuarda - GlobalPosition).Normalized();
            GlobalPosition += _direcao * Velocidade * 0.5f * (float)delta;
        }
        else
        {
            _direcao = Vector2.Zero;
        }
    }

    private void AtualizarAtacar(double delta)
    {
        if (!IsInstanceValid(_player)) return;

        float distPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (distPlayer > OwnerAttackLeashRange)
        {
            AtualizarSeguir(delta);

            if (_alvoInimigo == null || !IsInstanceValid(_alvoInimigo))
                ProcurarAlvoProximo();
            return;
        }

        if (_alvoInimigo != null && IsInstanceValid(_alvoInimigo))
        {
            float distAlvo = GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
            float distAlvoDoPlayer = _player.GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
            if (distAlvoDoPlayer > OwnerAttackLeashRange)
            {
                _alvoInimigo = null;
                AtualizarSeguir(delta);
                return;
            }

            if (distAlvo <= AtaqueRange)
            {
                AtacarAlvo();
            }
            else
            {
                _direcao = (_alvoInimigo.GlobalPosition - GlobalPosition).Normalized();
                GlobalPosition += _direcao * Velocidade * (float)delta;
            }
        }
        else
        {
            ProcurarAlvoProximo();
            if (_alvoInimigo == null)
                AtualizarSeguir(delta);
        }
    }

    private void ProcurarAlvoProximo()
    {
        if (!IsInstanceValid(_player))
            return;

        if (_player.TryGetSelectedCombatTarget(out var targetSelecionado)
            && EhAlvoValidoDoPet(targetSelecionado)
            && _player.GlobalPosition.DistanceTo(targetSelecionado.GlobalPosition) <= OwnerAttackLeashRange)
        {
            _alvoInimigo = targetSelecionado;
            return;
        }

        var inimigos = GetTree().GetNodesInGroup("Inimigos");
        float menorDist = OwnerAttackLeashRange;
        Node2D alvo = null;

        foreach (var node in inimigos)
        {
            if (node is Node2D n && EhAlvoValidoDoPet(n))
            {
                float d = _player.GlobalPosition.DistanceTo(n.GlobalPosition);
                if (d < menorDist && IsInstanceValid(n))
                {
                    menorDist = d;
                    alvo = n;
                }
            }
        }

        _alvoInimigo = alvo;
    }

    private void AtacarAlvo()
    {
        if (_alvoInimigo == null || !IsInstanceValid(_alvoInimigo)) return;
        if (!EhAlvoValidoDoPet(_alvoInimigo))
        {
            _alvoInimigo = null;
            return;
        }

        float agora = (float)Time.GetTicksMsec() / 1000f;
        if (agora - _ultimoAtaque < AtaqueCooldown) return;
        _ultimoAtaque = agora;

        Vector2 alvoDir = _alvoInimigo.GlobalPosition - GlobalPosition;
        if (alvoDir.LengthSquared() > 0.001f)
            _direcao = alvoDir.Normalized();

        if (_sprite != null)
        {
            string anim = _direcao switch
            {
                Vector2 v when v.Y < -0.5f => $"{_animPrefixo}_attack_up",
                Vector2 v when v.Y > 0.5f => $"{_animPrefixo}_attack_down",
                Vector2 v when v.X < -0.5f => $"{_animPrefixo}_attack_left",
                Vector2 v when v.X > 0.5f => $"{_animPrefixo}_attack_right",
                _ => $"{_animPrefixo}_attack_down"
            };
            TocarAnimacaoComFallback(anim);
            _ataqueAnimAte = agora + Mathf.Min(0.55f, Mathf.Max(0.25f, AtaqueCooldown * 0.65f));
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
        {
            if (_alvoInimigo.HasMeta("network_id"))
            {
                ulong targetId = (ulong)_alvoInimigo.GetMeta("network_id");
                gameNet.SendAttack(targetId, PetAttackSkillId);
            }
        }
        else
        {
            if (_alvoInimigo is Inimigo inimigo)
                inimigo.LevarDano(AtaqueDano);
        }

        CriarEfeitoAtaque();
    }

    private bool EhAlvoValidoDoPet(Node2D alvo)
    {
        if (alvo == null || !IsInstanceValid(alvo))
            return false;

        if (alvo == _player || alvo is Player || alvo.IsInGroup("player") || alvo.IsInGroup("RemotePlayers"))
            return false;

        if (alvo is not Inimigo)
            return false;

        if (alvo.HasMeta("dying") && alvo.GetMeta("dying").AsBool())
            return false;

        return alvo.IsInGroup("Inimigos");
    }

    private void CriarEfeitoAtaque()
    {
        if (_alvoInimigo == null) return;
        var dano = new Label();
        dano.Text = AtaqueDano.ToString();
        dano.Modulate = Colors.Orange;
        dano.Scale = new Vector2(0.8f, 0.8f);
        dano.GlobalPosition = _alvoInimigo.GlobalPosition + new Vector2(
            (float)GD.RandRange(-10, 10),
            (float)GD.RandRange(-20, 0)
        );
        GetParent().AddChild(dano);

        var tween = CreateTween();
        tween.TweenProperty(dano, "position:y", dano.Position.Y - 30, 0.6f);
        tween.TweenProperty(dano, "modulate:a", 0.0f, 0.3f);
        tween.TweenCallback(Callable.From(() => dano.QueueFree()));
    }

    private void OnItemEntrouNaArea(Area2D area)
    {
        if (ModoAtual == PetMode.Atacar) return;
        if (!ColetaAtiva) return;

        var itemColetavel = area.GetParentOrNull<ItemColetavel>();
        if (itemColetavel != null
            && IsInstanceValid(itemColetavel)
            && _player != null
            && _player.GlobalPosition.DistanceTo(itemColetavel.GlobalPosition) <= ColetaRange)
            _alvoLoot = itemColetavel;
    }

    private void OnColetaTimer()
    {
        if (!Ativo || _player == null) return;
        if (ModoAtual == PetMode.Atacar) return;
        if (!ColetaAtiva) return;

        float distPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (distPlayer > ColetaRange + SeguirDistancia) return;

        _alvoLoot = EncontrarLootMaisProximo();
    }

    private Node2D EncontrarLootMaisProximo()
    {
        if (!ColetaAtiva || _player == null || !IsInstanceValid(_player))
            return null;

        Node2D itemMaisProximo = null;
        float menorDist = ColetaRange;
        var lootNodes = GetTree().GetNodesInGroup("Loot");
        foreach (var node in lootNodes)
        {
            if (node is Node2D n && IsInstanceValid(n))
            {
                if (_player.GlobalPosition.DistanceTo(n.GlobalPosition) > ColetaRange)
                    continue;

                float d = GlobalPosition.DistanceTo(n.GlobalPosition);
                if (d < menorDist)
                {
                    menorDist = d;
                    itemMaisProximo = n;
                }
            }
        }

        return itemMaisProximo;
    }

    private bool AtualizarColeta(double delta)
    {
        if (!ColetaAtiva || _player == null || !IsInstanceValid(_player))
            return false;

        if (_alvoLoot == null
            || !IsInstanceValid(_alvoLoot)
            || _player.GlobalPosition.DistanceTo(_alvoLoot.GlobalPosition) > ColetaRange)
        {
            float agora = (float)Time.GetTicksMsec() / 1000f;
            if (agora < _proximaBuscaLoot)
                return false;

            _proximaBuscaLoot = agora + 0.25f;
            _alvoLoot = EncontrarLootMaisProximo();
        }

        if (_alvoLoot == null || !IsInstanceValid(_alvoLoot))
            return false;

        Vector2 toLoot = _alvoLoot.GlobalPosition - GlobalPosition;
        float dist = toLoot.Length();
        if (dist <= LootPickupTouchRange)
        {
            _direcao = Vector2.Zero;
            SolicitarColetaDoLoot(_alvoLoot);
            return true;
        }

        _direcao = toLoot.Normalized();
        GlobalPosition += _direcao * Velocidade * (float)delta;
        return true;
    }

    private void SolicitarColetaDoLoot(Node2D lootNode)
    {
        if (lootNode == null || !IsInstanceValid(lootNode) || !lootNode.HasMeta("loot_id"))
            return;

        float agora = (float)Time.GetTicksMsec() / 1000f;
        if (agora - _ultimoPedidoColeta < LootPickupRequestCooldown)
            return;
        _ultimoPedidoColeta = agora;

        long rawLootId = lootNode.GetMeta("loot_id").AsInt64();
        if (rawLootId <= 0)
            return;

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet?.HasActivePetCollar != true)
            return;

        gameNet.SendLootPickup((ulong)rawLootId);
    }

    private void AtualizarAnimacao()
    {
        if (_sprite == null || string.IsNullOrEmpty(_animPrefixo)) return;

        float agora = (float)Time.GetTicksMsec() / 1000f;
        if (agora < _ataqueAnimAte)
            return;

        bool moveuNesteFrame = !_temPosicaoAnimAnterior
            || GlobalPosition.DistanceSquaredTo(_posicaoAnimAnterior) > 1.0f;
        _posicaoAnimAnterior = GlobalPosition;
        _temPosicaoAnimAnterior = true;

        string baseAnim = "";
        if (moveuNesteFrame && _direcao.Length() > 0.1f)
        {
            if (Mathf.Abs(_direcao.X) > Mathf.Abs(_direcao.Y))
            {
                _ultimaDirecaoAnim = _direcao.X > 0 ? "right" : "left";
                baseAnim = $"{_animPrefixo}_walk_{_ultimaDirecaoAnim}";
            }
            else
            {
                _ultimaDirecaoAnim = _direcao.Y > 0 ? "down" : "up";
                baseAnim = $"{_animPrefixo}_walk_{_ultimaDirecaoAnim}";
            }
        }
        else
        {
            baseAnim = $"{_animPrefixo}_idle_{_ultimaDirecaoAnim}";
        }

        if (TocarAnimacaoComFallback(baseAnim))
        {
            return;
        }

        if (_sprite?.SpriteFrames != null)
        {
            var names = _sprite.SpriteFrames.GetAnimationNames();
            if (names.Length > 0 && string.IsNullOrEmpty(_sprite.Animation.ToString()))
                _sprite.Play(names[0]);
        }
    }

    private bool TocarAnimacao(string anim)
    {
        if (_sprite?.SpriteFrames == null || string.IsNullOrWhiteSpace(anim))
            return false;

        if (_sprite.SpriteFrames.HasAnimation(anim))
        {
            _sprite.Play(anim);
            return true;
        }

        string alvo = anim.Trim();
        foreach (string nome in _sprite.SpriteFrames.GetAnimationNames())
        {
            if (string.Equals(nome, alvo, StringComparison.OrdinalIgnoreCase))
            {
                _sprite.Play(nome);
                return true;
            }
        }

        return false;
    }

    private bool TocarAnimacaoComFallback(string anim)
    {
        if (TocarAnimacao(anim))
            return true;

        string prefix = _animPrefixo;
        if (!string.IsNullOrWhiteSpace(prefix) && anim.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
        {
            string semPrefixo = anim[(prefix.Length + 1)..];
            if (TocarAnimacao(semPrefixo))
                return true;
        }

        string[] alternativos =
        {
            anim.Replace("walk_", "Walk_"),
            anim.Replace("attack_", "Attack_"),
            anim.Replace("idle_", "Idle_"),
        };
        foreach (string alt in alternativos)
        {
            if (TocarAnimacao(alt))
                return true;

            if (!string.IsNullOrWhiteSpace(prefix) && alt.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase))
            {
                string semPrefixo = alt[(prefix.Length + 1)..];
                if (TocarAnimacao(semPrefixo))
                    return true;
            }
        }

        return false;
    }
}
