using Godot;
using System;

public enum ProjectileSoundType
{
    None,
    Arrow,
    Fireball,
}

public partial class Projetil : Area2D
{
    [Export] public ProjectileSoundType SomDisparo { get; set; } = ProjectileSoundType.None;
    [Export] public float VolumeDisparoDb { get; set; } = -2f;

    // Valores padrão (serão alterados pelo Player.cs dependendo da classe)
    public float Speed = 600.0f; 
    public int DanoMin = 8;              // Dano mínimo
    public int DanoMax = 12;             // Dano máximo
    public bool EhDanoMagico = false;    // Se for true, usa DanoMagico, se false usa DanoFisico
    public bool VisualOnlyOnline = false;
    
    private Vector2 _direcao = Vector2.Zero;
    private EquipamentoComponent _equipamentoDoPlayer;  // Referência ao sistema de status do Player
    private Node _dono;
    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D")
            ?? FindChild("AnimatedSprite2D", true, false) as AnimatedSprite2D;
        ReiniciarAnimacaoVisual();
        TocarSomDisparo();

        // Conecta o sinal para saber quando o projétil bateu em algo
        BodyEntered += OnBodyEntered;

        // Se o projétil não bater em nada, ele se destrói sozinho em 3 segundos
        GetTree().CreateTimer(3.0f).Timeout += QueueFree;
        
        // Tenta encontrar o EquipamentoComponent do Player na cena
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player != null)
        {
            _equipamentoDoPlayer = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Usa GlobalPosition para alinhar perfeitamente com o mundo
        GlobalPosition += _direcao * Speed * (float)delta;
    }

    // Função que recebe a direção de quem disparou
    public void DefinirDirecao(Vector2 direcao)
    {
        _direcao = direcao.Normalized();
        
        // Rotaciona o sprite para apontar para onde está voando
        Rotation = _direcao.Angle();
    }

    public void DefinirDono(Node dono)
    {
        _dono = dono;
    }

    private double _tempoDeVida = 0;

    public override void _Process(double delta)
    {
        _tempoDeVida += delta;
        if (_animatedSprite != null && !_animatedSprite.IsPlaying())
            ReiniciarAnimacaoVisual();
    }

    private void ReiniciarAnimacaoVisual()
    {
        if (_animatedSprite == null || _animatedSprite.SpriteFrames == null)
            return;

        string anim = _animatedSprite.Animation.ToString();
        if (string.IsNullOrWhiteSpace(anim) || !_animatedSprite.SpriteFrames.HasAnimation(anim))
            anim = _animatedSprite.SpriteFrames.HasAnimation("default") ? "default" : "";

        if (string.IsNullOrWhiteSpace(anim))
            return;

        _animatedSprite.SpriteFrames.SetAnimationLoop(anim, true);
        _animatedSprite.Animation = anim;
        _animatedSprite.Frame = 0;
        _animatedSprite.Play(anim);
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_dono != null && IsInstanceValid(_dono) && body == _dono)
            return;

        var collBody = body as CollisionObject2D;
        GD.Print($"[PROJETIL] Colidiu com: {body.Name} (tipo={body.GetType().Name}, layer={(collBody?.CollisionLayer ?? 0)}, grupo Inimigos={body.IsInGroup("Inimigos")})");

        if (body is Player)
        {
            if (_tempoDeVida < 0.15)
                return;
            QueueFree();
            return;
        }

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        bool online = gameNet != null && gameNet.IsConnected;

        if (body.IsInGroup("Inimigos"))
        {
            if (VisualOnlyOnline)
            {
                QueueFree();
                return;
            }

            if (online)
            {
                ulong? targetId = null;
                if (body.HasMeta("network_id"))
                {
                    targetId = (ulong)body.GetMeta("network_id");
                    GD.Print($"[PROJETIL] Online - enviando C2S_Attack para target {targetId.Value}");
                    gameNet.SendAttack(targetId.Value);
                }
                else
                {
                    GD.PrintErr("[PROJETIL] Inimigo sem network_id! N?o ? poss?vel atacar online.");
                }
            }
            else
            {
                GD.PrintErr("[PROJETIL] Dano local bloqueado. O combate deve passar pelo servidor.");
            }
        }

        QueueFree();
    }

    private void TocarSomDisparo()
    {
        if (SomDisparo == ProjectileSoundType.None)
            return;

        var stream = CarregarSomDisparo(SomDisparo);
        if (stream == null)
        {
            GD.PrintErr($"[PROJETIL] Som de disparo nao encontrado: {SomDisparo}");
            return;
        }

        var audio = new AudioStreamPlayer2D
        {
            Name = $"SomProjetil_{SomDisparo}",
            Stream = stream,
            VolumeDb = VolumeDisparoDb,
            MaxDistance = 850f,
            Attenuation = 0.35f,
            GlobalPosition = GlobalPosition,
        };

        audio.Finished += () =>
        {
            if (IsInstanceValid(audio))
                audio.QueueFree();
        };

        var parent = GetParent();
        if (parent != null)
            parent.AddChild(audio);
        else
            AddChild(audio);

        audio.Play();
    }

    private static AudioStream CarregarSomDisparo(ProjectileSoundType tipo)
    {
        string[] paths = tipo switch
        {
            ProjectileSoundType.Arrow => new[]
            {
                "res://audio/Ataque_do_Arco.wav",
                "res://Audio/Ataque_do_Arco.wav",
                "res://audio/Ataque de Arco.wav",
                "res://Audio/Ataque de Arco.wav",
                "res://audio/ataque de arco.wav",
                "res://Audio/ataque de arco.wav",
            },
            ProjectileSoundType.Fireball => new[]
            {
                "res://audio/Ataque_bola_fogo.wav",
                "res://Audio/Ataque_bola_fogo.wav",
            },
            _ => Array.Empty<string>(),
        };

        foreach (string path in paths)
        {
            if (ResourceLoader.Exists(path))
                return ResourceLoader.Load<AudioStream>(path);
        }

        return null;
    }
}
