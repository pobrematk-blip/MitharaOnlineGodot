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
    [Export] public float SeguirDistancia = 80f;
    [Export] public float Velocidade = 300f;
    [Export] public float ColetaRange = 150f;
    [Export] public float AtaqueRange = 60f;
    [Export] public float AtaqueCooldown = 0.8f;
    [Export] public int AtaqueDano = 5;
    [Export] public float GuardaRange = 200f;

    public PetMode ModoAtual { get; private set; } = PetMode.Seguir;
    public int PetID { get; set; }
    public string NomePet { get; set; } = "";
    public string AnimPrefix { get; set; } = "";
    private string _animPrefixo
    {
        get
        {
            string prefix = string.IsNullOrWhiteSpace(AnimPrefix) ? NomePet : AnimPrefix;
            return prefix.Trim().TrimEnd('_').ToLowerInvariant();
        }
    }
    public TipoPet TipoPet { get; set; }
    public bool Ativo { get; set; } = true;
    public bool ColetaAtiva { get; set; } = true;

    private Player _player;
    private AnimatedSprite2D _sprite;
    private Vector2 _direcao;
    private float _ultimoAtaque;
    private Node2D _alvoInimigo;
    private Vector2 _posicaoGuarda;
    private Timer _coletaTimer;
    private Area2D _areaColeta;
    private Area2D _areaAtaque;

    public override void _Ready()
    {
        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite != null)
            _sprite.Scale = new Vector2(0.7f, 0.7f);
        _posicaoGuarda = GlobalPosition;

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

        _coletaTimer = new Timer();
        _coletaTimer.WaitTime = 2.0f;
        _coletaTimer.OneShot = false;
        _coletaTimer.Timeout += OnColetaTimer;
        AddChild(_coletaTimer);
        _coletaTimer.Start();

        AtualizarAreaModo();
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
            if (distAlvo > GuardaRange * 1.5f)
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
        else if (distPlayer > SeguirDistancia * 2f)
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
        if (distPlayer > SeguirDistancia * 2f)
        {
            AtualizarSeguir(delta);

            if (_alvoInimigo == null || !IsInstanceValid(_alvoInimigo))
                ProcurarAlvoProximo();
            return;
        }

        if (_alvoInimigo != null && IsInstanceValid(_alvoInimigo))
        {
            float distAlvo = GlobalPosition.DistanceTo(_alvoInimigo.GlobalPosition);
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
        var inimigos = GetTree().GetNodesInGroup("Inimigos");
        float menorDist = Mathf.Max(AtaqueRange * 5f, 400f);
        Node2D alvo = null;

        foreach (var node in inimigos)
        {
            if (node is Node2D n && n != _player)
            {
                float d = GlobalPosition.DistanceTo(n.GlobalPosition);
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

        float agora = (float)Time.GetTicksMsec() / 1000f;
        if (agora - _ultimoAtaque < AtaqueCooldown) return;
        _ultimoAtaque = agora;

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
            if (_sprite?.SpriteFrames?.HasAnimation(anim) == true)
                _sprite.Play(anim);
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
        {
            if (_alvoInimigo.HasMeta("network_id"))
            {
                ulong targetId = (ulong)_alvoInimigo.GetMeta("network_id");
                gameNet.SendAttack(targetId);
            }
        }
        else
        {
            if (_alvoInimigo is Inimigo inimigo)
                inimigo.LevarDano(AtaqueDano);
        }

        CriarEfeitoAtaque();
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
        if (TipoPet != TipoPet.Loot) return;
        if (ModoAtual == PetMode.Atacar) return;
        if (!ColetaAtiva) return;

        var itemColetavel = area.GetParentOrNull<ItemColetavel>();
        if (itemColetavel != null && IsInstanceValid(itemColetavel))
        {
            if (_player != null)
            {
                float dist = GlobalPosition.DistanceTo(itemColetavel.GlobalPosition);
                if (dist <= ColetaRange)
                {
                    Vector2 dir = (itemColetavel.GlobalPosition - GlobalPosition).Normalized();
                    GlobalPosition += dir * Mathf.Min(dist, Velocidade * 0.5f);
                }
            }
        }
    }

    private void OnColetaTimer()
    {
        if (TipoPet != TipoPet.Loot || !Ativo || _player == null) return;
        if (ModoAtual == PetMode.Atacar) return;
        if (!ColetaAtiva) return;

        float distPlayer = GlobalPosition.DistanceTo(_player.GlobalPosition);
        if (distPlayer > SeguirDistancia * 3f) return;

        Node2D itemMaisProximo = null;
        float menorDist = ColetaRange;

        var lootNodes = GetTree().GetNodesInGroup("Loot");
        foreach (var node in lootNodes)
        {
            if (node is Node2D n && IsInstanceValid(n))
            {
                float d = GlobalPosition.DistanceTo(n.GlobalPosition);
                if (d < menorDist)
                {
                    menorDist = d;
                    itemMaisProximo = n;
                }
            }
        }

        if (itemMaisProximo != null)
        {
            Vector2 dir = (itemMaisProximo.GlobalPosition - GlobalPosition).Normalized();
            GlobalPosition += dir * Velocidade * 0.5f;

            float dist = GlobalPosition.DistanceTo(itemMaisProximo.GlobalPosition);
            if (dist < 30f && _player != null && IsInstanceValid(_player))
            {
                _player.TryPickupLoot();
            }
        }
    }

    private void AtualizarAnimacao()
    {
        if (_sprite == null || string.IsNullOrEmpty(_animPrefixo)) return;

        string baseAnim = "";
        if (_direcao.Length() > 0.1f)
        {
            if (Mathf.Abs(_direcao.X) > Mathf.Abs(_direcao.Y))
                baseAnim = _direcao.X > 0 ? $"{_animPrefixo}_walk_right" : $"{_animPrefixo}_walk_left";
            else
                baseAnim = _direcao.Y > 0 ? $"{_animPrefixo}_walk_down" : $"{_animPrefixo}_walk_up";
        }
        else
        {
            string curAnim = _sprite.Animation.ToString();
            string lastDir = "down";
            if (!string.IsNullOrEmpty(curAnim))
            {
                lastDir = curAnim
                    .Replace($"{_animPrefixo}_walk_", "")
                    .Replace($"{_animPrefixo}_idle_", "")
                    .Replace($"{_animPrefixo}_attack_", "");
                if (string.IsNullOrEmpty(lastDir)) lastDir = "down";
            }
            baseAnim = $"{_animPrefixo}_idle_{lastDir}";
        }

        if (_sprite?.SpriteFrames?.HasAnimation(baseAnim) == true)
            _sprite.Play(baseAnim);
    }
}
