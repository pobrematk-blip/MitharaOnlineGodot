using Godot;
using System;
using System.Collections.Generic;
#nullable enable annotations

public partial class Player : CharacterBody2D
{
    private const float NpcInteractionRange = 180f;
    private const float MeleeTargetFallbackRadius = 24f;
    private const float MeleeTargetContactPadding = 10f;
    private const float IdleTransitionDelay = 5f;
    private const float AttackAnimationSpeedScale = 4.0f;
    private const string PlayerAnimationModelScenePath = "res://characters/Player/player.tscn";
    private static SpriteFrames _modeloAnimacaoPlayer;
    [Signal] public delegate void StatusAtualizadoEventHandler();

    [Export] public float MaxSpeed = 185.0f;
    [Export] public float Acceleration = 1200.0f;
    [Export] public float Friction = 1500.0f;
    
    [Export] public int MaxHealth = 100;
    [Export] public int MaxMana = 50;

    // Campo para arrastar a cena do projétil (projetil.tscn) no Inspetor da Godot
    [Export] public PackedScene ProjetilScene;

    protected AnimatedSprite2D AnimatedSprite;
    private AnimatedSprite2D _cabeloOverlay;
    private AnimatedSprite2D _barbaOverlay;
    private AnimatedSprite2D _armaduraOverlay;
    private AnimatedSprite2D _capaceteOverlay;
    private AnimatedSprite2D _luvasOverlay;
    private AnimatedSprite2D _botasOverlay;
    private SpriteFrames _spriteFramesBasePersonagem;
    private string _nomeRacaAtual = "";
    private string _spriteCorpoAtual = "";
    private bool _morteAnimationFinishedConectado;

    private sealed class HumanFullSpriteProfile
    {
        public readonly string Nome;
        public readonly string[] PalavrasChave;
        public readonly string[] SufixosArquivo;
        public readonly int[] MovimentoRows;
        public readonly int MovimentoFrameSize;
        public readonly int[] AtaqueRows;
        public readonly int AtaqueFrameSize;
        public readonly int AtaqueFrameCount;
        public readonly string AnimationProfilePath;

        public HumanFullSpriteProfile(string nome, string[] palavrasChave, string[] sufixosArquivo,
            int[] movimentoRows = null, int movimentoFrameSize = LpcSpriteFramesBuilder.FrameSize,
            int[] ataqueRows = null, int ataqueFrameSize = LpcSpriteFramesBuilder.FrameSize,
            int ataqueFrameCount = 0,
            string animationProfilePath = "")
        {
            Nome = nome;
            PalavrasChave = palavrasChave;
            SufixosArquivo = sufixosArquivo;
            MovimentoRows = movimentoRows;
            MovimentoFrameSize = movimentoFrameSize;
            AtaqueRows = ataqueRows;
            AtaqueFrameSize = ataqueFrameSize;
            AtaqueFrameCount = ataqueFrameCount;
            AnimationProfilePath = animationProfilePath;
        }
    }

    private static readonly HumanFullSpriteProfile HumanUnarmedProfile = new(
        "Desarmado",
        Array.Empty<string>(),
        new[] { "{race} Desarmado", "{raceNoSpace}" });

    private static readonly HumanFullSpriteProfile[] HumanFullSpriteProfiles =
    {
        new("Arco", new[] { "arco" },
            new[] { "{race} com Arco" },
            new[] { 27, 28, 29, 30 }, 128,
            new[] { 16, 17, 18, 19 }, 64,
            13,
            "res://characters/Player/AnimationProfiles/Arco.tres"),

        new("Adaga", new[] { "adaga", "lamina", "lâmina" },
            new[] { "{race} com Adaga" },
            new[] { 8, 9, 10, 11 }, 128,
            new[] { 31, 34, 33, 32 }, 128,
            13,
            "res://characters/Player/AnimationProfiles/Adaga.tres"),

        new("Espada e Escudo", new[] { "espada", "escudo" },
            new[] { "{race} com Espada e Escudo" },
            new[] { 8, 9, 10, 11 }, 128,
            new[] { 27, 28, 29, 30 }, 128,
            6,
            "res://characters/Player/AnimationProfiles/EspadaEEscudo.tres"),

        new("Machado Duas Maos", new[] { "machado de guerra", "machado duas", "machado de duas", "machadao", "machadão" },
            new[] { "{race} com Machado de Guerra" },
            new[] { 8, 9, 10, 11 }, 128,
            new[] { 18, 19, 20, 21 }, 192,
            6,
            "res://characters/Player/AnimationProfiles/MachadoDeGuerra.tres"),

        new("Machado de Coleta", new[] { "machado de coleta", "machado coleta" },
            new[] { "{race} com Machado de Coleta" }),

        new("Maca e Escudo", new[] { "maca", "maça", "mangual", "martelo", "escudo magico", "escudo mágico" },
            new[] { "{race} com Maca e Escudo" },
            new[] { 8, 9, 10, 11 }, 128,
            new[] { 18, 19, 20, 21 }, 192,
            6,
            "res://characters/Player/AnimationProfiles/MacaEEscudo.tres"),

        new("Martelo", new[] { "martelo" },
            new[] { "{race} com Martelo" }),

        new("Cajado", new[] { "cajado", "staff" },
            new[] { "{race} com Cajado" },
            new[] { 8, 9, 10, 11 }, 128,
            new[] { 18, 21, 20, 19 }, 192,
            8,
            "res://characters/Player/AnimationProfiles/Cajado.tres"),

        new("Picareta", new[] { "picareta" },
            new[] { "{race} com Picareta" }),

        new("Regador", new[] { "regador" },
            new[] { "{race} com Regador" }),

        new("Vara de Pesca", new[] { "vara de pesca", "pesca" },
            new[] { "{race} com Vara de Pesca" },
            new[] { 8, 9, 10, 11 }, 128,
            new[] { 12, 13, 14, 15 }, 128),
    };

    public static SpriteFrames CriarSpriteFramesParaRacaClasse(string nomeRaca, string nomeClasse, out string spritesheetPath, out string perfilVisual)
    {
        spritesheetPath = "";
        perfilVisual = "";

        var profile = EncontrarPerfilSpritePorClasse(nomeClasse) ?? HumanUnarmedProfile;
        string prefixoAtaque = ClasseRegistry.ObterPrefixoAtaqueRecomendado(nomeClasse);
        SpriteFrames baseFrames = CriarSpriteFramesBaseParaRaca(nomeRaca, prefixoAtaque);
        Texture2D sheet = CarregarTexturaPrimeiroExistente(ResolverCaminhosSpriteRaca(profile, nomeRaca));
        if (sheet == null && profile != HumanUnarmedProfile)
        {
            profile = HumanUnarmedProfile;
            sheet = CarregarTexturaPrimeiroExistente(ResolverCaminhosSpriteRaca(profile, nomeRaca));
        }

        if (sheet == null)
            return null;

        SpriteFrames frames = profile == HumanUnarmedProfile
            ? LpcSpriteFramesBuilder.Construir(sheet, prefixoAtaque)
            : CriarSpriteFramesEquipamento(sheet, prefixoAtaque, profile);

        if (frames == null || frames.GetAnimationNames().Length == 0)
            return null;

        if (profile != HumanUnarmedProfile && baseFrames != null)
            CopiarAnimacoesBaseParaSpriteArmado(frames, baseFrames);

        AplicarModeloAnimacaoEditavel(frames, profile, sheet, sheet, prefixoAtaque);

        spritesheetPath = sheet.ResourcePath;
        perfilVisual = profile.Nome;
        return frames;
    }

    private static SpriteFrames CriarSpriteFramesEquipamento(Texture2D sheet, string prefixoAtaque, HumanFullSpriteProfile profile)
    {
        PlayerSpriteAnimationProfile animationProfile = CarregarPerfilAnimacao(profile);
        if (animationProfile != null)
            return LpcSpriteFramesBuilder.ConstruirEquipamento(sheet, sheet, prefixoAtaque, animationProfile);

        return LpcSpriteFramesBuilder.ConstruirEquipamento(
            sheet,
            sheet,
            prefixoAtaque,
            profile.MovimentoRows,
            profile.MovimentoFrameSize,
            profile.AtaqueRows,
            profile.AtaqueFrameSize,
            profile.AtaqueFrameCount);
    }

