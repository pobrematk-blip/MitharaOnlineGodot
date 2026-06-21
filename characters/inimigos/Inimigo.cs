using Godot;

public partial class Inimigo : CharacterBody2D
{
    [Export] public string NomeDoInimigo = "Monstro";
    [Export] public int VidaMaxima = 30;
    [Export] public float Velocidade = 100.0f;
    [Export] public float DistanciaAtaque = 45.0f;
    [Export] public int DanoDoAtaque = 10;
    [Export] public float TempoEntreAtaques = 1.2f;
    [Export] public bool IsBoss = false;
    [Export] public int ExperienciaDropada = 20;
    [Export] public Godot.Collections.Array<DropEntry> DropTable = new();
    [Export] public int PetID = 0;
    [Export] public string AnimPrefix = "goblin_";
    [Export] public string MobType = "goblin";

    private readonly Vector2 _healthBarSize = new(80, 6);
    private readonly Vector2 _healthBarOffset = new(0, -48);
    private readonly Color _healthBarBackground = new(0, 0, 0, 0.35f);
    private readonly Color _healthBarForeground = new(0.85f, 0.15f, 0.15f, 1);

    private AnimatedSprite2D _sprite;
    private Vector2 _facingDirection = Vector2.Down;
    private Vector2 _networkDirection = Vector2.Down;
    private bool _networkMoving;
    private int _vidaAtual;
    private bool _vidaInicializada;
    private bool _avisouSemNetworkId;
    private bool _avisouDanoLocalBloqueado;

    public int VidaAtual => _vidaAtual;
    public int VidaMax => VidaMaxima;
    public bool IsNetworked => HasMeta("network_id");
    public Vector2 NetworkTargetPos { get; set; }

    public override void _Ready()
    {
        AddToGroup("Inimigos");

        CollisionLayer = 4u;
        CollisionMask = 0u;
        MotionMode = MotionModeEnum.Floating;
        FloorStopOnSlope = false;
        Velocity = Vector2.Zero;
        ZIndex = 0;
        ZAsRelative = true;

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite == null)
        {
            GD.PrintErr($"[INIMIGO] {Name}: AnimatedSprite2D n?o encontrado.");
        }
        else
        {
            GarantirSpriteFrames();
            _sprite.AnimationFinished += OnAnimationFinished;
            TocarAnimacao($"idle_{CardinalDirection(_facingDirection)}", true);
        }

        if (!_vidaInicializada)
            _vidaAtual = VidaMaxima;

        NetworkTargetPos = GlobalPosition;
        DefinirPetPadrao();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = Vector2.Zero;

        if (!IsNetworked)
        {
            if (!_avisouSemNetworkId)
            {
                _avisouSemNetworkId = true;
                GD.PrintErr($"[INIMIGO] {Name} ignorado: monstro sem network_id no cliente. No MMORPG, monstros nascem e se movem pelo servidor.");
            }

            return;
        }

        Vector2 previousPosition = GlobalPosition;
        Vector2 toTarget = NetworkTargetPos - GlobalPosition;
        float distance = toTarget.Length();

        if (distance > 96f)
        {
            GlobalPosition = NetworkTargetPos;
        }
        else if (distance <= 0.5f)
        {
            GlobalPosition = NetworkTargetPos;
        }
        else
        {
            // Suavizacao independente do FPS, processada no mesmo tick da animacao.
            float t = 1.0f - Mathf.Exp(-(float)delta * 12f);
            GlobalPosition = GlobalPosition.Lerp(NetworkTargetPos, t);
        }

