using Godot;
using System;
using System.Collections.Generic;
#nullable enable annotations

public partial class Player : CharacterBody2D
{
    private const float NpcInteractionRange = 180f;
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
    private AnimatedSprite2D _armaOverlay;
    private AnimatedSprite2D _armaduraOverlay;
    private AnimatedSprite2D _capaceteOverlay;
    private AnimatedSprite2D _luvasOverlay;
    private AnimatedSprite2D _botasOverlay;
    private sealed class WeaponVisualProfile
    {
        public readonly string Nome;
        public readonly string[] PalavrasChave;
        public readonly string BasePath;
        public readonly string AttackPath;
        public readonly int[] MovimentoRows;
        public readonly int MovimentoFrameSize;
        public readonly int[] AtaqueRows;
        public readonly int AtaqueFrameSize;

        public WeaponVisualProfile(string nome, string[] palavrasChave, string basePath, string attackPath,
            int[] movimentoRows, int movimentoFrameSize, int[] ataqueRows, int ataqueFrameSize)
        {
            Nome = nome;
            PalavrasChave = palavrasChave;
            BasePath = basePath;
            AttackPath = attackPath;
            MovimentoRows = movimentoRows;
            MovimentoFrameSize = movimentoFrameSize;
            AtaqueRows = ataqueRows;
            AtaqueFrameSize = ataqueFrameSize;
        }
    }

    private static readonly WeaponVisualProfile[] WeaponVisualProfiles =
    {
        new("Arco", new[] { "arco" },
            "res://Itens/aparence/Armas/Arco 1.png",
            "res://Itens/aparence/Armas/Arco 1 Attack.png",
            new[] { 27, 28, 29, 30 }, 128,
            new[] { 16, 17, 18, 19 }, 64),

        new("Adaga", new[] { "adaga", "lamina", "lâmina" },
            "res://Itens/aparence/Armas/Adaga walk.png",
            "res://Itens/aparence/Armas/Adaga Attack.png",
            new[] { 8, 9, 10, 11 }, 64,
            new[] { 12, 13, 14, 15 }, 64),

        new("Espada", new[] { "espada" },
            "res://Itens/aparence/Armas/Espada walk.png",
            "res://Itens/aparence/Armas/Espada Attack.png",
            new[] { 8, 9, 10, 11 }, 64,
            new[] { 28, 29, 30, 31 }, 128),

        new("Machadao", new[] { "machado", "machadao", "machadão" },
            "res://Itens/aparence/Armas/Machadao walk.png",
            "res://Itens/aparence/Armas/Machadao Attack.png",
            new[] { 8, 9, 10, 11 }, 64,
            new[] { 29, 30, 31, 32 }, 128),

        new("Martelo", new[] { "martelo" },
            "res://Itens/aparence/Armas/Martelo Walk.png",
            "res://Itens/aparence/Armas/Martelo Attack.png",
            new[] { 8, 9, 10, 11 }, 64,
            new[] { 29, 30, 31, 32 }, 128),

        new("Cristal da Staff", new[] { "cristal" },
            "res://Itens/aparence/Armas/Cristal da Satff walk.png",
            "res://Itens/aparence/Armas/Cristal da Staff Attack.png",
            new[] { 8, 9, 10, 11 }, 64,
            new[] { 29, 30, 31, 32 }, 128),

        new("Staff", new[] { "staff", "cajado" },
            "res://Itens/aparence/Armas/Staff walk.png",
            "res://Itens/aparence/Armas/Staff Attack.png",
            new[] { 8, 9, 10, 11 }, 64,
            new[] { 29, 30, 31, 32 }, 128),
    };
    public string CurrentDirection { get; protected set; } = "down";
    protected bool IsAttacking = false;
    private float _attackTimeoutCounter = 0f;
    private float _maxAttackDuration = 0.8f;
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