    private static PlayerSpriteAnimationProfile CarregarPerfilAnimacao(HumanFullSpriteProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.AnimationProfilePath))
            return null;

        return ResourceLoader.Exists(profile.AnimationProfilePath)
            ? ResourceLoader.Load<PlayerSpriteAnimationProfile>(profile.AnimationProfilePath)
            : null;
    }

    private static SpriteFrames CriarSpriteFramesBaseParaRaca(string nomeRaca, string prefixoAtaque)
    {
        Texture2D sheetBase = CarregarTexturaPrimeiroExistente(ResolverCaminhosSpriteRaca(HumanUnarmedProfile, nomeRaca));
        if (sheetBase == null)
            return null;

        var frames = LpcSpriteFramesBuilder.Construir(sheetBase, prefixoAtaque);
        if (frames != null && frames.GetAnimationNames().Length > 0)
            AplicarModeloAnimacaoEditavel(frames, HumanUnarmedProfile, sheetBase, null, prefixoAtaque);

        return frames != null && frames.GetAnimationNames().Length > 0 ? frames : null;
    }
    public string CurrentDirection { get; protected set; } = "down";
    protected bool IsAttacking = false;
    private float _attackTimeoutCounter = 0f;
    private float _maxAttackDuration = 0.8f;
    private float _attackCooldownRemaining;
    private bool _wasFPressed = false;
    private bool _projetilDisparado = false;
    private float _promptUpdateTimer;
    private Sprite2D _shadowSprite;
    private ulong? _selectedTargetId;
    private Node2D _targetMarker;

    private GameNetwork _network;
    private LevelProgressionComponent _levelProgression;
    private float _moveSendTimer;
    private Vector2 _lastSentPosition;
    private bool _wasMoving;
    private bool _isLyingDown;
    private float _idleTransitionTimer;
    private bool _holdingBeforeIdle;

    [Export] public int MaxStamina = 100;
    [Export] public float BasicAttackCooldown = 1.2f;
    public int CurrentStamina { get; private set; }
    public bool IsSprinting { get; private set; }
    private float _staminaRegenCooldown = 0f;
    private float _staminaAccumulator = 0f;
    private bool _staminaExhausted;

    public int CurrentHealth { get; private set; }
    public int CurrentMana { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public bool TryConsumeMana(int amount)
    {
        if (amount <= 0) return true;
        if (CurrentMana >= amount)
        {
            CurrentMana -= amount;
            EmitSignal(SignalName.StatusAtualizado);
            return true;
        }
        return false;
    }

    public void SetHealthFromServer(int health, int maxHealth)
    {
        bool wasAlive = !IsDead;
        MaxHealth = maxHealth;
        CurrentHealth = Mathf.Clamp(health, 0, maxHealth);
        if (CurrentHealth <= 0 && wasAlive)
            Morrer();
        EmitSignal(SignalName.StatusAtualizado);
    }

    public void SetManaFromServer(int mana, int maxMana)
    {
        MaxMana = maxMana;
        CurrentMana = Mathf.Clamp(mana, 0, maxMana);
        EmitSignal(SignalName.StatusAtualizado);
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        EmitSignal(SignalName.StatusAtualizado);
        GD.Print($"[PLAYER] Curado em {amount}. Vida atual: {CurrentHealth}/{MaxHealth}");
    }

    public void PerformSkillDamage(int amount)
    {
        LevarDano(amount);
    }

    public void DoDash(int power)
    {
        Vector2 impulse = DirectionUtil.DirectionToVector(CurrentDirection);
        Velocity += impulse * power;
        GD.Print($"[PLAYER] Dash aplicado: power={power}");
    }

    public void BecomeInvisible(float duration)
    {
        Visible = false;
        GD.Print($"[PLAYER] Invisivel por {duration}s");
        var t = new Timer();
        t.OneShot = true;
        t.WaitTime = duration;
        AddChild(t);
        t.Timeout += () => { if (IsInstanceValid(this)) { Visible = true; } t.QueueFree(); };
        t.Start();
    }

    public void SummonPet(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return;
        var res = GD.Load<PackedScene>(scenePath);
        if (res == null) return;
        var inst = res.Instantiate<Node2D>();
        if (inst == null) return;
        inst.GlobalPosition = GlobalPosition;
        GetParent().AddChild(inst);
        GD.Print($"[PLAYER] Summoned pet from {scenePath}");
    }

    public void AttemptRevive(Node target)
    {
        if (target is Player p && p.IsDead)
        {
            p.CurrentHealth = p.MaxHealth;
            p.CurrentMana = p.MaxMana;
            p.EmitSignal(SignalName.StatusAtualizado);
            p.SetPhysicsProcess(true);
            GD.Print("[PLAYER] Revive aplicado em alvo.");
        }
    }

    // (full implementations later in file)
    [Export] public float MeleeAttackRange = 96.0f;
    [Export] public float AttackDotThreshold = 0.5f;
    
    // Define qual classe esse script está controlando no momento
    public string NomeDaClasse = "mago"; 
    [Export] public FaccaoResource FaccaoAtiva { get; set; }
    private int _nivel = 1;

    public int Nivel => _nivel;

    public override void _Ready()
    {
        // Verifica se o nó AnimatedSprite realmente existe antes de usá-lo
        if (HasNode("AnimatedSprite"))
        {
            AnimatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite");
            AnimatedSprite.AnimationFinished += OnAnimationFinished;
            _cabeloOverlay = GetNodeOrNull<AnimatedSprite2D>("CabeloOverlay");
            _barbaOverlay = GetNodeOrNull<AnimatedSprite2D>("BarbaOverlay");
            _armaduraOverlay = GetNodeOrNull<AnimatedSprite2D>("ArmaduraOverlay");
            _capaceteOverlay = GetNodeOrNull<AnimatedSprite2D>("CapaceteOverlay");
            _luvasOverlay = GetNodeOrNull<AnimatedSprite2D>("LuvasOverlay");
            _botasOverlay = GetNodeOrNull<AnimatedSprite2D>("BotasOverlay");
        }
        else
        {
            GD.PrintErr("[PLAYER] Erro: O nó filho 'AnimatedSprite' não foi encontrado na cena do Player!");
        }

        CurrentHealth = MaxHealth;
        CurrentMana = MaxMana;
        CurrentStamina = MaxStamina;
        AddToGroup("player");
        InitClass();

        CriarSombra();

        AplicarPersonagemEscolhido();

        var equipamento = FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equipamento != null)
        {
            equipamento.EquipamentoAtualizado += AtualizarOverlaysEquipamento;
            AtualizarOverlaysEquipamento();
        }

        if (FindChild("PlayerSkillComponent", true, false) == null)
        {
            var skillComp = new PlayerSkillComponent();
            skillComp.Name = "PlayerSkillComponent";
            AddChild(skillComp);
            skillComp.Owner = this;
        }

        _levelProgression = FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;

        _network = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_network != null)
        {
            if (_network.PendingPlayerSpawn != Vector2.Zero)
                ApplyServerPosition(_network.PendingPlayerSpawn.X, _network.PendingPlayerSpawn.Y);

            _network.OnRespawn += OnRespawnReceived;
            _network.OnTeleport += OnTeleportReceived;
            _network.OnEnterWorld += AplicarProgressaoServidorPendente;
            _network.OnGainExp += OnGainExpReceived;
            _network.OnLevelUp += OnLevelUpReceived;
            _network.OnSceneChange += OnSceneChangeReceived;
            CallDeferred(nameof(AplicarProgressaoServidorPendente));
        }
        _lastSentPosition = GlobalPosition;
    }

    private void AplicarProgressaoServidorPendente()
    {
        if (_network == null)
            return;

        AplicarProgressaoServidor(_network._pendingLevel, _network._pendingXp);
    }

    private void OnGainExpReceived(ulong entityId, int amount, long totalExp)
    {
        if (_network == null || entityId != _network.LocalPlayerId)
            return;

        int nivelAtual = _levelProgression?.Nivel ?? _nivel;
        AplicarProgressaoServidor(nivelAtual, totalExp);
    }

    private void OnLevelUpReceived(ulong entityId, int newLevel, int remainingXp)
    {
        if (_network == null || entityId != _network.LocalPlayerId)
            return;

        AplicarProgressaoServidor(newLevel, remainingXp);
    }

    private void AplicarProgressaoServidor(int nivel, long experiencia)
    {
        if (nivel <= 0)
            return;

        _nivel = Mathf.Max(nivel, LevelProgressionUtil.NivelInicial);
        _levelProgression ??= FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
        int xpAtual = (int)Math.Min(Math.Max(experiencia, 0), int.MaxValue);
        _levelProgression?.DefinirProgresso(_nivel, xpAtual);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Tab)
        {
            SelecionarProximoTarget(key.ShiftPressed ? -1 : 1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            SelecionarAlvoEm(GetGlobalMousePosition());
            if (!IsAttacking && _attackCooldownRemaining <= 0f)
                Atacar();
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void SelecionarAlvoEm(Vector2 worldPosition)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null || !net.IsConnected) return;

        if (!TryEncontrarTargetEm(net, worldPosition, out ulong escolhido, out Node2D nodeEscolhido))
        {
            LimparTarget();
            return;
        }

        AplicarTarget(escolhido, nodeEscolhido);
    }

    private void SelecionarProximoTarget(int direcao)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null || !net.IsConnected) return;

        var targets = new List<(ulong Id, Node2D Node, float Distancia)>();
        foreach (var entry in net.GetAllEntities())
        {
            if (entry.Key == net.LocalPlayerId || !IsInstanceValid(entry.Value))
                continue;
            if (entry.Value is not Inimigo && !entry.Value.HasMeta("player_name"))
                continue;
            targets.Add((entry.Key, entry.Value, GlobalPosition.DistanceSquaredTo(entry.Value.GlobalPosition)));
        }

        if (targets.Count == 0)
        {
            LimparTarget();
            return;
        }

        targets.Sort((a, b) => a.Distancia.CompareTo(b.Distancia));
        int indiceAtual = _selectedTargetId.HasValue
            ? targets.FindIndex(target => target.Id == _selectedTargetId.Value)
            : -1;
        int proximoIndice = indiceAtual < 0
            ? (direcao >= 0 ? 0 : targets.Count - 1)
            : (indiceAtual + direcao + targets.Count) % targets.Count;

        var proximo = targets[proximoIndice];
        AplicarTarget(proximo.Id, proximo.Node);
    }

    private void AplicarTarget(ulong entityId, Node2D targetNode)
    {
        if (_selectedTargetId == entityId)
            return;

        LimparTarget();
        _selectedTargetId = entityId;
        _targetMarker = new Node2D
        {
            Name = "TargetMarker",
            ZIndex = 20,
        };
        var leftArrow = new Polygon2D
        {
            Polygon = new Vector2[]
            {
                new Vector2(16f, 0f),
                new Vector2(-8f, -12f),
                new Vector2(-8f, 12f),
            },
            Color = new Color(1f, 0.18f, 0.12f, 0.95f),
            Position = new Vector2(-46f, -18f),
        };
        var rightArrow = new Polygon2D
        {
            Polygon = new Vector2[]
            {
                new Vector2(-16f, 0f),
                new Vector2(8f, -12f),
                new Vector2(8f, 12f),
            },
            Color = new Color(1f, 0.18f, 0.12f, 0.95f),
            Position = new Vector2(46f, -18f),
        };
        _targetMarker.AddChild(leftArrow);
        _targetMarker.AddChild(rightArrow);
        targetNode.AddChild(_targetMarker);

        Vector2 dirToTarget = (targetNode.GlobalPosition - GlobalPosition).Normalized();
        if (dirToTarget.LengthSquared() > 0.001f)
        {
            CurrentDirection = DirectionUtil.VectorToDirectionString(dirToTarget);
            UpdateAnimation(Vector2.Zero);
        }

        GD.Print($"[TARGET] Alvo selecionado: {_selectedTargetId.Value}");
    }

    private static bool TryEncontrarTargetEm(GameNetwork net, Vector2 worldPosition, out ulong entityId, out Node2D targetNode)
    {
        entityId = 0;
        targetNode = null;
        float menorDistancia = 80f;
        foreach (var entry in net.GetAllEntities())
        {
            if (entry.Key == net.LocalPlayerId || !IsInstanceValid(entry.Value))
                continue;
            if (entry.Value is not Inimigo && !entry.Value.HasMeta("player_name"))
                continue;

            float distancia = worldPosition.DistanceTo(entry.Value.GlobalPosition);
            if (distancia >= menorDistancia) continue;
            menorDistancia = distancia;
            entityId = entry.Key;
            targetNode = entry.Value;
        }
        return targetNode != null;
    }

    private void LimparTarget()
    {
        if (_targetMarker != null && IsInstanceValid(_targetMarker))
            _targetMarker.QueueFree();
        _targetMarker = null;
        _selectedTargetId = null;
    }

    public bool TryGetSelectedTargetPosition(out Vector2 targetPosition)
    {
        targetPosition = Vector2.Zero;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (!TryGetSelectedTarget(net, out var targetNode, out _))
            return false;

        targetPosition = targetNode.GlobalPosition;
        return true;
    }

    public bool TryGetSelectedCombatTarget(out Node2D targetNode)
    {
        targetNode = null;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        return TryGetSelectedTarget(net, out targetNode, out _);
    }

    public void ApplyServerPosition(float x, float y, bool animated = false)
    {
        var target = new Vector2(x, y);
        if (animated && IsInsideTree())
        {
            var tween = CreateTween();
            tween.TweenProperty(this, "global_position", target, 0.18f)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }
        else
        {
            GlobalPosition = target;
        }

        _lastSentPosition = target;
    }

    private void OnRespawnReceived(ulong entityId, float x, float y, int health, int maxHealth, int mana, int maxMana)
    {
        if (entityId == _network?.LocalPlayerId)
            Reviver(x, y, health, maxHealth, mana, maxMana);
    }

    private void OnTeleportReceived(ulong entityId, float x, float y)
    {
        if (entityId == _network?.LocalPlayerId)
        {
            GlobalPosition = new Vector2(x, y);
            _lastSentPosition = GlobalPosition;
        }
    }

    private void OnSceneChangeReceived(string sceneName, float x, float y)
    {
        if (sceneName == SceneConstants.SCENE_MAIN)
            CarregarCenaPrincipal(x, y);
        else
            CarregarInterior(sceneName, x, y);
    }

    private void CarregarCenaPrincipal(float x, float y)
    {
        var world = GetParent();
        var interioresNode = world?.GetNodeOrNull("Interiores");
        if (interioresNode != null)
        {
            foreach (var child in interioresNode.GetChildren())
                child.QueueFree();
        }

        GlobalPosition = new Vector2(x, y);
        _lastSentPosition = GlobalPosition;
    }

    private void CarregarInterior(string sceneName, float x, float y)
    {
        string scenePath = sceneName switch
        {
            SceneConstants.SCENE_ALFAIATARIA => SceneConstants.ALFAIATARIA,
            _ => "",
        };

        if (string.IsNullOrEmpty(scenePath))
        {
            GD.PrintErr($"[SCENE] Cena interior desconhecida: {sceneName}");
            return;
        }

        var world = GetParent();
        if (world == null) return;

        Node2D interiores = world.GetNodeOrNull<Node2D>("Interiores");
        if (interiores == null)
        {
            interiores = new Node2D { Name = "Interiores" };
            world.AddChild(interiores);
        }
        else
        {
            foreach (var child in interiores.GetChildren())
                child.QueueFree();
        }

        var interiorScene = ResourceLoader.Load<PackedScene>(scenePath);
        if (interiorScene == null)
        {
            GD.PrintErr($"[SCENE] Falha ao carregar: {scenePath}");
            return;
        }

        var interior = interiorScene.Instantiate<Node2D>();
        interiores.AddChild(interior);

        float interiorX = x + SceneConstants.INTERIOR_OFFSET_X;
        float interiorY = y + SceneConstants.INTERIOR_OFFSET_Y;

        var spawnPoint = interior.GetNodeOrNull<Marker2D>("SpawnPoint");
        if (spawnPoint != null)
            interiores.Position = new Vector2(interiorX - spawnPoint.Position.X, interiorY - spawnPoint.Position.Y);
        else
            interiores.Position = new Vector2(interiorX, interiorY);

        GlobalPosition = new Vector2(interiorX, interiorY);
        _lastSentPosition = GlobalPosition;
    }

    public virtual void InitClass() { }

    private void AplicarPersonagemEscolhido()
    {
        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        if (escolhido == null || !escolhido.TemPersonagem)
        {
            GD.Print("[PLAYER] Nenhum personagem escolhido encontrado, usando valores padrao.");
            return;
        }

        var classe = escolhido.MontarClasseParaPlayer();
        if (classe == null) return;

        AplicarClasse(classe);
        _nomeRacaAtual = escolhido.Raca?.NomeRaca ?? classe.Raca?.NomeRaca ?? "";

        if (AnimatedSprite != null)
        {
            var frames = CriarSpriteFramesParaRacaClasse(_nomeRacaAtual, classe.NomeClasse, out string sheetPath, out string perfilVisual)
                ?? classe.ObterSpriteFramesCompletos();
            if (frames != null && frames.GetAnimationNames().Length > 0)
            {
                AnimatedSprite.SpriteFrames = frames;
                _spriteFramesBasePersonagem = CriarSpriteFramesBaseParaRaca(_nomeRacaAtual, ObterPrefixoAtaqueAtual()) ?? frames;
                _spriteCorpoAtual = string.IsNullOrWhiteSpace(perfilVisual) ? "base" : perfilVisual;
                GD.Print($"[PLAYER] Sprites aplicados: {escolhido.Raca?.NomeRaca} / {classe.NomeClasse} / {perfilVisual} ({sheetPath})");
            }
            else
            {
                GD.PrintErr("[PLAYER] Nenhum sprite encontrado para a raca/classe selecionada.");
            }
        }

        if (classe.UsaProjetil && ClassePodeUsarProjetilBasico(classe.NomeClasse))
        {
            ProjetilScene = classe.CenaDoProjetil;
        }
        else
        {
            ProjetilScene = null;
        }

        MaxSpeed = classe.VelocidadeMovimento > 0 ? classe.VelocidadeMovimento : MaxSpeed;
        MeleeAttackRange = classe.AlcanceAtaqueMelee > 0
            ? Mathf.Max(88f, classe.AlcanceAtaqueMelee)
            : MeleeAttackRange;

        ConfigurarArvoreTalentos(classe);

        AplicarOverlayCabelo(escolhido.CabeloPath, escolhido.CabeloCor);
        AplicarOverlayBarba(escolhido.BarbaPath, escolhido.BarbaCor);
    }

    private void ConfigurarArvoreTalentos(ClasseCustomResource classe)
    {
        if (classe.ArvoreTalentos == null)
        {
            GD.Print("[PLAYER] Classe n?o possui ?rvore de talentos.");
            return;
        }

        var talentComp = GetNodeOrNull<TalentTreeComponent>("TalentTreeComponent");
        if (talentComp == null)
        {
            talentComp = new TalentTreeComponent();
            talentComp.Name = "TalentTreeComponent";
            AddChild(talentComp);
            talentComp.Owner = this;
        }

        talentComp.TalentTree = classe.ArvoreTalentos;

        GD.Print($"[PLAYER] Arvore de talentos configurada: {classe.ArvoreTalentos.ResourceName}");
    }

    public void AplicarClasse(ClasseCustomResource classe)
    {
        if (classe == null) return;
        NomeDaClasse = classe.NomeClasse.ToLower();
        FaccaoAtiva = classe.Raca?.Faccao;
        MaxHealth = classe.VidaMaxima + (classe.Raca?.BonusVidaMaxima ?? 0);
        MaxMana = classe.ManaMaxima + (classe.Raca?.BonusManaMaxima ?? 0);
        CurrentHealth = MaxHealth;
        CurrentMana = MaxMana;
        _nivel = LevelProgressionUtil.NivelInicial;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isStunned) return;
        if (_isLyingDown)
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }
        if (_isSleeping || _isPrisoned || _isFrozen)
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }        if (_isRooted)
        {
            Velocity = Vector2.Zero;
            MoveAndSlide();
            return;
        }
        // Se o sprite falhar ao carregar, não roda a física para evitar crashes
        if (AnimatedSprite == null) return;

        // Gerencia timeout de ataque para evitar travamento
        if (_attackCooldownRemaining > 0f)
            _attackCooldownRemaining = Mathf.Max(0f, _attackCooldownRemaining - (float)delta);

        if (IsAttacking)
        {
            _attackTimeoutCounter += (float)delta;
            if (_attackTimeoutCounter >= _maxAttackDuration)
            {
                TentarDispararProjetilNoFinalDaAnimacao();
                FinalizarAtaqueAtual("[PLAYER] Ataque finalizado por seguranca apos exceder o tempo maximo.");
            }
            else if (DeveEncerrarAtaqueNoUltimoFrame())
            {
                TentarDispararProjetilNoFinalDaAnimacao();
                FinalizarAtaqueAtual();
            }
        }

        // Permite movimento durante o ataque (removido o if (IsAttacking) return;)
        if (Input.IsActionJustPressed("atacar"))
        {
            if (_isSleeping || _isPrisoned || _isFrozen || _isSilenced || _isBlinded)
            {
                GD.Print("[PLAYER] Não pode atacar no estado atual.");
            }
            else if (!IsAttacking)
            {
                if (_attackCooldownRemaining > 0f)
                    return;

                // Verifica se inventário ou tela de personagem estão abertos
                var inventarioUI = GetTree().CurrentScene.FindChild("InventarioUi", true, false) as InventarioUI;
                var characterUI = GetTree().CurrentScene.FindChild("CharacterUI", true, false) as CharacterUI;
                var bancoUI = GetTree().CurrentScene.FindChild("BancoUi", true, false) as BancoUI;
                bool inventarioAberto = inventarioUI != null && inventarioUI.PainelVisivel;
                bool characterAberto = characterUI != null && characterUI.PainelVisivel;
                bool bancoAberto = bancoUI != null && bancoUI.PainelVisivel;
                
                if (!inventarioAberto && !characterAberto && !bancoAberto)
                {
                    Atacar();
                    return;
                }
            }
        }

        _promptUpdateTimer -= (float)delta;
        if (_promptUpdateTimer <= 0f)
        {
            _promptUpdateTimer = 0.18f;
            UpdateNpcPrompts();
        }

        if (Input.IsKeyPressed(Key.F) && !_wasFPressed)
        {
            _wasFPressed = true;
            GD.Print($"[PLAYER] F pressionado, lyingDown={_isLyingDown}");
            if (_isLyingDown) return;
            if (!TryInteractNpc())
                TryPickupLoot();
        }
        if (!Input.IsKeyPressed(Key.F))
            _wasFPressed = false;

        if (Input.IsKeyPressed(Key.Shift) && CurrentStamina > 0 && !_isSleeping && !_isPrisoned && !_isFrozen && !_isRooted && !_isFeared)
            IsSprinting = true;
        else
            IsSprinting = false;

        Vector2 inputDirection = Vector2.Zero;
        if (Input.IsActionPressed("mover_direita"))   inputDirection.X += 1;
        if (Input.IsActionPressed("mover_esquerda"))  inputDirection.X -= 1;
        if (Input.IsActionPressed("mover_baixo"))     inputDirection.Y += 1;
        if (Input.IsActionPressed("mover_cima"))      inputDirection.Y -= 1;

        bool hasMoveInput = inputDirection != Vector2.Zero;
        inputDirection = inputDirection.Normalized();
        Vector2 velocity = Velocity;

        float effectiveMax = MaxSpeed * _speedMultiplier;
        if (IsAttacking)
            effectiveMax *= 0.15f;
        if (IsSprinting)
            effectiveMax *= 1.8f;
        if (_isFeared)
        {
            Vector2 fleeDirection = Vector2.Zero;
            var enemies = GetTree()?.GetNodesInGroup("Inimigos");
            if (enemies != null)
            {
                foreach (Node item in enemies)
                {
                    if (item is Node2D enemy)
                    {
                        fleeDirection += (GlobalPosition - enemy.GlobalPosition).Normalized();
                    }
                }
            }
            fleeDirection = fleeDirection.Normalized();
            if (fleeDirection != Vector2.Zero)
                velocity = velocity.MoveToward(fleeDirection * effectiveMax * 0.8f, Acceleration * (float)delta);
            else
                velocity = velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
        }
        else
        {
            if (_isConfused)
                inputDirection = -inputDirection;

            if (inputDirection != Vector2.Zero)
                velocity = velocity.MoveToward(inputDirection * effectiveMax, Acceleration * (float)delta);
            else
                velocity = velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
        }

        Velocity = velocity;
        MoveAndSlide();

        int prevStamina = CurrentStamina;
        if (IsSprinting)
        {
            _staminaAccumulator += 30f * (float)delta;
            if (_staminaAccumulator >= 1f)
            {
                int drain = (int)_staminaAccumulator;
                CurrentStamina = Mathf.Max(0, CurrentStamina - drain);
                _staminaAccumulator -= drain;
            }
            _staminaRegenCooldown = 0.15f;
        }
        else
        {
            if (_staminaExhausted && hasMoveInput)
            {
                _staminaAccumulator = 0f;
                _staminaRegenCooldown = 0.15f;
            }
            else if (_staminaRegenCooldown > 0f)
                _staminaRegenCooldown -= (float)delta;
            else
            {
                if (!hasMoveInput)
                    _staminaExhausted = false;
                _staminaAccumulator += 20f * (float)delta;
                if (_staminaAccumulator >= 1f)
                {
                    int regen = (int)_staminaAccumulator;
                    CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + regen);
                    _staminaAccumulator -= regen;
                }
            }
        }
        if (CurrentStamina <= 0 && IsSprinting)
        {
            IsSprinting = false;
            _staminaExhausted = true;
            _staminaAccumulator = 0f;
        }
        if (CurrentStamina != prevStamina)
            EmitSignal(SignalName.StatusAtualizado);

        UpdateAnimation(velocity);
        SincronizarOverlays();

        // Enviar movimento para o servidor
        if (_network != null && _network.IsConnected)
        {
            bool isMoving = velocity.LengthSquared() > 0.01f;
            Vector2 pos = GlobalPosition;
            Vector2 dir = velocity.Normalized();
            Vector2 netPos = ToNetworkPos(pos);

            _moveSendTimer += (float)delta;

            if (isMoving)
            {
                if (_moveSendTimer >= 0.1f || pos.DistanceSquaredTo(_lastSentPosition) > 400f)
                {
                    _network.SendPlayerMove(netPos, dir, true, IsSprinting);
                    _lastSentPosition = pos;
                    _moveSendTimer = 0f;
                }
                _wasMoving = true;
            }
            else if (_wasMoving)
            {
                _network.SendPlayerStop(netPos);
                _wasMoving = false;
                _moveSendTimer = 0f;
            }
        }
    }

    private Vector2 ToNetworkPos(Vector2 globalPos)
    {
        if (IsInInterior())
            return globalPos - new Vector2(SceneConstants.INTERIOR_OFFSET_X, SceneConstants.INTERIOR_OFFSET_Y);
        return globalPos;
    }

    private bool IsInInterior()
    {
        var world = GetParent();
        var interiores = world?.GetNodeOrNull<Node2D>("Interiores");
        return interiores != null && interiores.GetChildCount() > 0;
    }

    private void SincronizarOverlays()
    {
        if (AnimatedSprite == null) return;
        string anim = AnimatedSprite.Animation.ToString();
        int frame = AnimatedSprite.Frame;
        float speed = AnimatedSprite.SpeedScale;

        SincronizarOverlay(_cabeloOverlay, anim, frame, speed);
        SincronizarOverlay(_barbaOverlay, anim, frame, speed);
        SincronizarOverlay(_armaduraOverlay, anim, frame, speed);
        SincronizarOverlay(_capaceteOverlay, anim, frame, speed);
        SincronizarOverlay(_luvasOverlay, anim, frame, speed);
        SincronizarOverlay(_botasOverlay, anim, frame, speed);
    }

    private static void SincronizarOverlay(AnimatedSprite2D overlay, string anim, int frame, float speed)
    {
        if (overlay == null || !overlay.Visible) return;
        if (overlay.SpriteFrames == null) return;
        if (!overlay.SpriteFrames.HasAnimation(anim)) return;
        if (overlay.Animation.ToString() != anim)
            overlay.Play(anim);
        overlay.Frame = frame;
        overlay.SpeedScale = speed;
    }

    public void AplicarOverlayCabelo(string spritesheetPath, Color cor)
    {
        if (_cabeloOverlay == null) return;
        if (!string.IsNullOrEmpty(spritesheetPath) && ResourceLoader.Exists(spritesheetPath))
        {
            var tex = GD.Load<Texture2D>(spritesheetPath);
            if (tex != null)
            {
                var frames = LpcSpriteFramesBuilder.Construir(tex, "");
                _cabeloOverlay.SpriteFrames = frames;
                _cabeloOverlay.Modulate = cor;
                _cabeloOverlay.Visible = true;
                SincronizarOverlays();
                return;
            }
        }
        _cabeloOverlay.Visible = false;
    }

    public void AplicarOverlayBarba(string spritesheetPath, Color cor)
    {
        if (_barbaOverlay == null) return;
        if (!string.IsNullOrEmpty(spritesheetPath) && ResourceLoader.Exists(spritesheetPath))
        {
            var tex = GD.Load<Texture2D>(spritesheetPath);
            if (tex != null)
            {
                var frames = LpcSpriteFramesBuilder.Construir(tex, "");
                _barbaOverlay.SpriteFrames = frames;
                _barbaOverlay.Modulate = cor;
                _barbaOverlay.Visible = true;
                SincronizarOverlays();
                return;
            }
        }
        _barbaOverlay.Visible = false;
    }

    private void AplicarOverlayEquipamento(AnimatedSprite2D overlay, ItemResource item)
    {
        if (overlay == null) return;

        // Prioridade: SpriteFrames direto > construção automática da spritesheet
        if (item.SpriteFramesEquipamento != null)
        {
            overlay.SpriteFrames = item.SpriteFramesEquipamento;
            overlay.Visible = true;
            SincronizarOverlays();
            return;
        }

        if (item.SpritesheetEquipamento != null)
        {
            var frames = LpcSpriteFramesBuilder.Construir(item.SpritesheetEquipamento, ObterPrefixoAtaqueAtual());
            if (frames != null && frames.GetAnimationNames().Length > 0)
            {
                overlay.SpriteFrames = frames;
                overlay.Visible = true;
                SincronizarOverlays();
                return;
            }
        }

        overlay.Visible = false;
        overlay.SpriteFrames = null;
    }

    private static Texture2D CarregarTexturaPrimeiroExistente(params string[] paths)
    {
        foreach (string path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && ResourceLoader.Exists(path))
            {
                var tex = GD.Load<Texture2D>(path);
                if (tex != null)
                    return tex;
            }
        }
        return null;
    }

    private void LimparOverlayEquipamento(AnimatedSprite2D overlay)
    {
        if (overlay == null) return;
        overlay.Visible = false;
        overlay.SpriteFrames = null;
    }

    private void AtualizarOverlaysEquipamento()
    {
        var equipamento = FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equipamento == null) return;

        AtualizarSpriteCorpoPorEquipamento(equipamento);

        AtualizarOverlaySlot(equipamento, TipoEquipamento.Peitoral, _armaduraOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Capacete, _capaceteOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Luvas, _luvasOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Botas, _botasOverlay);
    }

    private bool AtualizarSpriteCorpoPorEquipamento(EquipamentoComponent equipamento)
    {
        if (AnimatedSprite == null)
            return false;

        var arma = equipamento.ObterSlot(TipoEquipamento.Arma)?.Item;
        var escudo = equipamento.ObterSlot(TipoEquipamento.Escudo)?.Item;
        var profile = EncontrarPerfilSpriteHumano(arma, escudo)
            ?? EncontrarPerfilSpritePorClasse(NomeDaClasse)
            ?? HumanUnarmedProfile;

        if (AplicarSpriteCorpoHumano(profile))
            return true;

        RestaurarSpriteBasePersonagem();
        return false;
    }

    private bool AplicarSpriteCorpoHumano(HumanFullSpriteProfile profile)
    {
        if (profile == null || AnimatedSprite == null)
            return false;

        if (_spriteCorpoAtual == profile.Nome)
            return true;

        Texture2D sheet = CarregarTexturaPrimeiroExistente(ResolverCaminhosSpriteRaca(profile));
        if (sheet == null)
        {
            GD.PrintErr($"[APARENCIA] Spritesheet nao encontrado para raca '{_nomeRacaAtual}' perfil '{profile.Nome}'.");
            return false;
        }

        string prefixoAtaque = ObterPrefixoAtaqueAtual();
        SpriteFrames baseFrames = CriarSpriteFramesBaseParaRaca(_nomeRacaAtual, prefixoAtaque) ?? _spriteFramesBasePersonagem;
        SpriteFrames frames = profile == HumanUnarmedProfile
            ? LpcSpriteFramesBuilder.Construir(sheet, prefixoAtaque)
            : CriarSpriteFramesEquipamento(sheet, prefixoAtaque, profile);

        if (frames == null || frames.GetAnimationNames().Length == 0)
            return false;

        if (profile != HumanUnarmedProfile && baseFrames != null)
            CopiarAnimacoesBaseParaSpriteArmado(frames, baseFrames);

        AplicarModeloAnimacaoEditavel(frames, profile, sheet, sheet, prefixoAtaque);

        string animAtual = AnimatedSprite.Animation.ToString();
        int frameAtual = AnimatedSprite.Frame;
        float speedAtual = AnimatedSprite.SpeedScale;

        AnimatedSprite.SpriteFrames = frames;
        _spriteCorpoAtual = profile.Nome;

        if (!string.IsNullOrWhiteSpace(animAtual) && frames.HasAnimation(animAtual))
        {
            AnimatedSprite.Play(animAtual);
            int frameCount = frames.GetFrameCount(animAtual);
            if (frameCount > 0)
                AnimatedSprite.Frame = Mathf.Clamp(frameAtual, 0, frameCount - 1);
            AnimatedSprite.SpeedScale = speedAtual;
        }
        else
        {
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);
            string idle = $"idle_{cardinal}";
            if (frames.HasAnimation(idle))
                AnimatedSprite.Play(idle);
        }

        SincronizarOverlays();
        GD.Print($"[APARENCIA] Corpo humano aplicado: {profile.Nome} ({sheet.ResourcePath})");
        return true;
    }

    private string[] ResolverCaminhosSpriteRaca(HumanFullSpriteProfile profile)
    {
        return ResolverCaminhosSpriteRaca(profile, _nomeRacaAtual);
    }

    private static string[] ResolverCaminhosSpriteRaca(HumanFullSpriteProfile profile, string nomeRaca)
    {
        string race = string.IsNullOrWhiteSpace(nomeRaca) ? "Humano" : nomeRaca.Trim();
        string raceNoSpace = race.Replace(" ", "");
        var paths = new List<string>();

        foreach (string suffix in profile.SufixosArquivo)
        {
            string file = suffix
                .Replace("{race}", race)
                .Replace("{raceNoSpace}", raceNoSpace);

            if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                file += ".png";

            paths.Add($"{LpcSpriteFramesBuilder.PastaSpritesRaca}{file}");
        }

        return paths.ToArray();
    }

    private static void AplicarModeloAnimacaoEditavel(SpriteFrames destino, HumanFullSpriteProfile profile, Texture2D sheetBase, Texture2D sheetAcao, string prefixoAtaque)
    {
        SpriteFrames modelo = CarregarModeloAnimacaoPlayer();
        if (destino == null || modelo == null)
            return;

        string[] direcoes = { "up", "left", "down", "right" };
        foreach (string dir in direcoes)
        {
            CopiarAnimacaoModeloComAtlas(destino, modelo, $"idle_{dir}", sheetBase, EhAtlasDesarmado);
            CopiarAnimacaoModeloComAtlas(destino, modelo, $"walk_{dir}", sheetBase, EhAtlasDesarmado);
            string runOrigem = dir switch
            {
                "left" => "right",
                "right" => "left",
                _ => dir
            };
            CopiarAnimacaoModeloComAtlas(destino, modelo, $"run_{runOrigem}", $"run_{dir}", sheetBase, EhAtlasDesarmado);
        }

        CopiarAnimacaoModeloComAtlas(destino, modelo, "death", sheetBase, EhAtlasDesarmado);

        if (profile == HumanUnarmedProfile || sheetAcao == null)
            return;

        PlayerSpriteAnimationProfile perfilAnimacao = CarregarPerfilAnimacao(profile);
        Func<string, bool> filtroAtaque = CriarFiltroAtlasModeloAtaque(profile);
        prefixoAtaque = string.IsNullOrWhiteSpace(prefixoAtaque) ? ObterPrefixoAtaqueDoPerfil(profile) : prefixoAtaque.Trim().ToLowerInvariant();
        foreach (string dir in direcoes)
        {
            string animDestino = $"{prefixoAtaque}_attack_{dir}";
            foreach (string prefixoModelo in ObterPrefixosModeloAtaque(profile, prefixoAtaque))
            {
                string animModelo = $"{prefixoModelo}_attack_{dir}";
                if (CopiarAnimacaoModeloComAtlas(destino, modelo, animModelo, animDestino, sheetAcao, filtroAtaque, perfilAnimacao))
                    break;
            }
        }
    }

    private static string ObterPrefixoAtaqueDoPerfil(HumanFullSpriteProfile profile)
    {
        return profile?.Nome switch
        {
            "Arco" => "arqueiro",
            "Adaga" => "ladino",
            "Cajado" => "mago",
            _ => "guerreiro"
        };
    }

    private static IEnumerable<string> ObterPrefixosModeloAtaque(HumanFullSpriteProfile profile, string prefixoAtaque)
    {
        if (profile != null)
        {
            switch (profile.Nome)
            {
                case "Machado Duas Maos":
                    yield return "machado_guerra";
                    break;
                case "Espada e Escudo":
                    yield return "espada_escudo";
                    break;
                case "Maca e Escudo":
                    yield return "maca_escudo";
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(prefixoAtaque))
            yield return prefixoAtaque.Trim().ToLowerInvariant();
    }

    private static SpriteFrames CarregarModeloAnimacaoPlayer()
    {
        if (_modeloAnimacaoPlayer != null)
            return _modeloAnimacaoPlayer;

        try
        {
            if (!ResourceLoader.Exists(PlayerAnimationModelScenePath))
                return null;

            var scene = ResourceLoader.Load<PackedScene>(PlayerAnimationModelScenePath);
            var instance = scene?.Instantiate();
            var sprite = instance?.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
            _modeloAnimacaoPlayer = sprite?.SpriteFrames;
            instance?.Free();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[PLAYER] Falha ao carregar modelo editavel de animacao: {ex.Message}");
        }

        return _modeloAnimacaoPlayer;
    }

    private static bool EhAtlasDesarmado(string path)
    {
        return path.Contains("Desarmado", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Humano.png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Elfo.png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/DarkElfo.png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/MortoVivo.png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Orc.png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Troll.png", StringComparison.OrdinalIgnoreCase);
    }

    private static Func<string, bool> CriarFiltroAtlasModeloAtaque(HumanFullSpriteProfile profile)
    {
        var nomesEsperados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string sufixo in profile?.SufixosArquivo ?? Array.Empty<string>())
        {
            string nome = sufixo.Replace("{race}", "Humano").Replace("{raceNoSpace}", "Humano");
            if (nome.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                nome = nome[..^4];
            nomesEsperados.Add(nome);
        }

        return path =>
        {
            if (string.IsNullOrWhiteSpace(path) || EhAtlasDesarmado(path))
                return false;

            if (nomesEsperados.Count == 0)
                return true;

            string arquivo = System.IO.Path.GetFileNameWithoutExtension(path.Replace('\\', '/'));
            return nomesEsperados.Contains(arquivo);
        };
    }

    private static void CopiarAnimacaoModeloComAtlas(SpriteFrames destino, SpriteFrames modelo, string anim, Texture2D atlasDestino, Func<string, bool> filtroAtlasOrigem)
    {
        CopiarAnimacaoModeloComAtlas(destino, modelo, anim, anim, atlasDestino, filtroAtlasOrigem);
    }

    private static bool CopiarAnimacaoModeloComAtlas(SpriteFrames destino, SpriteFrames modelo, string animOrigem, string animDestino, Texture2D atlasDestino, Func<string, bool> filtroAtlasOrigem, PlayerSpriteAnimationProfile perfilAtaque = null)
    {
        if (destino == null || modelo == null || atlasDestino == null || !modelo.HasAnimation(animOrigem))
            return false;

        var frames = new List<(Texture2D texture, float duration)>();
        int frameCount = modelo.GetFrameCount(animOrigem);
        Vector2 atlasSize = atlasDestino.GetSize();

        for (int i = 0; i < frameCount; i++)
        {
            Texture2D frameTexture = modelo.GetFrameTexture(animOrigem, i);
            if (frameTexture is not AtlasTexture atlasOrigem || atlasOrigem.Atlas == null)
                continue;

            string origemPath = atlasOrigem.Atlas.ResourcePath ?? "";
            if (!filtroAtlasOrigem(origemPath))
                continue;

            Rect2 region = atlasOrigem.Region;
            if (perfilAtaque != null)
            {
                Rect2? normalizada = NormalizarRegiaoAtaqueModelo(region, animOrigem, perfilAtaque, atlasSize);
                if (!normalizada.HasValue)
                    continue;

                region = normalizada.Value;
            }

            if (region.Position.X + region.Size.X > atlasSize.X || region.Position.Y + region.Size.Y > atlasSize.Y)
                continue;

            frames.Add((new AtlasTexture
            {
                Atlas = atlasDestino,
                Region = region
            }, modelo.GetFrameDuration(animOrigem, i)));
        }

        if (frames.Count == 0)
            return false;

        if (destino.HasAnimation(animDestino))
            destino.RemoveAnimation(animDestino);

        destino.AddAnimation(animDestino);
        destino.SetAnimationLoop(animDestino, modelo.GetAnimationLoop(animOrigem));
        destino.SetAnimationSpeed(animDestino, modelo.GetAnimationSpeed(animOrigem));
        foreach (var frame in frames)
            destino.AddFrame(animDestino, frame.texture, frame.duration);

        return true;
    }

    private static Rect2? NormalizarRegiaoAtaqueModelo(Rect2 region, string animOrigem, PlayerSpriteAnimationProfile perfil, Vector2 atlasSize)
    {
        int dirIndex = ObterIndiceDirecaoDaAnimacao(animOrigem);
        if (dirIndex < 0)
            return region;

        int frameWidth = perfil.AttackFrameWidth > 0 ? perfil.AttackFrameWidth : perfil.FrameWidth;
        int frameHeight = perfil.AttackFrameHeight > 0 ? perfil.AttackFrameHeight : perfil.FrameHeight;
        if (frameWidth <= 0 || frameHeight <= 0)
            return region;

        int row = perfil.GetAttackRow(dirIndex);
        int startCol = Math.Max(0, perfil.GetAttackStartCol(dirIndex));
        int frameCount = Math.Max(1, perfil.GetAttackFrameCount(dirIndex));
        float rowY = row * frameHeight;
        float rowEnd = rowY + frameHeight;

        if (region.Position.Y >= rowY && region.Position.Y < rowEnd && region.Size.X <= frameWidth && region.Size.Y <= frameHeight)
        {
            int col = Math.Clamp((int)Mathf.Floor(region.Position.X / frameWidth), startCol, startCol + frameCount - 1);
            var normalizada = new Rect2(col * frameWidth, rowY, frameWidth, frameHeight);
            return normalizada.Position.X + normalizada.Size.X <= atlasSize.X && normalizada.Position.Y + normalizada.Size.Y <= atlasSize.Y
                ? normalizada
                : null;
        }

        bool tamanhoCorreto = Mathf.IsEqualApprox(region.Size.X, frameWidth) && Mathf.IsEqualApprox(region.Size.Y, frameHeight);
        if (tamanhoCorreto && Mathf.IsEqualApprox(region.Position.Y, rowY))
            return region;

        return null;
    }

    private static int ObterIndiceDirecaoDaAnimacao(string anim)
    {
        if (anim.EndsWith("_up", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (anim.EndsWith("_left", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (anim.EndsWith("_down", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (anim.EndsWith("_right", StringComparison.OrdinalIgnoreCase))
            return 3;

        return -1;
    }

    private static void CopiarAnimacoesBaseParaSpriteArmado(SpriteFrames destino, SpriteFrames origem)
    {
        if (destino == null || origem == null)
            return;

        string[] direcoes = { "up", "left", "down", "right" };
        foreach (string dir in direcoes)
        {
            CopiarAnimacao(destino, origem, $"idle_{dir}");
            CopiarAnimacao(destino, origem, $"walk_{dir}");
            CopiarAnimacao(destino, origem, $"run_{dir}");
        }

        CopiarAnimacao(destino, origem, "death");
    }

    private static void CopiarAnimacao(SpriteFrames destino, SpriteFrames origem, string anim)
    {
        if (!origem.HasAnimation(anim))
            return;

        if (destino.HasAnimation(anim))
            destino.RemoveAnimation(anim);

        destino.AddAnimation(anim);
        destino.SetAnimationLoop(anim, origem.GetAnimationLoop(anim));
        destino.SetAnimationSpeed(anim, origem.GetAnimationSpeed(anim));

        int frameCount = origem.GetFrameCount(anim);
        for (int i = 0; i < frameCount; i++)
        {
            var frame = origem.GetFrameTexture(anim, i);
            float duration = origem.GetFrameDuration(anim, i);
            destino.AddFrame(anim, frame, duration);
        }
    }

    private void RestaurarSpriteBasePersonagem()
    {
        if (AnimatedSprite == null || _spriteFramesBasePersonagem == null)
            return;

        if (_spriteCorpoAtual == "base")
            return;

        string animAtual = AnimatedSprite.Animation.ToString();
        int frameAtual = AnimatedSprite.Frame;
        float speedAtual = AnimatedSprite.SpeedScale;

        AnimatedSprite.SpriteFrames = _spriteFramesBasePersonagem;
        _spriteCorpoAtual = "base";

        if (!string.IsNullOrWhiteSpace(animAtual) && _spriteFramesBasePersonagem.HasAnimation(animAtual))
        {
            AnimatedSprite.Play(animAtual);
            int frameCount = _spriteFramesBasePersonagem.GetFrameCount(animAtual);
            if (frameCount > 0)
                AnimatedSprite.Frame = Mathf.Clamp(frameAtual, 0, frameCount - 1);
            AnimatedSprite.SpeedScale = speedAtual;
        }

        SincronizarOverlays();
    }

    private static HumanFullSpriteProfile EncontrarPerfilSpriteHumano(params ItemResource[] itens)
    {
        foreach (var item in itens)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Nome))
                continue;

            string nome = item.Nome.ToLowerInvariant();
            foreach (var profile in HumanFullSpriteProfiles)
            {
                foreach (string palavra in profile.PalavrasChave)
                {
                    if (!string.IsNullOrWhiteSpace(palavra) && nome.Contains(palavra.ToLowerInvariant()))
                        return profile;
                }
            }
        }

        return null;
    }

    private static HumanFullSpriteProfile EncontrarPerfilSpritePorClasse(string nomeClasse)
    {
        string classe = (nomeClasse ?? "").Trim().ToLowerInvariant();
        return classe switch
        {
            "arqueiro" => EncontrarPerfilSpritePorNome("Arco"),
            "ladino" or "assassino" or "assasino" => EncontrarPerfilSpritePorNome("Adaga"),
            "berseker" or "berserker" => EncontrarPerfilSpritePorNome("Machado Duas Maos"),
            "guardiao" or "guradiao" or "guardião" => EncontrarPerfilSpritePorNome("Espada e Escudo"),
            "mago" => EncontrarPerfilSpritePorNome("Cajado"),
            "prist" or "priest" or "clerigo" or "clérigo" or "sacerdote" => EncontrarPerfilSpritePorNome("Maca e Escudo"),
            _ => null
        };
    }

    private static HumanFullSpriteProfile EncontrarPerfilSpritePorNome(string nome)
    {
        foreach (var profile in HumanFullSpriteProfiles)
        {
            if (string.Equals(profile.Nome, nome, StringComparison.OrdinalIgnoreCase))
                return profile;
        }

        return null;
    }

    private string ObterPrefixoAtaqueAtual()
    {
        return ClasseRegistry.ObterPrefixoAtaqueRecomendado(NomeDaClasse);
    }

    private void AtualizarOverlaySlot(EquipamentoComponent equipamento, TipoEquipamento tipo, AnimatedSprite2D overlay)
    {
        var slot = equipamento.ObterSlot(tipo);
        if (slot != null && slot.Item != null && DeveAplicarOverlayEquipamento(slot.Item))
            AplicarOverlayEquipamento(overlay, slot.Item);
        else
            LimparOverlayEquipamento(overlay);
    }

    private static bool DeveAplicarOverlayEquipamento(ItemResource item)
    {
        if (item == null)
            return false;

        return item.Tipo != TipoEquipamento.Arma &&
            (item.SpriteFramesEquipamento != null || item.SpritesheetEquipamento != null);
    }

    private void UpdateAnimation(Vector2 velocity)
    {
        if (AnimatedSprite == null) return;

        string currentAnim = AnimatedSprite.Animation.ToString();

        if (IsAttacking && currentAnim.Contains("attack"))
            return;

        if (velocity.Length() > 10.0f)
        {
            _idleTransitionTimer = 0f;
            _holdingBeforeIdle = false;
            CurrentDirection = DirectionUtil.VectorToDirectionString(velocity);
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);

            if (IsSprinting)
            {
                string runAnim = $"run_{cardinal}";
                string walkAnim = $"walk_{cardinal}";
                AnimatedSprite.Play(AnimatedSprite.SpriteFrames?.HasAnimation(runAnim) == true ? runAnim : walkAnim);
                AnimatedSprite.SpeedScale = (velocity.Length() / (MaxSpeed * 1.8f)) * 1.5f;
            }
            else
            {
                AnimatedSprite.Play($"walk_{cardinal}");
                AnimatedSprite.SpeedScale = (velocity.Length() / MaxSpeed) * 1.5f;
            }
        }
        else
        {
            if (!currentAnim.StartsWith("idle_", StringComparison.Ordinal))
            {
                _idleTransitionTimer += (float)GetPhysicsProcessDeltaTime();
                if (_idleTransitionTimer < IdleTransitionDelay)
                {
                    if (!_holdingBeforeIdle && AnimatedSprite.IsPlaying())
                    {
                        AnimatedSprite.Stop();
                        SincronizarOverlays();
                        _holdingBeforeIdle = true;
                    }
                    return;
                }
            }

            _holdingBeforeIdle = false;
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);
            AnimatedSprite.Play($"idle_{cardinal}");
            AnimatedSprite.SpeedScale = 1.0f; 
        }
    }

    public virtual void Atacar()
    {
        if (AnimatedSprite == null) return;
        if (IsAttacking || _attackCooldownRemaining > 0f) return;

        var targetFinder = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (TryGetSelectedTarget(targetFinder, out var targetNode, out _))
        {
            Vector2 dirToTarget = (targetNode.GlobalPosition - GlobalPosition).Normalized();
            if (dirToTarget.LengthSquared() > 0.001f)
                CurrentDirection = DirectionUtil.VectorToDirectionString(dirToTarget);
        }

        string prefixoAtaque = ObterPrefixoAtaqueAtual();
        string animacaoDeAtaque = $"{prefixoAtaque}_attack_{CurrentDirection}";

        // Fallback para animação cardinal se a 8-dir não existir
        if (AnimatedSprite.SpriteFrames != null && !AnimatedSprite.SpriteFrames.HasAnimation(animacaoDeAtaque))
        {
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);
            animacaoDeAtaque = $"{prefixoAtaque}_attack_{cardinal}";
        }

        if (AnimatedSprite.SpriteFrames != null && AnimatedSprite.SpriteFrames.HasAnimation(animacaoDeAtaque))
        {
            IsAttacking = true;
            _attackTimeoutCounter = 0f;
            AnimatedSprite.SpriteFrames.SetAnimationLoop(animacaoDeAtaque, false);
            AnimatedSprite.Play(animacaoDeAtaque);
            AnimatedSprite.SpeedScale = AttackAnimationSpeedScale;
            SincronizarOverlays();
            GD.Print($"[PLAYER] Iniciando ataque: '{animacaoDeAtaque}'");
        }
        else
        {
            GD.Print($"[PLAYER] Animação '{animacaoDeAtaque}' não encontrada. Atacando sem animação.");
            IniciarCooldownAtaqueBasico();
        }

        // A animação também é uma ação online: servidor valida e replica aos demais jogadores.
        if (_network != null && _network.IsConnected)
            _network.SendPlayerAction(1, DirectionUtil.DirectionToVector(CurrentDirection));

        // Executa a lógica de ataque independente da animação
        if (ClasseEhMago())
        {
            if (ProjetilScene != null)
            {
                if (IsAttacking)
                    _projetilDisparado = false;
                else
                    DispararProjetil();
            }
            else
                GD.PrintErr("[PLAYER] Erro: ProjetilScene não configurada!");
        }
        else if (string.Equals(NomeDaClasse, "arqueiro", StringComparison.OrdinalIgnoreCase))
        {
            if (ProjetilScene != null)
                _projetilDisparado = false;
            else
                GD.PrintErr("[PLAYER] Erro: ProjetilScene não configurada!");
        }
        else
        {
            ExecutarAtaqueMelee();
        }
    }

    private bool DeveEncerrarAtaqueNoUltimoFrame()
    {
        if (AnimatedSprite == null || AnimatedSprite.SpriteFrames == null)
            return false;

        string anim = AnimatedSprite.Animation.ToString();
        if (!anim.Contains("attack", StringComparison.OrdinalIgnoreCase))
            return false;

        int frameCount = AnimatedSprite.SpriteFrames.GetFrameCount(anim);
        return frameCount > 0 && AnimatedSprite.Frame >= frameCount - 1;
    }

    private void TentarDispararProjetilNoFinalDaAnimacao()
    {
        if (_projetilDisparado || ProjetilScene == null)
            return;

        bool usaProjetil = ClasseEhMago() || ClasseEhArqueiro();
        if (!usaProjetil)
            return;

        if (ClasseEhArqueiro() && !TemArcoEquipado())
        {
            GD.Print("[PLAYER] Arqueiro sem arco equipado, projetil não disparado.");
            _projetilDisparado = true;
            return;
        }

        DispararProjetil();
        _projetilDisparado = true;
    }

    private void FinalizarAtaqueAtual(string log = "")
    {
        IsAttacking = false;
        _attackTimeoutCounter = 0f;
        IniciarCooldownAtaqueBasico();

        if (AnimatedSprite != null)
        {
            AnimatedSprite.SpeedScale = 1.0f;
            RetomarAnimacaoAposAtaque();
        }

        if (!string.IsNullOrWhiteSpace(log))
            GD.Print(log);
    }

    private void IniciarCooldownAtaqueBasico()
    {
        _attackCooldownRemaining = Mathf.Max(0f, ObterCooldownAtaqueBasico());
    }

    private float ObterCooldownAtaqueBasico()
    {
        string classe = NomeDaClasse?.Trim().ToLowerInvariant() ?? "";
        return classe switch
        {
            "berseker" or "berserker" or "barbaro" or "bárbaro" => 1.5f,
            "arqueiro" or "mago" => 1.1f,
            _ => BasicAttackCooldown,
        };
    }

    private void RetomarAnimacaoAposAtaque()
    {
        if (AnimatedSprite == null)
            return;

        _idleTransitionTimer = IdleTransitionDelay;
        _holdingBeforeIdle = false;

        Vector2 velocity = Velocity;
        if (velocity.Length() > 10.0f)
        {
            string cardinalMovimento = DirectionUtil.DirectionToCardinal(DirectionUtil.VectorToDirectionString(velocity));
            string runAnim = $"run_{cardinalMovimento}";
            string walkAnim = $"walk_{cardinalMovimento}";
            string anim = IsSprinting && AnimatedSprite.SpriteFrames?.HasAnimation(runAnim) == true ? runAnim : walkAnim;
            if (AnimatedSprite.SpriteFrames?.HasAnimation(anim) == true)
                AnimatedSprite.Play(anim);
            return;
        }

        string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);
        string idleAnim = $"idle_{cardinal}";
        if (AnimatedSprite.SpriteFrames?.HasAnimation(idleAnim) == true)
            AnimatedSprite.Play(idleAnim);
    }

    private void DispararProjetil()
    {
        if (ProjetilScene == null) return;

        var novoProjetil = ProjetilScene.Instantiate<Node2D>();
        if (novoProjetil == null)
        {
            GD.PrintErr("[PLAYER] Erro: não foi possível instanciar o projetil.");
            return;
        }

        Vector2 direcaoDoVetor = DirectionUtil.DirectionToVector(CurrentDirection);
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (TryGetSelectedTarget(gameNet, out var selectedNode, out _))
        {
            direcaoDoVetor = (selectedNode.GlobalPosition - GlobalPosition).Normalized();
            CurrentDirection = DirectionUtil.VectorToDirectionString(direcaoDoVetor);
        }

        Vector2 origem = AnimatedSprite?.GlobalPosition ?? GlobalPosition;
        novoProjetil.GlobalPosition = origem + direcaoDoVetor * 50;
        GetParent().AddChild(novoProjetil);

        GD.Print($"[PLAYER] Projetil instanciado em {novoProjetil.GlobalPosition}, direcao: {direcaoDoVetor}");

        if (novoProjetil is Projetil proj)
        {
            proj.DefinirDono(this);
            proj.DefinirDirecao(direcaoDoVetor);

            var equip = GetNodeOrNull<EquipamentoComponent>("EquipamentoComponent");
            if (string.Equals(NomeDaClasse, "mago", StringComparison.OrdinalIgnoreCase))
            {
                proj.Speed = 200.0f;
                proj.DanoMin = equip?.DanoMagicoMin ?? 20;
                proj.DanoMax = equip?.DanoMagicoMax ?? 30;
                proj.EhDanoMagico = true;
            }
            else if (string.Equals(NomeDaClasse, "arqueiro", StringComparison.OrdinalIgnoreCase))
            {
                proj.Speed = 450.0f;
                proj.DanoMin = equip?.DanoFisicoMin ?? 10;
                proj.DanoMax = equip?.DanoFisicoMax ?? 16;
                proj.EhDanoMagico = false;
            }
        }
        else
        {
            GD.PrintErr("[PLAYER] Erro: O projétil instanciado não é do tipo Projetil.");
        }

        // Envia pacote para replicar o projétil para outros jogadores
        if (gameNet != null && gameNet.IsConnected)
        {
            byte projType = ClasseEhMago() ? (byte)1 : (byte)0;
            gameNet.SendProjectileFire(novoProjetil.GlobalPosition.X, novoProjetil.GlobalPosition.Y, direcaoDoVetor.X, direcaoDoVetor.Y, projType);
        }
    }

    private bool ClasseEhMago()
    {
        return string.Equals(NomeDaClasse, "mago", StringComparison.OrdinalIgnoreCase);
    }

    private bool ClasseEhArqueiro()
    {
        return string.Equals(NomeDaClasse, "arqueiro", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ClassePodeUsarProjetilBasico(string nomeClasse)
    {
        return string.Equals(nomeClasse, "mago", StringComparison.OrdinalIgnoreCase)
            || string.Equals(nomeClasse, "arqueiro", StringComparison.OrdinalIgnoreCase);
    }

    private bool TemArcoEquipado()
    {
        var equip = GetNodeOrNull<EquipamentoComponent>("EquipamentoComponent");
        if (equip == null) return false;
        var slotArma = equip.ObterSlot(TipoEquipamento.Arma);
        return slotArma?.Item != null && slotArma.Item.Nome.Contains("arco", StringComparison.OrdinalIgnoreCase);
    }

    private void ExecutarAtaqueMelee()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");

        if (gameNet != null && gameNet.IsConnected)
        {
            ulong? targetId = _selectedTargetId;
            float dist;
            if (!TryGetSelectedTarget(gameNet, out _, out dist))
                targetId = FindNearestNetworkEntity(gameNet, out dist);
            if (targetId.HasValue && dist <= MeleeAttackRange)
            {
                gameNet.SendAttack(targetId.Value);
                GD.Print($"[PLAYER] Enviando C2S_Attack para entidade {targetId.Value}");
            }
            else
            {
                GD.Print("[PLAYER] Nenhum alvo em alcance melee.");
            }
            return;
        }

        GD.PrintErr("[PLAYER] Dano melee local bloqueado. Combate deve passar pelo servidor.");
    }

    private bool TryGetSelectedTarget(GameNetwork gameNet, out Node2D targetNode, out float distance)
    {
        targetNode = null;
        distance = float.MaxValue;
        if (gameNet == null || !_selectedTargetId.HasValue) return false;
        Node2D node = null;
        foreach (var entry in gameNet.GetAllEntities())
        {
            if (entry.Key == _selectedTargetId.Value)
            {
                node = entry.Value;
                break;
            }
        }
        if (node == null || !IsInstanceValid(node))
        {
            LimparTarget();
            return false;
        }
        targetNode = node;
        distance = DistanciaMeleeAte(node);
        return true;
    }

    private ulong? FindNearestNetworkEntity(GameNetwork gameNet, out float closestDist)
    {
        closestDist = float.MaxValue;
        ulong? closest = null;

        foreach (var kvp in gameNet.GetAllEntities())
        {
            if (kvp.Key == gameNet.LocalPlayerId) continue;
            if (!IsInstanceValid(kvp.Value) || kvp.Value is not Inimigo) continue;

            float d = DistanciaMeleeAte(kvp.Value);
            if (d < closestDist)
            {
                closestDist = d;
                closest = kvp.Key;
            }
        }

        return closest;
    }

    private float DistanciaMeleeAte(Node2D target)
    {
        float centerDistance = GlobalPosition.DistanceTo(target.GlobalPosition);
        float targetRadius = ObterRaioContatoMelee(target);
        return Mathf.Max(0f, centerDistance - targetRadius - MeleeTargetContactPadding);
    }

    private static float ObterRaioContatoMelee(Node2D target)
    {
        var shapeNode = target.FindChild("CollisionShape2D", true, false) as CollisionShape2D;
        if (shapeNode?.Shape == null)
            return MeleeTargetFallbackRadius;

        float radius = shapeNode.Shape switch
        {
            CircleShape2D circle => circle.Radius,
            RectangleShape2D rect => Mathf.Max(rect.Size.X, rect.Size.Y) * 0.5f,
            CapsuleShape2D capsule => Mathf.Max(capsule.Radius, capsule.Height * 0.5f),
            _ => MeleeTargetFallbackRadius,
        };

        float scale = Mathf.Max(Mathf.Abs(shapeNode.GlobalScale.X), Mathf.Abs(shapeNode.GlobalScale.Y));
        return Mathf.Clamp(radius * Mathf.Max(scale, 0.01f), 16f, 72f);
    }

    private int CalcularDanoFisico()
    {
        var equipamento = FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        return equipamento != null ? equipamento.CalcularDanoFisicoAleatorio() : 12;
    }

    public void LevarDano(int quantidade, Node attacker = null)
    {
        if (quantidade <= 0) return;
        if (_isInvincible)
        {
            GD.Print("[PLAYER] Invencível, nenhum dano recebido.");
            return;
        }

        if (_currentReflect > 0 && attacker != null)
        {
            if (attacker is Inimigo inimigo)
                inimigo.LevarDano(_currentReflect);
            else
                attacker.CallDeferred("LevarDano", _currentReflect);
            GD.Print($"[PLAYER] Refletiu {_currentReflect} de dano para o atacante.");
        }

        // Absorve dano com shield primeiro
        if (CurrentShield > 0)
        {
            int absorb = Mathf.Min(CurrentShield, quantidade);
            CurrentShield -= absorb;
            quantidade -= absorb;
            GD.Print($"[PLAYER] Escudo absorveu {absorb} de dano. Escudo restante: {CurrentShield}");
        }

        if (quantidade <= 0)
        {
            EmitSignal(SignalName.StatusAtualizado);
            return;
        }

        CurrentHealth -= quantidade;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);
        GD.Print($"[PLAYER] Levou {quantidade} de dano! Vida restante: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
            Morrer();

        EmitSignal(SignalName.StatusAtualizado);
    }

    private void Morrer()
    {
        if (_isLyingDown)
            return;

        GD.Print("[PLAYER] O Player foi derrotado!");
        IsAttacking = false;
        _isLyingDown = true;
        _idleTransitionTimer = 0f;
        _holdingBeforeIdle = false;
        AddToGroup("PlayersDowned");
        Velocity = Vector2.Zero;
        SetPhysicsProcess(false);
        SetProcess(false);

        if (AnimatedSprite != null && AnimatedSprite.SpriteFrames != null)
        {
            if (AnimatedSprite.SpriteFrames.HasAnimation("death"))
            {
                AnimatedSprite.SpriteFrames.SetAnimationLoop("death", false);
                AnimatedSprite.Play("death");
                if (!_morteAnimationFinishedConectado)
                {
                    AnimatedSprite.AnimationFinished += AoTerminarMorte;
                    _morteAnimationFinishedConectado = true;
                }
                SincronizarOverlays();
            }
            else
            {
                AnimatedSprite.Stop();
            }
        }

        var respawnUI = ObterRespawnUI();
        if (respawnUI != null)
            respawnUI.ShowDeathScreen();
    }

    private void AoTerminarMorte()
    {
        if (AnimatedSprite == null) return;
        string anim = AnimatedSprite.Animation.ToString();
        if (string.Equals(anim, "death", StringComparison.Ordinal))
        {
            int frameCount = AnimatedSprite.SpriteFrames?.GetFrameCount("death") ?? 0;
            if (frameCount > 0)
                AnimatedSprite.Frame = frameCount - 1;
            AnimatedSprite.Stop();
            if (_morteAnimationFinishedConectado)
            {
                AnimatedSprite.AnimationFinished -= AoTerminarMorte;
                _morteAnimationFinishedConectado = false;
            }
            SincronizarOverlays();
        }
    }

    public void Reviver(float x, float y, int health, int maxHealth, int mana = 0, int maxMana = 0)
    {
        _isLyingDown = false;
        _idleTransitionTimer = 0f;
        _holdingBeforeIdle = false;
        RemoveFromGroup("PlayersDowned");
        GlobalPosition = new Vector2(x, y);
        SetHealthFromServer(health, maxHealth);
        if (maxMana > 0)
            SetManaFromServer(mana, maxMana);
        SetPhysicsProcess(true);
        SetProcess(true);

        var respawnUI = ObterRespawnUI();
        if (respawnUI != null)
            respawnUI.HideDeathScreen();

        GD.Print("[PLAYER] Reviveu!");
    }

    private RespawnUI ObterRespawnUI()
    {
        return GetNodeOrNull<RespawnUI>("/root/main/HUD/RespawnUI")
            ?? GetNodeOrNull<RespawnUI>("/root/Main/HUD/RespawnUI")
            ?? GetTree()?.CurrentScene?.FindChild("RespawnUI", true, false) as RespawnUI;
    }

    private bool TryReviveDownedPlayer()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected) return false;

        var entities = GetTree()?.GetNodesInGroup("PlayersDowned");
        if (entities == null) return false;

        Node2D? nearest = null;
        float nearestDist = 100f;
        foreach (Node node in entities)
        {
            if (node is Node2D n2d)
            {
                float d = GlobalPosition.DistanceTo(n2d.GlobalPosition);
                if (d < 60f && (nearest == null || d < nearestDist))
                {
                    nearestDist = d;
                    nearest = n2d;
                }
            }
        }

        if (nearest != null && nearest.HasMeta("network_id"))
        {
            ulong targetId = (ulong)nearest.GetMeta("network_id").AsInt64();
            GD.Print($"[PLAYER] Revivendo jogador {targetId}");
            gameNet.SendRevivePlayer(targetId);
            return true;
        }

        return false;
    }

    public bool TryInteractNpc()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        bool online = gameNet != null && gameNet.IsConnected;

        Node2D? nearest = null;
        float nearestDist = NpcInteractionRange;

        var npcNodes = GetTree()?.GetNodesInGroup("NPC");
        if (npcNodes == null) return false;

        foreach (Node node in npcNodes)
        {
            if (node is Node2D n2d)
            {
                if (online && !n2d.HasMeta("network_id"))
                    continue;

                float d = GlobalPosition.DistanceTo(n2d.GlobalPosition);
                if (d <= NpcInteractionRange && (nearest == null || d < nearestDist))
                {
                    nearestDist = d;
                    nearest = n2d;
                }
            }
        }

        if (nearest == null) return false;

        if (online && nearest.HasMeta("network_id"))
        {
            ulong npcId = (ulong)nearest.GetMeta("network_id").AsInt64();
            GD.Print($"[NPC] Solicitando interação online: id={npcId}, distância={nearestDist:F1}");
            gameNet.SendNpcInteract(npcId);
            return true;
        }

        GD.PrintErr("[NPC] Interacao local bloqueada. NPC precisa de network_id e resposta do servidor.");
        return true;

    }

    private void UpdateNpcPrompts()
    {
        var npcNodes = GetTree()?.GetNodesInGroup("NPC");
        if (npcNodes == null) return;

        foreach (Node node in npcNodes)
        {
            if (node is Node2D n2d)
            {
                float d = GlobalPosition.DistanceTo(n2d.GlobalPosition);
                bool near = d <= NpcInteractionRange;

                var prompt = n2d.FindChild("InteractPrompt", true, false) as Label;
                if (prompt != null)
                    prompt.Visible = near;
            }
        }
    }

    public void TryPickupLoot()
    {
        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet == null || !gameNet.IsConnected) return;

        var lootNodes = GetTree()?.GetNodesInGroup("Loot");
        Node2D? nearest = null;
        const float pickupRange = 120f;
        float nearestDist = pickupRange;

        if (lootNodes != null)
        {
            foreach (Node node in lootNodes)
            {
                if (node is Node2D n2d)
                {
                    float d = GlobalPosition.DistanceTo(n2d.GlobalPosition);

                    bool isServerLoot = n2d.HasMeta("loot_id");
                    bool isSceneItem = n2d is ItemColetavel;

                    if (!isServerLoot && !isSceneItem) continue;

                    bool near = d <= pickupRange;

                    if (isServerLoot)
                    {
                        UpdateLootPromptVisibility(n2d, near);
                    }

                    if (near && (isServerLoot || isSceneItem) && (nearest == null || d < nearestDist))
                    {
                        nearestDist = d;
                        nearest = n2d;
                    }
                }
            }
        }

        if (nearest != null)
        {
            if (nearest.HasMeta("loot_id"))
            {
                ulong lootId = (ulong)nearest.GetMeta("loot_id").AsInt64();
                gameNet.SendLootPickup(lootId);
                return;
            }

            if (nearest is ItemColetavel itemColetavel)
                itemColetavel.Coletar();
        }
    }

    private void UpdateLootPromptVisibility(Node2D lootNode, bool visible)
    {
        var prompt = lootNode.FindChild("LootPrompt", true, false) as Label;
        if (prompt == null)
            prompt = lootNode.FindChild("InteractPrompt", true, false) as Label;

        if (prompt != null && prompt.Text == "[F]")
            prompt.Visible = visible;
    }

    // Buff state
    private int CurrentShield = 0;
    private bool _isSilenced = false;
    private bool _isStunned = false;
    private bool _isRooted = false;
    private bool _isFeared = false;
    private bool _isConfused = false;
    private bool _isSleeping = false;
    private bool _isPrisoned = false;
    private bool _isFrozen = false;
    private bool _isBlinded = false;
    private bool _isInvincible = false;
    private int _currentReflect = 0;
    private float _speedMultiplier = 1.0f;

    public void ApplyTemporaryBuff(string buffType, float duration, int power)
    {
        switch (buffType.ToLower())
        {
            case "shield":
                CurrentShield += power;
                GD.Print($"[PLAYER] Escudo aplicado: {power} (duração {duration}s). Total: {CurrentShield}");
                // Timer to remove shield after duration
                var t = new Timer();
                t.OneShot = true;
                t.WaitTime = duration;
                AddChild(t);
                t.Timeout += () => { CurrentShield = Mathf.Max(0, CurrentShield - power); t.QueueFree(); };
                t.Start();
                break;
            case "silence":
                _isSilenced = true;
                GD.Print($"[PLAYER] Silenciado por {duration}s");
                var ts = new Timer(); ts.OneShot = true; ts.WaitTime = duration; AddChild(ts);
                ts.Timeout += () => { _isSilenced = false; ts.QueueFree(); };
                ts.Start();
                break;
            case "stun":
                _isStunned = true;
                SetPhysicsProcess(false);
                GD.Print($"[PLAYER] Atordoado por {duration}s");
                var tstu = new Timer(); tstu.OneShot = true; tstu.WaitTime = duration; AddChild(tstu);
                tstu.Timeout += () => { _isStunned = false; SetPhysicsProcess(true); tstu.QueueFree(); };
                tstu.Start();
                break;
            case "bleed":
                GD.Print($"[PLAYER] Sangramento aplicado por {duration}s (dano {power} por tick)");
                IniciarDotTimer("bleed", duration, power);
                break;
            case "burn":
                GD.Print($"[PLAYER] Queimação aplicada por {duration}s (dano {power} por tick)");
                IniciarDotTimer("burn", duration, power);
                break;
            case "poison":
                GD.Print($"[PLAYER] Veneno aplicado por {duration}s (dano {power} por tick)");
                IniciarDotTimer("poison", duration, power);
                break;
            case "slow":
                _speedMultiplier = 1.0f - (power / 100f);
                GD.Print($"[PLAYER] Lentidão aplicada: x{_speedMultiplier} por {duration}s");
                var tsl = new Timer(); tsl.OneShot = true; tsl.WaitTime = duration; AddChild(tsl);
                tsl.Timeout += () => { _speedMultiplier = 1.0f; tsl.QueueFree(); };
                tsl.Start();
                break;
            case "root":
                _isRooted = true;
                GD.Print($"[PLAYER] Enraizado por {duration}s");
                var tr = new Timer(); tr.OneShot = true; tr.WaitTime = duration; AddChild(tr);
                tr.Timeout += () => { _isRooted = false; tr.QueueFree(); };
                tr.Start();
                break;
            case "fear":
                _isFeared = true;
                GD.Print($"[PLAYER] Amedrontado por {duration}s");
                var tf = new Timer(); tf.OneShot = true; tf.WaitTime = duration; AddChild(tf);
                tf.Timeout += () => { _isFeared = false; tf.QueueFree(); };
                tf.Start();
                break;
            case "confusion":
                _isConfused = true;
                GD.Print($"[PLAYER] Confundido por {duration}s");
                var tc = new Timer(); tc.OneShot = true; tc.WaitTime = duration; AddChild(tc);
                tc.Timeout += () => { _isConfused = false; tc.QueueFree(); };
                tc.Start();
                break;
            case "sleep":
                _isSleeping = true;
                GD.Print($"[PLAYER] Dormindo por {duration}s");
                var td = new Timer(); td.OneShot = true; td.WaitTime = duration; AddChild(td);
                td.Timeout += () => { _isSleeping = false; td.QueueFree(); };
                td.Start();
                break;
            case "prison":
                _isPrisoned = true;
                _isSilenced = true;
                GD.Print($"[PLAYER] Preso por {duration}s");
                var tp = new Timer(); tp.OneShot = true; tp.WaitTime = duration; AddChild(tp);
                tp.Timeout += () => { _isPrisoned = false; _isSilenced = false; tp.QueueFree(); };
                tp.Start();
                break;
            case "freeze":
                _isFrozen = true;
                GD.Print($"[PLAYER] Congelado por {duration}s");
                var tfz = new Timer(); tfz.OneShot = true; tfz.WaitTime = duration; AddChild(tfz);
                tfz.Timeout += () => { _isFrozen = false; tfz.QueueFree(); };
                tfz.Start();
                break;
            case "invincibility":
                _isInvincible = true;
                GD.Print($"[PLAYER] Invencível por {duration}s");
                var ti = new Timer(); ti.OneShot = true; ti.WaitTime = duration; AddChild(ti);
                ti.Timeout += () => { _isInvincible = false; ti.QueueFree(); };
                ti.Start();
                break;
            case "blindness":
                _isBlinded = true;
                GD.Print($"[PLAYER] Cegueira aplicada por {duration}s");
                var tb = new Timer(); tb.OneShot = true; tb.WaitTime = duration; AddChild(tb);
                tb.Timeout += () => { _isBlinded = false; tb.QueueFree(); };
                tb.Start();
                break;
            case "reflect":
                _currentReflect = power;
                GD.Print($"[PLAYER] Reflexão de dano aplicada por {duration}s: {power}");
                var trf = new Timer(); trf.OneShot = true; trf.WaitTime = duration; AddChild(trf);
                trf.Timeout += () => { _currentReflect = 0; trf.QueueFree(); };
                trf.Start();
                break;
            case "curse":
                GD.Print($"[PLAYER] Maldição aplicada por {duration}s");
                break;
            case "taunt":
                GD.Print($"[PLAYER] Taunt aplicado por {duration}s");
                break;
            default:
                GD.Print($"[PLAYER] Buff genérico '{buffType}' aplicado por {duration}s (power {power})");
                break;
        }
    }

    public void RemoveTemporaryBuff(string buffType)
    {
        switch (buffType.ToLower())
        {
            case "shield":
                CurrentShield = 0; break;
            case "silence":
                _isSilenced = false; break;
            case "stun":
                _isStunned = false; SetPhysicsProcess(true); break;
            case "slow":
                _speedMultiplier = 1.0f; break;
            case "root":
                _isRooted = false; break;
            case "fear":
                _isFeared = false; break;
            case "confusion":
                _isConfused = false; break;
            case "sleep":
                _isSleeping = false; break;
            case "prison":
                _isPrisoned = false; _isSilenced = false; break;
            case "freeze":
                _isFrozen = false; break;
            case "invincibility":
                _isInvincible = false; break;
            case "blindness":
                _isBlinded = false; break;
            case "reflect":
                _currentReflect = 0; break;
            default:
                break;
        }
        GD.Print($"[PLAYER] Buff '{buffType}' removido");
    }

        private void IniciarDotTimer(string tipo, float duracao, int danoPorTick)
        {
            var dot = new Timer();
            dot.Name = $"DOT_{tipo}";
            dot.OneShot = false;
            dot.WaitTime = 1.0f;
            dot.Timeout += () =>
            {
                if (!IsInstanceValid(this)) { dot.QueueFree(); return; }
                LevarDano(danoPorTick);
            };
            AddChild(dot);
            dot.Start();

            var stop = new Timer();
            stop.Name = $"DOT_{tipo}_stop";
            stop.OneShot = true;
            stop.WaitTime = duracao;
            stop.Timeout += () =>
            {
                if (!IsInstanceValid(this)) return;
                if (IsInstanceValid(dot)) dot.QueueFree();
                stop.QueueFree();
            };
            AddChild(stop);
            stop.Start();
        }

        protected virtual void OnAnimationFinished()
    {
        if (AnimatedSprite == null) return;

        string anim = AnimatedSprite.Animation.ToString();

        if (anim.Contains("attack"))
        {
            TentarDispararProjetilNoFinalDaAnimacao();
            FinalizarAtaqueAtual($"[PLAYER] ✅ Animação de ataque '{anim}' terminou. Liberando IsAttacking.");
        }
        // death handled by AoTerminarMorte
    }

    private void CriarSombra()
    {
        var img = Image.CreateEmpty(32, 10, false, Image.Format.Rgba8);
        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 4.5f) / 4.5f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= 1.0f)
                    img.SetPixel(x, y, new Color(0, 0, 0, Mathf.Lerp(0.35f, 0.0f, dist)));
                else
                    img.SetPixel(x, y, Colors.Transparent);
            }
        }
        var tex = ImageTexture.CreateFromImage(img);

        _shadowSprite = new Sprite2D();
        _shadowSprite.Texture = tex;
        _shadowSprite.Scale = new Vector2(1.5f, 1.5f);
        _shadowSprite.ZIndex = -1;
        _shadowSprite.Position = new Vector2(0, 12);
        AddChild(_shadowSprite);
        _shadowSprite.Owner = Owner;
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_network != null)
        {
            _network.OnRespawn -= OnRespawnReceived;
            _network.OnTeleport -= OnTeleportReceived;
            _network.OnEnterWorld -= AplicarProgressaoServidorPendente;
            _network.OnGainExp -= OnGainExpReceived;
            _network.OnLevelUp -= OnLevelUpReceived;
            _network.OnSceneChange -= OnSceneChangeReceived;
        }

        if (AnimatedSprite != null && _morteAnimationFinishedConectado)
        {
            AnimatedSprite.AnimationFinished -= AoTerminarMorte;
            _morteAnimationFinishedConectado = false;
        }
    }
}