        float visualMoveSq = GlobalPosition.DistanceSquaredTo(previousPosition);
        bool aindaTemDeslocamento = GlobalPosition.DistanceSquaredTo(NetworkTargetPos) > 0.25f;
        bool visualmenteMovendo = _networkMoving && (visualMoveSq > 0.0001f || aindaTemDeslocamento);
        AtualizarAnimacaoDeRede(_networkDirection, visualmenteMovendo);
    }

    public void DefinirPosicaoInicial(Vector2 posicao)
    {
        GlobalPosition = posicao;
        NetworkTargetPos = posicao;
    }

    public void SetNetworkState(Vector2 posicao, Vector2 direcao, bool moving)
    {
        NetworkTargetPos = posicao;
        _networkMoving = moving;

        if (direcao.LengthSquared() > 0.001f)
            _networkDirection = direcao.Normalized();
    }

    public void SetVidaAtual(int health, int maxHealth)
    {
        VidaMaxima = Mathf.Max(maxHealth, 1);
        _vidaAtual = Mathf.Clamp(health, 0, VidaMaxima);
        _vidaInicializada = true;
        QueueRedraw();
    }

    public void AtualizarAnimacaoDeRede(Vector2 direcao, bool moving)
    {
        if (_sprite == null || _sprite.SpriteFrames == null)
            return;

        string currentAnim = _sprite.Animation.ToString();
        if (currentAnim.Contains("attack") && _sprite.IsPlaying())
            return;

        if (direcao.LengthSquared() > 0.001f)
            AtualizarDirecaoEstavel(direcao);

        string state = moving ? "walk" : "idle";
        TocarAnimacao($"{state}_{CardinalDirection(_facingDirection)}");
    }

    public void TriggerAttackAnimation(Vector2 direction)
    {
        if (direction.LengthSquared() > 0.001f)
            AtualizarDirecaoEstavel(direction, true);

        TocarAnimacao($"attack_{CardinalDirection(_facingDirection)}", true);
    }

    private void AtualizarDirecaoEstavel(Vector2 direction, bool force = false)
    {
        float absX = Mathf.Abs(direction.X);
        float absY = Mathf.Abs(direction.Y);
        bool facingHorizontal = Mathf.Abs(_facingDirection.X) > 0.5f;
        bool facingVertical = Mathf.Abs(_facingDirection.Y) > 0.5f;

        bool useHorizontal;
        if (force || (!facingHorizontal && !facingVertical))
            useHorizontal = absX >= absY;
        else if (facingHorizontal)
            useHorizontal = absY <= absX * 1.25f;
        else
            useHorizontal = absX > absY * 1.25f;

        if (useHorizontal && absX > 0.001f)
            _facingDirection = direction.X >= 0f ? Vector2.Right : Vector2.Left;
        else if (absY > 0.001f)
            _facingDirection = direction.Y >= 0f ? Vector2.Down : Vector2.Up;
    }

    public void LevarDano(int quantidade)
    {
        if (!_avisouDanoLocalBloqueado)
        {
            _avisouDanoLocalBloqueado = true;
            GD.PrintErr($"[INIMIGO] Dano local bloqueado em {NomeDoInimigo}. O cliente deve enviar ataque ao servidor e aguardar vida atualizada.");
        }
    }

    public static void PreencherDropTable(string mobType, Godot.Collections.Array<DropEntry> table)
    {
        // Compatibilidade com cenas antigas. Drop real pertence ao servidor.
    }

    public override void _Draw()
    {
        if (VidaMaxima <= 0 || _vidaAtual <= 0)
            return;

        float pct = Mathf.Clamp(_vidaAtual / (float)VidaMaxima, 0f, 1f);
        Vector2 topLeft = _healthBarOffset - new Vector2(_healthBarSize.X / 2f, 0);

        DrawRect(new Rect2(topLeft, _healthBarSize), _healthBarBackground);
        DrawRect(new Rect2(topLeft, new Vector2(_healthBarSize.X * pct, _healthBarSize.Y)), _healthBarForeground);
    }

    private void GarantirSpriteFrames()
    {
        if (_sprite == null)
            return;

        bool precisaGerar = _sprite.SpriteFrames == null || _sprite.SpriteFrames.GetAnimationNames().Length == 0;
        if (precisaGerar)
            _sprite.SpriteFrames = MobSpriteFramesBuilder.GetOrBuild(MobType);
    }

    private void DefinirPetPadrao()
    {
        if (PetID > 0)
            return;

        PetID = MobType switch
        {
            "goblin" => 3,
            "lobo" => 2,
            _ => 0,
        };
    }

    private void OnAnimationFinished()
    {
        if (_sprite == null)
            return;

        string currentAnim = _sprite.Animation.ToString();
        if (currentAnim.Contains("attack"))
            TocarAnimacao($"idle_{CardinalDirection(_facingDirection)}", true);
    }

    private void TocarAnimacao(string suffix, bool force = false)
    {
        if (_sprite?.SpriteFrames == null)
            return;

        string animationName = $"{AnimPrefix}{suffix}";
        if (!_sprite.SpriteFrames.HasAnimation(animationName))
            animationName = BuscarFallbackAnimacao(suffix);

        if (string.IsNullOrEmpty(animationName))
            return;

        if (force || _sprite.Animation.ToString() != animationName || !_sprite.IsPlaying())
            _sprite.Play(animationName);

        _sprite.SpeedScale = suffix.StartsWith("walk") ? 1.5f : 1.0f;
    }

    private string BuscarFallbackAnimacao(string suffix)
    {
        if (_sprite?.SpriteFrames == null)
            return string.Empty;

        string[] candidates =
        {
            $"{AnimPrefix}idle_down",
            $"{MobSpriteFramesBuilder.ObterPrefixo(MobType)}idle_down",
            "goblin_idle_down"
        };

        foreach (string candidate in candidates)
        {
            if (_sprite.SpriteFrames.HasAnimation(candidate))
                return candidate;
        }

        var animations = _sprite.SpriteFrames.GetAnimationNames();
        return animations.Length > 0 ? animations[0].ToString() : string.Empty;
    }

    private static string CardinalDirection(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.001f)
            return "down";

        dir = dir.Normalized();
        float angle = Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X));
        if (angle < 0)
            angle += 360f;

        if (angle >= 315f || angle < 45f)
            return "right";
        if (angle >= 45f && angle < 135f)
            return "down";
        if (angle >= 135f && angle < 225f)
            return "left";

        return "up";
    }
}