    [Export] public int MaxStamina = 100;
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
            p.CurrentHealth = Math.Min(p.MaxHealth, p.MaxHealth / 2);
            p.SetPhysicsProcess(true);
            GD.Print("[PLAYER] Revive aplicado em alvo.");
        }
    }

    // (full implementations later in file)
    [Export] public float MeleeAttackRange = 88.0f;
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
            _armaOverlay = GetNodeOrNull<AnimatedSprite2D>("ArmaOverlay");
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
            if (!IsAttacking)
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

    private void OnRespawnReceived(ulong entityId, float x, float y, int health, int maxHealth)
    {
        if (entityId == _network?.LocalPlayerId)
            Reviver(x, y, health, maxHealth);
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

        if (AnimatedSprite != null)
        {
            var frames = classe.ObterSpriteFramesCompletos();
            if (frames != null && frames.GetAnimationNames().Length > 0)
            {
                AnimatedSprite.SpriteFrames = frames;
                GD.Print($"[PLAYER] Sprites aplicados: {escolhido.Raca?.NomeRaca} / {classe.NomeClasse}");
            }
            else
            {
                GD.PrintErr("[PLAYER] Nenhum sprite encontrado para a raca/classe selecionada.");
            }
        }

        if (classe.UsaProjetil)
        {
            ProjetilScene = classe.CenaDoProjetil;
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
        if (IsAttacking)
        {
            _attackTimeoutCounter += (float)delta;
            if (_attackTimeoutCounter >= _maxAttackDuration)
            {
                GD.PrintErr("[PLAYER] ✘ TIMEOUT: Ataque demorou muito, liberando manualmente!");
                IsAttacking = false;
                _attackTimeoutCounter = 0f;
            }
        }

        // Dispara projetil no meio da animação de ataque (arqueiro)
        if (IsAttacking && !_projetilDisparado && AnimatedSprite.Frame >= 2 && AnimatedSprite.Animation.ToString().Contains("attack"))
        {
            if (ProjetilScene != null)
            {
                if (TemArcoEquipado())
                {
                    DispararProjetil();
                    _projetilDisparado = true;
                }
                else
                {
                    GD.Print("[PLAYER] Arqueiro sem arco equipado, projetil não disparado.");
                    _projetilDisparado = true;
                }
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
        SincronizarOverlay(_armaOverlay, anim, frame, speed);
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
            var frames = LpcSpriteFramesBuilder.Construir(item.SpritesheetEquipamento, NomeDaClasse);
            if (frames != null && frames.GetAnimationNames().Length > 0)
            {
                overlay.SpriteFrames = frames;
                overlay.Visible = true;
                SincronizarOverlays();
                return;
            }
        }

        if (TentarAplicarVisualArma(overlay, item))
            return;

        overlay.Visible = false;
        overlay.SpriteFrames = null;
    }

    private bool TentarAplicarVisualArma(AnimatedSprite2D overlay, ItemResource item)
    {
        if (overlay == null || item == null)
            return false;

        if (item.Tipo != TipoEquipamento.Arma)
            return false;

        var profile = EncontrarPerfilVisualArma(item);
        if (profile == null)
            return false;

        Texture2D baseSheet = CarregarTexturaPrimeiroExistente(profile.BasePath);
        Texture2D ataqueSheet = CarregarTexturaPrimeiroExistente(profile.AttackPath);
        if (baseSheet == null && ataqueSheet == null)
        {
            GD.PrintErr($"[APARENCIA] Nenhuma spritesheet encontrada para {profile.Nome}: {profile.BasePath} / {profile.AttackPath}");
            return false;
        }

        var frames = LpcSpriteFramesBuilder.ConstruirEquipamento(
            baseSheet,
            ataqueSheet,
            NomeDaClasse,
            profile.MovimentoRows,
            profile.MovimentoFrameSize,
            profile.AtaqueRows,
            profile.AtaqueFrameSize);

        if (frames == null || frames.GetAnimationNames().Length == 0)
            return false;

        overlay.SpriteFrames = frames;
        overlay.ZIndex = string.Equals(profile.Nome, "Arco", StringComparison.OrdinalIgnoreCase) ? -1 : 3;
        overlay.ZAsRelative = true;
        overlay.Visible = true;
        GD.Print($"[APARENCIA] {profile.Nome} aplicado no personagem: item={item.Nome} base={baseSheet?.ResourcePath ?? "null"} ataque={ataqueSheet?.ResourcePath ?? "null"} animacoes={frames.GetAnimationNames().Length}");
        SincronizarOverlays();
        return true;
    }

    private static WeaponVisualProfile EncontrarPerfilVisualArma(ItemResource item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Nome))
            return null;

        string nome = item.Nome.ToLowerInvariant();
        foreach (var profile in WeaponVisualProfiles)
        {
            foreach (string palavra in profile.PalavrasChave)
            {
                if (!string.IsNullOrWhiteSpace(palavra) && nome.Contains(palavra.ToLowerInvariant()))
                    return profile;
            }
        }

        return null;
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

        AtualizarOverlaySlot(equipamento, TipoEquipamento.Arma, _armaOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Peitoral, _armaduraOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Capacete, _capaceteOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Luvas, _luvasOverlay);
        AtualizarOverlaySlot(equipamento, TipoEquipamento.Botas, _botasOverlay);
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

        if (item.SpriteFramesEquipamento != null || item.SpritesheetEquipamento != null)
            return true;

        return item.Tipo == TipoEquipamento.Arma && EncontrarPerfilVisualArma(item) != null;
    }

    private void UpdateAnimation(Vector2 velocity)
    {
        if (AnimatedSprite == null) return;

        string currentAnim = AnimatedSprite.Animation.ToString();

        if (IsAttacking && currentAnim.Contains("attack"))
            return;

        if (velocity.Length() > 10.0f)
        {
            CurrentDirection = DirectionUtil.VectorToDirectionString(velocity);
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);

            if (IsSprinting)
            {
                AnimatedSprite.Play($"run_{cardinal}");
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
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);
            AnimatedSprite.Play($"idle_{cardinal}");
            AnimatedSprite.SpeedScale = 1.0f; 
        }
    }

    public virtual void Atacar()
    {
        if (AnimatedSprite == null) return;

        var targetFinder = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (TryGetSelectedTarget(targetFinder, out var targetNode, out _))
        {
            Vector2 dirToTarget = (targetNode.GlobalPosition - GlobalPosition).Normalized();
            if (dirToTarget.LengthSquared() > 0.001f)
                CurrentDirection = DirectionUtil.VectorToDirectionString(dirToTarget);
        }

        string animacaoDeAtaque = $"{NomeDaClasse}_attack_{CurrentDirection}";

        // Fallback para animação cardinal se a 8-dir não existir
        if (AnimatedSprite.SpriteFrames != null && !AnimatedSprite.SpriteFrames.HasAnimation(animacaoDeAtaque))
        {
            string cardinal = DirectionUtil.DirectionToCardinal(CurrentDirection);
            animacaoDeAtaque = $"{NomeDaClasse}_attack_{cardinal}";
        }

        if (AnimatedSprite.SpriteFrames != null && AnimatedSprite.SpriteFrames.HasAnimation(animacaoDeAtaque))
        {
            IsAttacking = true;
            _attackTimeoutCounter = 0f;
            AnimatedSprite.Play(animacaoDeAtaque);
            AnimatedSprite.SpeedScale = 2.0f;
            SincronizarOverlays();
            GD.Print($"[PLAYER] Iniciando ataque: '{animacaoDeAtaque}'");
        }
        else
        {
            GD.Print($"[PLAYER] Animação '{animacaoDeAtaque}' não encontrada. Atacando sem animação.");
        }

        // A animação também é uma ação online: servidor valida e replica aos demais jogadores.
        if (_network != null && _network.IsConnected)
            _network.SendPlayerAction(1, DirectionUtil.DirectionToVector(CurrentDirection));

        // Executa a lógica de ataque independente da animação
        if (string.Equals(NomeDaClasse, "mago", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NomeDaClasse, "prist", StringComparison.OrdinalIgnoreCase))
        {
            if (ProjetilScene != null)
                DispararProjetil();
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

        // Posiciona o projétil na posição do arco (ou do player, como fallback)
        Vector2 origem = _armaOverlay?.GlobalPosition ?? GlobalPosition;
        novoProjetil.GlobalPosition = origem + direcaoDoVetor * 50;
        GetParent().AddChild(novoProjetil);

        GD.Print($"[PLAYER] Projetil instanciado em {novoProjetil.GlobalPosition}, direcao: {direcaoDoVetor}");

        if (novoProjetil is Projetil proj)
        {
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
            else if (string.Equals(NomeDaClasse, "prist", StringComparison.OrdinalIgnoreCase))
            {
                proj.Speed = 280.0f;
                proj.DanoMin = equip?.DanoMagicoMin ?? 12;
                proj.DanoMax = equip?.DanoMagicoMax ?? 18;
                proj.EhDanoMagico = true;
            }
        }
        else
        {
            GD.PrintErr("[PLAYER] Erro: O projétil instanciado não é do tipo Projetil.");
        }

        // Envia pacote para replicar o projétil para outros jogadores
        if (gameNet != null && gameNet.IsConnected)
        {
            byte projType = string.Equals(NomeDaClasse, "mago", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(NomeDaClasse, "prist", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;
            gameNet.SendProjectileFire(novoProjetil.GlobalPosition.X, novoProjetil.GlobalPosition.Y, direcaoDoVetor.X, direcaoDoVetor.Y, projType);
        }
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
        distance = GlobalPosition.DistanceTo(node.GlobalPosition);
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

            float d = GlobalPosition.DistanceTo(kvp.Value.GlobalPosition);
            if (d < closestDist)
            {
                closestDist = d;
                closest = kvp.Key;
            }
        }

        return closest;
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
        GD.Print("[PLAYER] O Player foi derrotado!");
        IsAttacking = false;
        _isLyingDown = true;
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
                AnimatedSprite.AnimationFinished += AoTerminarMorte;
                SincronizarOverlays();
            }
            else
            {
                AnimatedSprite.Stop();
            }
        }

        var respawnUI = GetNodeOrNull<RespawnUI>("/root/main/HUD/RespawnUI");
        if (respawnUI != null)
            respawnUI.ShowDeathScreen();
    }

    private void AoTerminarMorte()
    {
        if (AnimatedSprite == null) return;
        string anim = AnimatedSprite.Animation.ToString();
        if (string.Equals(anim, "death", StringComparison.Ordinal))
        {
            AnimatedSprite.Stop();
            AnimatedSprite.AnimationFinished -= AoTerminarMorte;
        }
    }

    public void Reviver(float x, float y, int health, int maxHealth)
    {
        _isLyingDown = false;
        RemoveFromGroup("PlayersDowned");
        GlobalPosition = new Vector2(x, y);
        SetHealthFromServer(health, maxHealth);
        SetPhysicsProcess(true);
        SetProcess(true);

        var respawnUI = GetNodeOrNull<RespawnUI>("/root/main/HUD/RespawnUI");
        if (respawnUI != null)
            respawnUI.HideDeathScreen();

        GD.Print("[PLAYER] Reviveu!");
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
            GD.Print($"[PLAYER] ✅ Animação de ataque '{anim}' terminou. Liberando IsAttacking.");
            IsAttacking = false;
            _attackTimeoutCounter = 0f;
            AnimatedSprite.SpeedScale = 1.0f;
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
            _network.OnSceneChange -= OnSceneChangeReceived;
        }
    }
}
