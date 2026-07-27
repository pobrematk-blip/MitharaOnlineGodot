#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using Mithara.Network;

public partial class EntityManager : Node
{
    private const byte PlayerActionAttack = 1;
    private const byte PlayerActionBackJump = 2;
    private const byte PlayerActionSummonSherigan = 3;
    private const byte PlayerActionInvisibility = 4;
    private const byte PlayerActionReveal = 5;
    private const byte PlayerActionArcaneTeleportEnter = 6;
    private const byte PlayerActionArcaneTeleportExit = 7;
    private const byte PlayerActionGolpesFreneticos = 8;
    private const byte PlayerActionCarnificinaJump = 9;
    private const float RemoteDashTrailMinDistance = 56f;
    private const int RemoteDashTrailGhostCount = 5;
    private const float RemoteDashTrailDuration = 0.42f;
    private GameNetwork? _gameNet;
    private PackedScene? _inimigoScene;
    private readonly Dictionary<string, PackedScene?> _mobScenes = new();
    private readonly Dictionary<string, string> _mobNameToType = new()
    {
        ["goblin"] = "goblin",
        ["Goblin"] = "goblin",
        ["lobo"] = "lobo",
        ["Lobo"] = "lobo",
        ["wolf"] = "lobo",
        ["Wolf"] = "lobo",
        ["porco"] = "porco",
        ["Porco"] = "porco",
        ["minotauro"] = "minotauro",
        ["Minotauro"] = "minotauro",
        ["slime"] = "slime",
        ["Slime"] = "slime",
        ["slimeElite"] = "slimeElite",
        ["SlimeElite"] = "slimeElite",
        ["Slime Elite"] = "slimeElite",
        ["skeleton"] = "goblin",
        ["Esqueleto"] = "goblin",
        ["Demon Lord"] = "minotauro",
        ["boss_demon"] = "minotauro",
        ["slimeBoss"] = "slimeBoss",
        ["Slime Boss"] = "slimeBoss",
        ["plantaCarnivora"] = "plantaCarnivora",
        ["PlantaCarnivora"] = "plantaCarnivora",
        ["Planta Carnivora"] = "plantaCarnivora",
        ["plantaCarnivoraElite"] = "plantaCarnivoraElite",
        ["PlantaCarnivoraElite"] = "plantaCarnivoraElite",
        ["Planta Carnivora Elite"] = "plantaCarnivoraElite",
        ["cogumelo"] = "cogumelo",
        ["Cogumelo"] = "cogumelo",
        ["cogumeloElite"] = "cogumeloElite",
        ["CogumeloElite"] = "cogumeloElite",
        ["Cogumelo Elite"] = "cogumeloElite",
    };
    private readonly Dictionary<ulong, Node2D> _networkNodes = new();
    private readonly Dictionary<ulong, Node2D> _lootNodes = new();
    private readonly Dictionary<ulong, Node2D> _lojinhaNodes = new();
    private readonly Dictionary<ulong, Node2D> _remoteSheriganPets = new();
    private readonly Dictionary<ulong, AnimatedSprite2D> _remotePlayerSprites = new();
    private readonly Dictionary<string, AnimatedSprite2D> _bastiaoAreaEffects = new();
    private static readonly Dictionary<string, SpriteFrames?> _directoryFrameCache = new();
    private ulong _lojinhaInteracaoAtual;
    private Node2D? _worldNode;
    private static readonly Color NomeCorNormal = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Color NomeCorParty = new(0.15f, 0.95f, 1.0f, 1.0f);
    private static readonly Color NomeCorGuild = new(0.25f, 1.0f, 0.18f, 1.0f);
    private static readonly Color NomeCorFaccaoInimiga = new(1.0f, 0.12f, 0.08f, 1.0f);

    private static readonly Dictionary<Raridade, Color> RarityColors = new()
    {
        [Raridade.Comum] = Color.FromHtml("#ffffff"),
        [Raridade.Incomum] = Color.FromHtml("#1eff00"),
        [Raridade.Raro] = Color.FromHtml("#0070dd"),
        [Raridade.Epico] = Color.FromHtml("#a335ee"),
        [Raridade.Lendario] = Color.FromHtml("#ffcc00"),
        [Raridade.Mistico] = Color.FromHtml("#ff4444"),
    };

    private struct RemoteState
    {
        public Vector2 Position;
        public Vector2 Direction;
        public bool Moving;
        public bool Sprinting;
        public byte AIState;
        public double Timestamp;
    }

    private readonly Dictionary<ulong, RemoteState> _remoteStates = new();
    private readonly Dictionary<ulong, Vector2> _lastDirections = new();
    private readonly Dictionary<ulong, Vector2> _previousPositions = new();
    private readonly Dictionary<ulong, double> _lastDashTrailAt = new();
    private readonly Dictionary<string, double> _lastCasterSkillEffectAt = new();
    private const string MetaMachadoGiratorioUntil = "machado_giratorio_until";
    private const double InterpolationDelay = 0.08;
    private const int PendingMonsterSpawnBatchSize = 8;
    private const float VisibilityCullMargin = 320f;
    private const double VisibilityCullInterval = 0.15;
    private const string MetaRace = "race";
    private const string RemotePaperdollPrefix = "RemotePaperdoll_";
    private double _visibilityCullAccumulator;
    private Node2D? ObterMundo()
    {
        if (_worldNode == null || !IsInstanceValid(_worldNode))
        {
            var tree = GetTree();
            if (tree == null)
            {
                GameNetwork.LogError("ObterMundo: GetTree() retornou null");
                return null;
            }
            var root = tree.Root;
            if (root == null)
            {
                GameNetwork.LogError("ObterMundo: Root e null");
                return null;
            }

            // 1) Try from CurrentScene directly (most reliable)
            var current = tree.CurrentScene;
            if (current != null)
            {
                _worldNode = current.GetNodeOrNull<Node2D>("World");
                if (_worldNode != null)
                {
                    PrepararWorldParaYSort(_worldNode);
                    GameNetwork.Log($"ObterMundo: encontrado via CurrentScene/{current.Name}");
                    return _worldNode;
                }
            }

            // 2) Try common paths under Root
            string[] paths = new[] { "main/World", "Main/World", "world/World" };
            foreach (var p in paths)
            {
                _worldNode = root.GetNodeOrNull<Node2D>(p);
                if (_worldNode != null)
                {
                    PrepararWorldParaYSort(_worldNode);
                    GameNetwork.Log($"ObterMundo: encontrado via Root/{p}");
                    return _worldNode;
                }
            }

            // 3) Deep search all root children
            for (int i = 0; i < root.GetChildCount(); i++)
            {
                var child = root.GetChild(i);
                _worldNode = child.FindChild("World", true, false) as Node2D;
                if (_worldNode != null)
                {
                    PrepararWorldParaYSort(_worldNode);
                    GameNetwork.Log($"ObterMundo: encontrado via FindChild em {child.Name}");
                    return _worldNode;
                }
            }

            if (_worldNode == null)
            {
                int rootChildCount = root.GetChildCount();
                GameNetwork.Log($"ObterMundo: aguardando cena com World (Root tem {rootChildCount} filhos)");
            }
        }
        return _worldNode;
    }

    private static void PrepararWorldParaYSort(Node2D world)
    {
        world.YSortEnabled = true;
        world.ZIndex = 0;
        world.ZAsRelative = true;
    }

    private Vector2 ParaPosicaoVisual(Vector2 posicaoServidor)
    {
        var currentScene = GetTree()?.CurrentScene;
        var world = currentScene?.FindChild("World", true, false) as Node2D;
        var interiores = world?.GetNodeOrNull<Node2D>("Interiores");
        if (interiores != null && interiores.GetChildCount() > 0)
            return posicaoServidor + interiores.GlobalPosition;

        return posicaoServidor;
    }

    private Vector2 ParaPosicaoVisual(float x, float y)
        => ParaPosicaoVisual(new Vector2(x, y));

    private static void PrepararEntidadeYSort(Node2D node)
    {
        node.ZIndex = 1;
        node.ZAsRelative = true;
        node.YSortEnabled = false;
    }

    private const string MetaAnimPrefix = "anim_prefix";
    private const string MetaCharacterClass = "character_class";
    private const string MetaSpritePath = "sprite_path";
    private const string ArcaneShieldEffectNodeName = "ArcaneShieldLoopEffect";
    private const string EscudoProtetorEffectPath = "res://skills/Animacao/AA_S0_42_ProtectEXE_20FPS.png";
    private const string FlechaImpactoEffectPath = "res://skills/Efeitos/Arqueiro/FlechaImpactoEffect.tscn";
    private const string MagoBasicHitEffectPath = "res://skills/Efeitos/Mago/MagoBasicHitEffect.tscn";
    private const string RaioEstaticoEffectPath = "res://skills/Efeitos/Mago/RaioEstaticoEffect.tscn";
    private const string LancaGeloEffectPath = "res://skills/Efeitos/Mago/LancaGeloEffect.tscn";
    private const string TornadoEffectPath = "res://skills/Efeitos/Mago/TornadoEffect.tscn";
    private const string TempestadeEletricaEffectPath = "res://skills/Efeitos/Mago/TempestadeEletricaEffect.tscn";
    private const string ExplosaoVulcanicaEffectPath = "res://skills/Efeitos/Mago/ExplosaoVulcanicaEffect.tscn";
    private const string ChuvaMeteorosEffectPath = "res://skills/Efeitos/Mago/ChuvaMeteorosEffect.tscn";
    private const string SonoArcanoEffectPath = "res://skills/Efeitos/Mago/SonoArcanoEffect.tscn";
    private const string PrisaoGeloEffectPath = "res://skills/Efeitos/Mago/PrisaoGeloEffect.tscn";
    private const string TeleporteArcanoPortalEffectPath = "res://skills/Efeitos/Mago/TeleporteArcanoPortalEffect.tscn";
    private const string EscudoArcanoEffectPath = "res://skills/Efeitos/Mago/EscudoArcanoEffect.tscn";
    private const string FuriaElementalEffectPath = "res://skills/Efeitos/Mago/FuriaElementalEffect.tscn";
    private const string TiroParalisanteEffectPath = "res://skills/Efeitos/Arqueiro/TiroParalisanteEffect.tscn";
    private const string TiroExecucaoEffectPath = "res://skills/Efeitos/Arqueiro/TiroExecucaoEffect.tscn";
    private const string MarcaExecutorPoisonEffectPath = "res://skills/Efeitos/Arqueiro/MarcaExecutorPoisonEffect.tscn";
    private const string GolpeSombrioEffectDir = "res://skills/Animacao/50 Animated Effects v5/50 Animated Effects v5/8";
    private const string ExecucaoFinalEffectPath = "res://skills/Animacao/175.png";
    private const string PunhaladaNasCostasEffectPath = "res://skills/Animacao/1_100x100px.png";
    private const string GolpeAtordoanteEffectDir = "res://skills/Animacao/70 Animated Effects v12/70 Animated Effects v12/60";
    private const string SequenciaMortalEffectDir = "res://skills/Animacao/50 Animated Effects v3/50 Animated Effects v3/29";
    private const string ReflexosAssassinosEffectPath = "res://skills/Animacao/AA_S0_14_Dark_20FPS.png";
    private const string BossSlimeSlowEffectPath = "res://skills/Efeitos/Bosses/SlimeSlowEffect.tscn";
    private const string FuriaBerserkerEffectPath = "res://skills/Efeitos/Berserker/FuriaEffect.tscn";
    private const string FrenesiBerserkerEffectPath = "res://skills/Efeitos/Berserker/FrenesiEffect.tscn";
    private const string DeusDaGuerraEffectPath = "res://skills/Efeitos/Berserker/DeusDaGuerraEffect.tscn";
    private const string SangueDeFerroEffectPath = "res://skills/Efeitos/Berserker/SangueDeFerroEffect.tscn";
    private const string InvestidaBrutalImpactEffectPath = "res://skills/Efeitos/Berserker/InvestidaBrutalImpact.tscn";
    private const string SedeDeSangueEffectPath = "res://skills/Efeitos/Berserker/SedeDeSangueEffect.tscn";
    private const string ForcaBrutalEffectPath = "res://skills/Efeitos/Berserker/ForcaBrutalEffect.tscn";
    private const string GolpesFreneticosEffectPath = "res://skills/Efeitos/Berserker/GolpesFreneticosEffect.tscn";
    private const string MachadoGiratorioEffectPath = "res://skills/Efeitos/Berserker/MachadoGiratorioEffect.tscn";
    private const string SangramentoMortalEffectPath = "res://skills/Animacao/193.png";
    private const string SangramentoMortalProjectilePath = "res://skills/Animacao/Axe_03.png";
    private const string BerserkEffectDir = "res://skills/Animacao/50 Animated Effects v5/50 Animated Effects v5/19";
    private const string CuraMenorEffectPath = "res://skills/Animacao/72.png";
    private const string ClerigoBasicHitEffectPath = "res://skills/Animacao/strike2.png";
    private const string CuraEmAreaEffectPath = "res://skills/Animacao/Recover1_a.png";
    private const string RaioSagradoEffectPath = "res://skills/Animacao/155.png";
    private const string MarteloDesolacaoEffectPath = "res://skills/Animacao/sprite-sheete.png";
    private const string ChoqueEstaticoEffectPath = "res://skills/Animacao/4.png";
    private const string MilagreDivinoEffectPath = "res://skills/Animacao/AA_S0_01_Cast_20FPS.png";
    private const string CorteRapidoEffectPath = "res://skills/Animacao/8a.png";
    private const string EstocadaEffectDir = "res://skills/Animacao/70 Animated Effects v12/70 Animated Effects v12/34";
    private const string EspadaEstelarEffectPath = "res://skills/Animacao/Tormenta Hitted Projectile.png";
    private const string GolpeDeEscudoImpactEffectPath = "res://skills/Animacao/AA_S0_15_Stone_20FPS.png";
    private const string DesafioCasterEffectDir = "res://skills/Animacao/50 Animated Effects v9/50 Animated Effects v9/20";
    private const string DesafioMobEffectDir = "res://skills/Animacao/50 Animated Effects v5/50 Animated Effects v5/6";
    private const string EspinhosEffectPath = "res://skills/Animacao/spikes.png";
    private const string FortificacaoEffectPath = "res://skills/Animacao/21.png";
    private const string MuralhaInabalavelEffectPath = "res://skills/Animacao/82.png";
    private const string BastiaoEffectPath = "res://skills/Animacao/AA_S0_34_DeProtect_20FPS.png";
    private const string LuzRestauradoraEffectPath = "res://skills/Animacao/AA_B1_02_GoldAura_20FPS.png";
    private const string RenovacaoEffectPath = "res://skills/Animacao/Flechas Comuns3.png";
    private const string RessurreicaoEffectPath = "res://skills/Animacao/237.png";
    private const string PurificacaoEffectPath = "res://skills/Animacao/100422.png";
    private const string BencaoSagradaEffectDir = "res://skills/Animacao/70 Animated Effects v12/70 Animated Effects v12/28";
    private const string CarnificinaImpactEffectDir = "res://skills/Animacao/50 Animated Effects v4/50 Animated Effects v4/45";
    private const string CarnificinaStunEffectDir = "res://skills/Animacao/50 Animated Effects v4/50 Animated Effects v4/4";
    private const string SheriganScenePath = "res://characters/Inimigos/SpriteInimigo/Sherigan.tscn";

    private bool _sceneReady;
    private bool _isExitingTree;
    private readonly List<SpawnEvent> _pendingSpawns = new();
    private int _flushRetryCount;
    private int _serverDataApplyRetryCount;

    private struct SpawnEvent
    {
        public ulong EntityId;
        public string EntityType;
        public string Name;
        public float X, Y;
        public int Level, Health, MaxHealth;
        public string Extra1, Extra2, Extra3;
    }

    private CanvasLayer? _uiOverlay;
    private PlayerContextMenu? _contextMenu;
    private InvitePopupUI? _invitePopup;
    private BossHPBar? _bossHPBar;

    public override void _Ready()
    {
        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet == null) return;

        _gameNet.OnEntitySpawned += OnEntitySpawned;
        _gameNet.OnEnterWorld += OnEnterWorldHandler;
        _gameNet.OnCombatResult += OnCombatResult;
        _gameNet.OnEntityDied += OnEntityDied;
        _gameNet.OnGainExp += OnGainExp;
        _gameNet.OnLevelUp += OnLevelUp;
		_gameNet.OnEntityHealthUpdate += OnEntityHealthUpdateHandler;
		_gameNet.OnEntityManaUpdate += OnEntityManaUpdateHandler;
		_gameNet.OnRespawn += OnRespawnHandler;
        _gameNet.OnLootSpawn += OnLootSpawn;
        _gameNet.OnLootDespawn += OnLootDespawn;
        _gameNet.OnLojinhaSpawn += OnLojinhaSpawn;
        _gameNet.OnLojinhaDespawn += OnLojinhaDespawn;
        _gameNet.OnStatUpdate += OnStatUpdate;
        _gameNet.OnProjectileSpawn += OnProjectileSpawn;
        _gameNet.OnItemUseResult += OnItemUseResult;
        _gameNet.OnSkillUseResult += OnSkillUseResult;
        _gameNet.OnPartyData += OnPartyDataChanged;
        _gameNet.OnPartyMemberUpdate += OnPartyMemberChanged;
        _gameNet.OnGuildData += OnGuildDataChanged;
        _gameNet.OnGuildMemberUpdate += OnGuildMemberChanged;
        _gameNet.OnGuildCleared += OnGuildClearedHandler;
        _gameNet.OnStatusEffect += OnStatusEffect;
        _gameNet.OnShieldUpdate += OnShieldUpdate;
        _gameNet.OnBossCast += OnBossCast;
        _gameNet.OnSkillAreaEffect += OnSkillAreaEffect;
        _gameNet.OnSkillVisualEffect += OnSkillVisualEffect;
        _gameNet.OnEquipmentVisualUpdate += OnEquipmentVisualUpdate;

        CriarUI();
        CriarNavegacaoMundo();
    }

    private void CriarNavegacaoMundo()
    {
        var world = ObterMundo();
        if (world == null) return;

        if (world.FindChild("NavigationRegion2D", true, false) != null)
            return;

        var navRegion = new NavigationRegion2D();
        navRegion.Name = "NavigationRegion2D";
        var navPoly = new NavigationPolygon();
        var outline = new Vector2[]
        {
            new(-10000f, -10000f),
            new(10000f, -10000f),
            new(10000f, 10000f),
            new(-10000f, 10000f),
        };
#pragma warning disable CS0618
        navPoly.AddOutline(outline);
        navPoly.MakePolygonsFromOutlines();
#pragma warning restore CS0618
        navRegion.NavigationPolygon = navPoly;
        world.AddChild(navRegion);
        GameNetwork.Log($"[NAV] NavigationRegion2D criada no mundo");
    }

    private void CriarUI()
    {
        _uiOverlay = new CanvasLayer { Layer = 100, Name = "EntityManagerUI" };
        AddChild(_uiOverlay);

        _contextMenu = new PlayerContextMenu();
        _contextMenu.Name = "PlayerContextMenu";
        _uiOverlay.AddChild(_contextMenu);

        _invitePopup = new InvitePopupUI();
        _invitePopup.Name = "InvitePopupUI";
        _uiOverlay.AddChild(_invitePopup);

        _bossHPBar = GetTree()?.Root?.FindChild("BossHPBar", true, false) as BossHPBar;
        if (_bossHPBar == null || !IsInstanceValid(_bossHPBar))
        {
            _bossHPBar = new BossHPBar();
            _bossHPBar.Name = "BossHPBar";
            _uiOverlay.AddChild(_bossHPBar);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F)
        {
            if (_lojinhaInteracaoAtual != 0)
            {
                _gameNet?.SendLojinhaOpen(_lojinhaInteracaoAtual);
                GetViewport()?.SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                PlayerContextMenu.HideMenu();
                return;
            }
            if (mb.ButtonIndex == MouseButton.Right)
            {
                PlayerContextMenu.HideMenu();
                RaycastRemotePlayer(mb.GlobalPosition);
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    private void RaycastRemotePlayer(Vector2 screenPos)
    {
        var world = ObterMundo();
        if (world == null || _gameNet == null) return;

        Vector2 worldPos;
        var cam = world.GetViewport()?.GetCamera2D();
        if (cam is Camera2D camera2D)
        {
            var vpSize = world.GetViewport()!.GetVisibleRect().Size;
            worldPos = camera2D.GetScreenCenterPosition() + (screenPos - vpSize / 2) / camera2D.Zoom;
        }
        else
            worldPos = screenPos;

        Node2D? closest = null;
        float closestDist = float.MaxValue;

        foreach (var kvp in _networkNodes)
        {
            if (kvp.Value == null || !IsInstanceValid(kvp.Value)) continue;
            if (kvp.Key == _gameNet.LocalPlayerId) continue;
            if (!kvp.Value.HasMeta("player_name")) continue;

            float dist = kvp.Value.GlobalPosition.DistanceTo(worldPos);
            if (dist < 30f && dist < closestDist)
            {
                closestDist = dist;
                closest = kvp.Value;
            }
        }

        if (closest != null)
        {
            string pName = (string)closest.GetMeta("player_name");
            ulong pId = (ulong)closest.GetMeta("network_id");
            PlayerContextMenu.ShowAt(pId, pName, screenPos);
        }
    }

    private void OnEnterWorldHandler()
    {
        if (!IsInsideTree() || _isExitingTree)
            return;

        _flushRetryCount = 0;
        _serverDataApplyRetryCount = 0;
        _sceneReady = false;
        _worldNode = null;
        try
        {
            CarregarCenasMob();
        }
        catch (System.Exception ex)
        {
            GameNetwork.Log($"OnEnterWorldHandler: CarregarCenasMob falhou: {ex.Message}");
        }
        CallDeferred(nameof(FlushPendingSpawns));
        CallDeferred(nameof(ApplyServerDataAfterEnterWorld));
    }

    private void ApplyServerDataAfterEnterWorld()
    {
        if (!IsInsideTree() || _isExitingTree)
            return;

        if (_gameNet == null) return;
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null)
        {
            GameNetwork.Log("ApplyServerDataAfterEnterWorld: Player n?o encontrado na cena");
            RetryApplyServerDataAfterEnterWorld();
            return;
        }

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip != null)
        {
            equip.ImportarEstado(
                _gameNet._pendingBaseForca,
                _gameNet._pendingBaseAgilidade,
                _gameNet._pendingBaseDestreza,
                _gameNet._pendingBaseInteligencia,
                _gameNet._pendingStatPoints,
                _gameNet._pendingBaseVitalidade,
                _gameNet._pendingBaseSorte);
        }

        var prog = player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
        if (prog != null)
        {
            int lvl = _gameNet._pendingLevel;
            int xp = (int)_gameNet._pendingXp;
            GameNetwork.Log($"ApplyServerDataAfterEnterWorld: setting level={lvl} xp={xp}");
            prog.DefinirProgresso(lvl, xp);
        }
        else
        {
            GameNetwork.Log("ApplyServerDataAfterEnterWorld: LevelProgressionComponent n?o encontrado!");
            RetryApplyServerDataAfterEnterWorld();
        }

        _gameNet.ApplyPendingInventory();
    }

    private void RetryApplyServerDataAfterEnterWorld()
    {
        if (!IsInsideTree() || _isExitingTree)
            return;

        if (_serverDataApplyRetryCount++ >= 20)
            return;

        CallDeferred(nameof(ApplyServerDataAfterEnterWorld));
    }

    private void FlushPendingSpawns()
    {
        if (!IsInsideTree() || _isExitingTree)
            return;

        if (_flushRetryCount >= 10)
        {
            GameNetwork.LogError("FlushPendingSpawns: limite de retentativas atingido, abandonando");
            return;
        }
        try
        {
            var world = ObterMundo();

            if (world == null)
            {
                _flushRetryCount++;
                if (_flushRetryCount < 120)
                    CallDeferred(nameof(FlushPendingSpawns));
                else
                    GameNetwork.LogError("FlushPendingSpawns: World real n?o encontrado ap?s esperar a Main.tscn");
                return;
            }

            if (GetTree()?.CurrentScene?.FindChild("Player", true, false) == null)
            {
                _flushRetryCount++;
                CallDeferred(nameof(FlushPendingSpawns));
                return;
            }

            _sceneReady = true;

            int playerCount = 0, monsterCount = 0, npcCount = 0;
            foreach (var s in _pendingSpawns)
            {
                if (s.EntityType == "player") playerCount++;
                else if (s.EntityType == "monster" || s.EntityType == "boss") monsterCount++;
                else if (s.EntityType == "npc") npcCount++;
            }
            GameNetwork.Log($"FlushPendingSpawns: {_pendingSpawns.Count} spawns ({playerCount} players, {monsterCount} monsters, {npcCount} npcs)");

            // Ativar HUD - busca robusta
            var hud = GetNodeOrNull<CanvasLayer>("/root/main/HUD");
            if (hud == null)
            {
                var root = GetTree()?.Root;
                if (root != null)
                {
                    for (int i = 0; i < root.GetChildCount(); i++)
                    {
                        var h = root.GetChild(i).FindChild("HUD", true, false) as CanvasLayer;
                        if (h != null)
                        {
                            hud = h;
                            break;
                        }
                    }
                }
            }

            if (hud != null)
            {
                hud.Visible = true;
                GameNetwork.Log("HUD ativado de FlushPendingSpawns");
            }
            else
            {
                GameNetwork.Log("HUD n?o encontrado");
            }

            if (_gameNet != null)
            {
                var spawnPos = _gameNet.PendingPlayerSpawn;
                GameNetwork.Log($"PendingPlayerSpawn = {spawnPos}");
                if (spawnPos != Vector2.Zero)
                {
                    var player = world?.GetNodeOrNull<Node2D>("Player") as Player;
                    if (player != null)
                    {
                        player.Position = spawnPos;
                        GameNetwork.Log($"Player posicionado em {spawnPos}");
                    }
                    else
                        GameNetwork.Log("Player n?o encontrado no World para posicionar");
                }
            }

            _gameNet?.ApplyPendingInventory();

            for (int i = _pendingSpawns.Count - 1; i >= 0; i--)
            {
                var s = _pendingSpawns[i];
                if (s.EntityType == "monster" || s.EntityType == "boss")
                    continue;

                ProcessSpawn(s.EntityId, s.EntityType, s.Name, s.X, s.Y, s.Level, s.Health, s.MaxHealth, s.Extra1, s.Extra2, s.Extra3);
                _pendingSpawns.RemoveAt(i);
            }

            // Fechar tela de carregamento
            var loading = GetTree()?.Root.GetNodeOrNull("LoadingScreen");
            if (loading != null)
                loading.QueueFree();

            if (_pendingSpawns.Count > 0)
                CallDeferred(nameof(ProcessPendingSpawnBatch));
        }
        catch (System.Exception ex)
        {
            GameNetwork.LogError($"Erro em FlushPendingSpawns", ex.ToString());
            // Limita retentativas para evitar loop infinito
            _flushRetryCount++;
            if (_flushRetryCount < 10)
                CallDeferred(nameof(FlushPendingSpawns));
            else
                GameNetwork.LogError("FlushPendingSpawns: limite de retentativas atingido");
        }
    }

    private void ProcessPendingSpawnBatch()
    {
        if (!IsInsideTree() || _isExitingTree)
            return;

        int processed = 0;
        while (_pendingSpawns.Count > 0 && processed < PendingMonsterSpawnBatchSize)
        {
            var s = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);
            ProcessSpawn(s.EntityId, s.EntityType, s.Name, s.X, s.Y, s.Level, s.Health, s.MaxHealth, s.Extra1, s.Extra2, s.Extra3);
            processed++;
        }

        if (_pendingSpawns.Count > 0)
            CallDeferred(nameof(ProcessPendingSpawnBatch));
    }

    private void OnEntitySpawned(ulong entityId, string entityType, string name, float x, float y, int level, int health, int maxHealth, string extraData1, string extraData2, string extraData3)
    {
        if (_networkNodes.ContainsKey(entityId))
        {
            return;
        }

        int pendingIndex = _pendingSpawns.FindIndex(spawn => spawn.EntityId == entityId);
        if (pendingIndex >= 0)
        {
            _pendingSpawns[pendingIndex] = new SpawnEvent
            {
                EntityId = entityId, EntityType = entityType, Name = name,
                X = x, Y = y, Level = level, Health = health, MaxHealth = maxHealth,
                Extra1 = extraData1, Extra2 = extraData2, Extra3 = extraData3,
            };
            return;
        }

        if (!_sceneReady)
        {
            _pendingSpawns.Add(new SpawnEvent
            {
                EntityId = entityId, EntityType = entityType, Name = name,
                X = x, Y = y, Level = level, Health = health, MaxHealth = maxHealth,
                Extra1 = extraData1, Extra2 = extraData2, Extra3 = extraData3,
            });
            return;
        }

        try
        {
            ProcessSpawn(entityId, entityType, name, x, y, level, health, maxHealth, extraData1, extraData2, extraData3);
        }
        catch (System.Exception ex)
        {
            GameNetwork.LogError($"OnEntitySpawned falhou para {entityType} '{name}' ({entityId})", ex.ToString());
        }
    }

    private void ProcessSpawn(ulong entityId, string entityType, string name, float x, float y, int level, int health, int maxHealth, string extraData1, string extraData2, string extraData3)
    {
        if (_networkNodes.ContainsKey(entityId))
            return;

        Node2D? node = entityType switch
        {
            "player" => CreatePlayerEntity(entityId, name, x, y, level, health, maxHealth, extraData1, extraData2, extraData3),
            "monster" or "boss" => CreateMonsterEntity(entityId, name, x, y, level, health, maxHealth, extraData1, entityType == "boss" || EhPrefabBoss(extraData1, name)),
            "npc" => CreateNpcEntity(entityId, name, x, y, extraData1, extraData2, extraData3),
            "pet" => CreatePetEntity(entityId, name, x, y, level, health, maxHealth, extraData1, extraData2, extraData3),
            _ => null,
        };

        if (node != null && _gameNet != null)
        {
            _networkNodes[entityId] = node;
            _gameNet.RegisterEntity(entityId, node);
        }
    }

    private Node2D CreatePlayerEntity(ulong entityId, string name, float x, float y, int level, int health, int maxHealth, string characterClass, string race, string overheadData)
    {
        if (entityId == _gameNet?.LocalPlayerId)
        {
            return null!;
        }

        var root = new Node2D();
        root.Position = ParaPosicaoVisual(x, y);
        root.Name = $"Player_{entityId}";
        PrepararEntidadeYSort(root);
        root.SetMeta("network_id", entityId);
        root.SetMeta("player_name", name);
        root.SetMeta(MetaCharacterClass, characterClass);
        root.SetMeta(MetaRace, race);
        root.AddToGroup("RemotePlayers");

        string animPrefix = ClasseRegistry.ObterPrefixoAtaqueRecomendado(characterClass);
        root.SetMeta(MetaAnimPrefix, animPrefix);

        var sprite = new AnimatedSprite2D();
        sprite.Name = "AnimatedSprite";
        sprite.Scale = Vector2.One * 2f;

        var resolvedFrames = Player.CriarSpriteFramesParaRacaClasse(race, characterClass, out string sheetPath, out string perfilVisual);
        root.SetMeta(MetaSpritePath, sheetPath);

        if (resolvedFrames != null)
        {
            sprite.SpriteFrames = resolvedFrames;
            sprite.Play("idle_down");
        }
        else if (!string.IsNullOrWhiteSpace(sheetPath) && ResourceLoader.Exists(sheetPath))
        {
            var sheet = ResourceLoader.Load<Texture2D>(sheetPath);
            if (sheet != null)
            {
                var frames = LpcSpriteFramesBuilder.Construir(sheet, animPrefix);
                if (frames != null && frames.GetAnimationNames().Length > 0)
                {
                    sprite.SpriteFrames = frames;
                    sprite.Play("idle_down");
                }
            }
        }

        root.AddChild(sprite);
        _remotePlayerSprites[entityId] = sprite;

        long xp = 0;
        long xpMax = 1;
        string guildName = "";
        string guildTag = "";
        int guildEmblem = -1;
        string factionId = "";
        Godot.Collections.Array<Godot.Collections.Dictionary> equipment = new();
        string cabeloPath = "";
        string barbaPath = "";
        Color cabeloCor = Colors.White;
        Color barbaCor = Colors.White;
        if (!string.IsNullOrWhiteSpace(overheadData))
        {
            var data = Json.ParseString(overheadData).AsGodotDictionary();
            factionId = (string)data.GetValueOrDefault("faction_id", "");
            xp = (long)data.GetValueOrDefault("xp", 0L);
            xpMax = (long)data.GetValueOrDefault("xp_max", 1L);
            guildName = (string)data.GetValueOrDefault("guild_name", "");
            guildTag = (string)data.GetValueOrDefault("guild_tag", "");
            guildEmblem = (int)data.GetValueOrDefault("guild_emblem", -1);
            if (data.TryGetValue("equipment", out var equipmentValue)
                && equipmentValue.VariantType == Variant.Type.Array)
            {
                foreach (Variant entryValue in equipmentValue.AsGodotArray())
                {
                    if (entryValue.VariantType == Variant.Type.Dictionary)
                        equipment.Add(entryValue.AsGodotDictionary());
                }
            }
            cabeloPath = (string)data.GetValueOrDefault("cabelo_path", "");
            barbaPath = (string)data.GetValueOrDefault("barba_path", "");
            cabeloCor = ParseColorHtml((string)data.GetValueOrDefault("cabelo_cor", "ffffff"));
            barbaCor = ParseColorHtml((string)data.GetValueOrDefault("barba_cor", "ffffff"));
        }
        root.SetMeta("faction_id", factionId);
        root.SetMeta("guild_name", guildName);

        var overhead = new OverheadUI
        {
            Name = "OverheadUI_Remoto",
            Position = new Vector2(-60, -88),
            ZIndex = 10,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overhead.ConfigurarRemoto(name, guildName, guildTag, guildEmblem, xp, xpMax);
        overhead.AtualizarStatusRemoto(health, maxHealth, 1, 1);
        overhead.DefinirCorNome(CalcularCorNomeRemoto(entityId, factionId, guildName));
        root.AddChild(overhead);

        var parent = ObterMundo();
        if (parent != null)
        {
            parent.AddChild(root);
        }
        else
        {
            AddChild(root);
        }

        if (health <= 0)
            SetRemotePlayerDowned(root);

        AplicarEquipamentoVisualRemoto(entityId, equipment);
        AplicarAparenciaVisualRemota(root, sprite, cabeloPath, cabeloCor, "Cabelo");
        AplicarAparenciaVisualRemota(root, sprite, barbaPath, barbaCor, "Barba");

        return root;
    }

    private void OnEquipmentVisualUpdate(ulong entityId, Godot.Collections.Array<Godot.Collections.Dictionary> equipment)
    {
        AplicarEquipamentoVisualRemoto(entityId, equipment);
    }

    private void AplicarEquipamentoVisualRemoto(ulong entityId, Godot.Collections.Array<Godot.Collections.Dictionary> equipment)
    {
        if (entityId == _gameNet?.LocalPlayerId)
            return;
        if (!_networkNodes.TryGetValue(entityId, out var node) || node == null || !IsInstanceValid(node))
            return;
        if (!node.IsInGroup("RemotePlayers"))
            return;

        var sprite = node.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite");
        if (sprite == null || _gameNet?.ItemDB == null)
            return;

        var equipped = new Dictionary<int, ItemResource>();
        foreach (var entry in equipment)
        {
            int slot = ObterInt(entry, "slot");
            int itemId = ObterInt(entry, "item_id");
            if (slot <= 0 || itemId <= 0)
                continue;

            var item = _gameNet.ItemDB.GetItem(itemId);
            if (item != null)
                equipped[slot] = item;
        }

        string classe = ObterMetaString(node, MetaCharacterClass);
        string raca = ObterMetaString(node, MetaRace);
        equipped.TryGetValue((int)TipoEquipamento.Arma, out var arma);
        equipped.TryGetValue((int)TipoEquipamento.Escudo, out var escudo);

        string animAtual = sprite.Animation.ToString();
        int frameAtual = sprite.Frame;
        float speedAtual = sprite.SpeedScale;
        var corpoFrames = Player.CriarSpriteFramesCorpoEquipadoParaVisual(arma, escudo, raca, classe, out string sheetPath, out _);
        if (corpoFrames != null && corpoFrames.GetAnimationNames().Length > 0)
        {
            sprite.SpriteFrames = corpoFrames;
            if (!string.IsNullOrWhiteSpace(sheetPath))
                node.SetMeta(MetaSpritePath, sheetPath);
            RestaurarAnimacaoSprite(sprite, animAtual, frameAtual, speedAtual);
        }

        AtualizarOverlayRemoto(node, sprite, equipped, TipoEquipamento.Capacete);
        AtualizarOverlayRemoto(node, sprite, equipped, TipoEquipamento.Peitoral);
        AtualizarOverlayRemoto(node, sprite, equipped, TipoEquipamento.Luvas);
        AtualizarOverlayRemoto(node, sprite, equipped, TipoEquipamento.Calca);
        AtualizarOverlayRemoto(node, sprite, equipped, TipoEquipamento.Botas);
        SincronizarOverlaysRemotos(node, sprite);
    }

    private static int ObterInt(Godot.Collections.Dictionary dict, string key)
    {
        if (!dict.TryGetValue(key, out var value))
            return 0;

        return value.VariantType switch
        {
            Variant.Type.Int => (int)value.AsInt64(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed) ? parsed : 0,
            _ => 0,
        };
    }

    private static void RestaurarAnimacaoSprite(AnimatedSprite2D sprite, string anim, int frame, float speed)
    {
        if (sprite.SpriteFrames == null)
            return;

        if (!string.IsNullOrWhiteSpace(anim) && sprite.SpriteFrames.HasAnimation(anim))
        {
            sprite.Play(anim);
            int frameCount = sprite.SpriteFrames.GetFrameCount(anim);
            if (frameCount > 0)
                sprite.Frame = Mathf.Clamp(frame, 0, frameCount - 1);
            sprite.SpeedScale = speed;
            return;
        }

        if (sprite.SpriteFrames.HasAnimation("idle_down"))
            sprite.Play("idle_down");
    }

    private void AtualizarOverlayRemoto(Node2D node, AnimatedSprite2D baseSprite, Dictionary<int, ItemResource> equipped, TipoEquipamento tipo)
    {
        string nodeName = $"{RemotePaperdollPrefix}{(int)tipo}";
        var overlay = node.GetNodeOrNull<AnimatedSprite2D>(nodeName);
        if (!equipped.TryGetValue((int)tipo, out var item) || !DeveAplicarOverlayEquipamentoRemoto(item))
        {
            if (overlay != null)
            {
                overlay.Visible = false;
                overlay.SpriteFrames = null;
            }
            return;
        }

        string classe = ObterMetaString(node, MetaCharacterClass);
        equipped.TryGetValue((int)TipoEquipamento.Arma, out var arma);
        equipped.TryGetValue((int)TipoEquipamento.Escudo, out var escudo);
        var frames = Player.CriarSpriteFramesOverlayEquipamentoParaVisual(item, classe, arma, escudo);
        if (frames == null || frames.GetAnimationNames().Length == 0)
            return;

        overlay ??= CriarOverlayRemoto(node, baseSprite, nodeName);
        overlay.SpriteFrames = frames;
        overlay.Visible = true;
        overlay.Modulate = baseSprite.Modulate;
    }

    private static AnimatedSprite2D CriarOverlayRemoto(Node2D node, AnimatedSprite2D baseSprite, string nodeName)
    {
        var overlay = new AnimatedSprite2D
        {
            Name = nodeName,
            Position = baseSprite.Position,
            Scale = baseSprite.Scale,
            Offset = baseSprite.Offset,
            Centered = baseSprite.Centered,
            ZIndex = baseSprite.ZIndex + 1,
            ZAsRelative = baseSprite.ZAsRelative,
            Visible = false,
        };
        node.AddChild(overlay);
        return overlay;
    }

    private static bool DeveAplicarOverlayEquipamentoRemoto(ItemResource item)
    {
        if (item == null || item.Tipo == TipoEquipamento.Arma)
            return false;

        if (item.SpriteFramesEquipamento != null || item.SpritesheetEquipamento != null)
            return true;

        return item.CategoriaPeso == PesoItem.Medio
            && (item.Tipo == TipoEquipamento.Capacete
                || item.Tipo == TipoEquipamento.Peitoral
                || item.Tipo == TipoEquipamento.Calca
                || item.Tipo == TipoEquipamento.Botas);
    }

    private static void AplicarAparenciaVisualRemota(Node2D node, AnimatedSprite2D baseSprite, string spritesheetPath, Color cor, string nome)
    {
        string nodeName = $"{RemotePaperdollPrefix}{nome}";
        var overlay = node.GetNodeOrNull<AnimatedSprite2D>(nodeName);
        if (string.IsNullOrWhiteSpace(spritesheetPath) || !ResourceLoader.Exists(spritesheetPath))
        {
            if (overlay != null)
            {
                overlay.Visible = false;
                overlay.SpriteFrames = null;
            }
            return;
        }

        var tex = ResourceLoader.Load<Texture2D>(spritesheetPath);
        if (tex == null)
            return;

        string classe = ObterMetaString(node, MetaCharacterClass);
        var frames = LpcSpriteFramesBuilder.Construir(tex, ClasseRegistry.ObterPrefixoAtaqueRecomendado(classe));
        if (frames == null || frames.GetAnimationNames().Length == 0)
            return;

        overlay ??= CriarOverlayRemoto(node, baseSprite, nodeName);
        overlay.SpriteFrames = frames;
        overlay.Modulate = cor;
        overlay.Visible = true;
        SincronizarOverlaysRemotos(node, baseSprite);
    }

    private static Color ParseColorHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Colors.White;

        try
        {
            return Color.FromHtml(value.StartsWith("#") ? value : "#" + value);
        }
        catch
        {
            return Colors.White;
        }
    }

    private Node2D CreatePetEntity(ulong entityId, string name, float x, float y, int level, int health, int maxHealth, string ownerIdText, string petIdText, string petData)
    {
        if (!ulong.TryParse(ownerIdText, out ulong ownerId) || ownerId == _gameNet?.LocalPlayerId)
            return null!;

        string animPrefix = name;
        if (!string.IsNullOrWhiteSpace(petData))
        {
            var parsed = Json.ParseString(petData).AsGodotDictionary();
            animPrefix = (string)parsed.GetValueOrDefault("anim_prefix", name);
        }

        string mobType = NormalizarPetAnimPrefix(animPrefix, name);
        var root = new Node2D
        {
            Position = ParaPosicaoVisual(x, y),
            Name = $"Pet_{entityId}",
            Scale = new Vector2(1.4f, 1.4f),
            ZIndex = 0,
            ZAsRelative = true,
            YSortEnabled = false,
        };
        root.SetMeta("network_id", entityId);
        root.SetMeta("owner_id", ownerId);
        root.SetMeta("pet_anim_prefix", mobType);
        root.SetMeta("pet_level", level);
        root.AddToGroup("RemotePets");
        PrepararEntidadeYSort(root);

        var sprite = new AnimatedSprite2D
        {
            Name = "AnimatedSprite2D",
            SpriteFrames = CarregarPetFrames(mobType, name),
            Scale = Vector2.One * 1.1f,
        };
        root.AddChild(sprite);
        TocarAnimacaoPet(sprite, mobType, "idle_down");
        CriarBarraVidaPet(root, health, maxHealth);

        var world = ObterMundo();
        if (world != null)
            world.AddChild(root);
        else
            AddChild(root);

        return root;
    }

    public void AtualizarOverheadRemoto(ulong entityId, string nome, string guildName, string guildTag, int guildEmblem, long xp, long xpMax, string factionId = "")
    {
        if (!_networkNodes.TryGetValue(entityId, out var node)) return;
        if (node == null || !IsInstanceValid(node) || node.IsQueuedForDeletion())
        {
            _networkNodes.Remove(entityId);
            return;
        }

        var overhead = node.GetNodeOrNull<OverheadUI>("OverheadUI_Remoto");
        if (overhead == null || !IsInstanceValid(overhead) || overhead.IsQueuedForDeletion())
            return;

        if (!string.IsNullOrWhiteSpace(factionId))
            node.SetMeta("faction_id", factionId);
        node.SetMeta("guild_name", guildName);

        overhead.AtualizarDadosRemotos(nome, guildName, guildTag, guildEmblem, xp, xpMax);
        overhead.DefinirCorNome(CalcularCorNomeRemoto(entityId, ObterMetaString(node, "faction_id"), guildName));
    }

    private void OnPartyDataChanged(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members)
    {
        AtualizarCoresNomesRemotos();
    }

    private void OnPartyMemberChanged(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined, string characterClass)
    {
        AtualizarOverheadStatusRemoto(entityId, health, maxHealth, mana, maxMana);
    }

    private void AtualizarOverheadStatusRemoto(ulong entityId, int health, int maxHealth, int mana, int maxMana)
    {
        if (!_networkNodes.TryGetValue(entityId, out var node) || node == null || !IsInstanceValid(node))
            return;

        var overhead = node.GetNodeOrNull<OverheadUI>("OverheadUI_Remoto");
        if (overhead == null || !IsInstanceValid(overhead) || overhead.IsQueuedForDeletion())
            return;

        overhead.AtualizarStatusRemoto(health, maxHealth, mana, maxMana);
    }

    private void AtualizarOverheadVidaRemota(ulong entityId, int health, int maxHealth)
    {
        if (!_networkNodes.TryGetValue(entityId, out var node) || node == null || !IsInstanceValid(node))
            return;

        if (node.IsInGroup("RemotePets"))
        {
            AtualizarBarraVidaPet(node, health, maxHealth);
            return;
        }

        var overhead = node.GetNodeOrNull<OverheadUI>("OverheadUI_Remoto");
        if (overhead == null || !IsInstanceValid(overhead) || overhead.IsQueuedForDeletion())
            return;

        overhead.AtualizarVidaRemota(health, maxHealth);
    }

    private static void CriarBarraVidaPet(Node2D root, int health, int maxHealth)
    {
        var bar = new Control
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

        var fill = new ColorRect
        {
            Name = "Fill",
            Color = new Color(0.86f, 0.09f, 0.10f, 0.95f),
            Position = new Vector2(1f, 1f),
            Size = new Vector2(54f, 3f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        bar.AddChild(bg);
        bar.AddChild(fill);
        root.AddChild(bar);
        AtualizarBarraVidaPet(root, health, maxHealth);
    }

    private static void AtualizarBarraVidaPet(Node2D root, int health, int maxHealth)
    {
        var bar = root.GetNodeOrNull<Control>("PetHealthBar");
        var fill = bar?.GetNodeOrNull<ColorRect>("Fill");
        if (bar == null || fill == null || !IsInstanceValid(bar) || !IsInstanceValid(fill))
            return;

        float pct = maxHealth > 0 ? Mathf.Clamp(health / (float)maxHealth, 0f, 1f) : 0f;
        fill.Size = new Vector2(54f * pct, 3f);
        bar.Visible = health > 0;
    }

    private void AtualizarOverheadManaRemota(ulong entityId, int mana, int maxMana)
    {
        if (!_networkNodes.TryGetValue(entityId, out var node) || node == null || !IsInstanceValid(node))
            return;

        var overhead = node.GetNodeOrNull<OverheadUI>("OverheadUI_Remoto");
        if (overhead == null || !IsInstanceValid(overhead) || overhead.IsQueuedForDeletion())
            return;

        overhead.AtualizarManaRemota(mana, maxMana);
    }

    private void OnGuildDataChanged(int guildId, string guildName, string guildTag, int guildEmblem, Godot.Collections.Array<Godot.Collections.Dictionary> members, int level, int xp, int skillPoints, Godot.Collections.Array<Godot.Collections.Dictionary> skills)
    {
        AtualizarCoresNomesRemotos();
    }

    private void OnGuildMemberChanged(ulong entityId, string name, int rank, bool joined)
    {
        AtualizarCoresNomesRemotos();
    }

    private void OnGuildClearedHandler()
    {
        AtualizarCoresNomesRemotos();
    }

    private void AtualizarCoresNomesRemotos()
    {
        foreach (var kvp in _networkNodes)
        {
            var node = kvp.Value;
            if (node == null || !IsInstanceValid(node) || node.IsQueuedForDeletion())
                continue;
            if (!node.HasMeta("player_name"))
                continue;

            var overhead = node.GetNodeOrNull<OverheadUI>("OverheadUI_Remoto");
            if (overhead == null || !IsInstanceValid(overhead) || overhead.IsQueuedForDeletion())
                continue;

            string factionId = ObterMetaString(node, "faction_id");
            string guildName = ObterMetaString(node, "guild_name");
            overhead.DefinirCorNome(CalcularCorNomeRemoto(kvp.Key, factionId, guildName));
        }
    }

    private Color CalcularCorNomeRemoto(ulong entityId, string remoteFactionId, string remoteGuildName)
    {
        if (EstaNaMinhaParty(entityId))
            return NomeCorParty;

        if (EstaNaMinhaGuild(entityId, remoteGuildName))
            return NomeCorGuild;

        string localFactionId = ObterFaccaoLocalId();
        if (!string.IsNullOrWhiteSpace(localFactionId)
            && !string.IsNullOrWhiteSpace(remoteFactionId)
            && !localFactionId.Equals(remoteFactionId, System.StringComparison.OrdinalIgnoreCase))
            return NomeCorFaccaoInimiga;

        return NomeCorNormal;
    }

    private bool EstaNaMinhaParty(ulong entityId)
    {
        if (_gameNet == null || !_gameNet.HasPendingPartyData)
            return false;

        foreach (var member in _gameNet.PendingPartyMembers)
        {
            if (!member.ContainsKey("entity_id"))
                continue;
            if (ConverterEntityId(member["entity_id"]) == entityId)
                return true;
        }
        return false;
    }

    private bool EstaNaMinhaGuild(ulong entityId, string remoteGuildName)
    {
        if (_gameNet == null || _gameNet.GuildId < 0)
            return false;

        foreach (var member in _gameNet.CachedGuildMembers)
        {
            if (!member.ContainsKey("entity_id"))
                continue;
            if (ConverterEntityId(member["entity_id"]) == entityId)
                return true;
        }

        return !string.IsNullOrWhiteSpace(_gameNet.GuildName)
            && !string.IsNullOrWhiteSpace(remoteGuildName)
            && _gameNet.GuildName.Equals(remoteGuildName, System.StringComparison.OrdinalIgnoreCase);
    }

    private string ObterFaccaoLocalId()
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (!string.IsNullOrWhiteSpace(player?.FaccaoAtiva?.IdFaccao))
            return player.FaccaoAtiva.IdFaccao.Trim();

        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        return escolhido?.Faccao?.IdFaccao?.Trim() ?? "";
    }

    private static string ObterMetaString(Node node, string key)
    {
        return node.HasMeta(key) ? node.GetMeta(key).AsString() : "";
    }

    private static ulong ConverterEntityId(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Int => (ulong)value.AsInt64(),
            Variant.Type.Float => (ulong)value.AsDouble(),
            Variant.Type.String => ulong.TryParse(value.AsString(), out var parsed) ? parsed : 0UL,
            _ => 0UL,
        };
    }

    private void CarregarCenasMob()
    {
        _inimigoScene = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Inimigo.tscn");
        _mobScenes["goblin"] = _inimigoScene;
        _mobScenes["lobo"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Sherigan.tscn");
        _mobScenes["porco"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Porco.tscn");
        _mobScenes["minotauro"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Minotauro.tscn");
        _mobScenes["slime"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Slime.tscn");
        _mobScenes["slimeElite"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/SlimeElite.tscn");
        _mobScenes["slimeBoss"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/SlimeBoss.tscn");
        _mobScenes["plantaCarnivora"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/PlanTaCarnivora.tscn");
        _mobScenes["plantaCarnivoraElite"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/PlanTaCarnivoraElite.tscn");
        _mobScenes["cogumelo"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Cogumelo.tscn");
        _mobScenes["cogumeloElite"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/CogumeloEllite.tscn");
    }

    private PackedScene? ObterCenaMob(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId))
            return _inimigoScene;

        string mobType = _mobNameToType.TryGetValue(prefabId, out var mapped) ? mapped : prefabId;

        if (_mobScenes.TryGetValue(mobType, out var scene) && scene != null)
            return scene;

        return _inimigoScene;
    }

    private Node2D CreateMonsterEntity(ulong entityId, string name, float x, float y, int level, int health, int maxHealth, string prefabId, bool isBoss)
    {
        string mobType = _mobNameToType.TryGetValue(prefabId, out var mapped) ? mapped : prefabId;
        var scene = ObterCenaMob(prefabId);

        if (scene != null)
        {
            var inimigo = scene.Instantiate<Inimigo>();
            if (inimigo == null)
            {
                GameNetwork.LogError($"Cena de mob invalida para '{name}' ({entityId}) prefab='{prefabId}' tipo='{mobType}'. Usando placeholder.");
                return CreateMonsterPlaceholder(entityId, name, x, y, level, isBoss);
            }
            inimigo.Position = ParaPosicaoVisual(x, y);
            inimigo.Name = $"Monster_{entityId}";
            PrepararEntidadeYSort(inimigo);
            inimigo.NomeDoInimigo = name;
            inimigo.VidaMaxima = maxHealth;
            inimigo.IsBoss = isBoss;
            inimigo.Level = level;
            inimigo.MobType = mobType;
            if (isBoss)
                inimigo.AddToGroup("Bosses");
            inimigo.SetVidaAtual(health, maxHealth);
            inimigo.AtualizarPetPadrao();
            inimigo.AnimPrefix = "";
            inimigo.SetMeta("network_id", entityId);
            inimigo.SetMeta(MetaAnimPrefix, inimigo.AnimPrefix);
            inimigo.NetworkTargetPos = ParaPosicaoVisual(x, y);

            string levelTag = isBoss
                ? $"[center][color=red]Boss[/color]\n[color=yellow]Lv.{level}[/color] {name}[/center]"
                : $"[center][color=yellow]Lv.{level}[/color] {name}[/center]";
            var labelNome = new RichTextLabel
            {
                Name = "MobNameLabel",
                Text = levelTag,
                Position = new Vector2(-85, isBoss ? -116 : -92),
                Size = new Vector2(170, isBoss ? 52 : 28),
                ZIndex = 5,
                BbcodeEnabled = true,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.Off,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AplicarEstiloNomeMob(labelNome, Colors.White);
            inimigo.AddChild(labelNome);

            { var _p = ObterMundo(); if (_p != null) _p.AddChild(inimigo); else AddChild(inimigo); }
            return inimigo;
        }

        return CreateMonsterPlaceholder(entityId, name, x, y, level, isBoss);
    }

    private static bool EhPrefabBoss(string prefabId, string name)
    {
        return prefabId.Contains("boss", System.StringComparison.OrdinalIgnoreCase)
            || name.Contains("boss", System.StringComparison.OrdinalIgnoreCase);
    }

    private Node2D CreateMonsterPlaceholder(ulong entityId, string name, float x, float y, int level, bool isBoss)
    {
        var placeholder = new Node2D();
        placeholder.Position = ParaPosicaoVisual(x, y);
        placeholder.Name = $"Monster_{entityId}";
        PrepararEntidadeYSort(placeholder);
        if (isBoss)
            placeholder.AddToGroup("Bosses");

        var icon = new Label
        {
            Text = isBoss ? "\u2622" : "\u25A0",
            Position = new Vector2(-12, -12),
            Scale = new Vector2(isBoss ? 3f : 2f, isBoss ? 3f : 2f),
        };
        icon.AddThemeColorOverride("font_color", isBoss ? new Color(1.0f, 0.2f, 0.2f) : new Color(0.8f, 0.3f, 0.1f));
        placeholder.AddChild(icon);

        var labelName = new Label
        {
            Text = $"{name} Lv.{level}",
            Position = new Vector2(-30, -60),
            ZIndex = 2,
        };
        AplicarEstiloNomeMob(labelName, Colors.White);
        placeholder.AddChild(labelName);

        var _p2 = ObterMundo();
        if (_p2 != null)
            _p2.AddChild(placeholder);
        else
            AddChild(placeholder);
        return placeholder;
    }

    private Node2D CreateNpcEntity(ulong entityId, string name, float x, float y, string dialogId, string race, string animPrefix)
    {
        var existing = GetTree()?.GetNodesInGroup("NPC");
        GD.Print($"[EntityManager] CreateNpcEntity({entityId}, {name}, {x}, {y}) — {existing?.Count ?? 0} NPCs no grupo");
        Node2D? root = null;
        bool isInvisibleRefinePoint = string.Equals(dialogId, "refino", StringComparison.OrdinalIgnoreCase);

        if (existing != null)
        {
            var pos = ParaPosicaoVisual(x, y);
            foreach (Node node in existing)
            {
                if (node is Node2D n2d && IsInstanceValid(n2d))
                {
                    // Skip if already linked to another NPC
                    if (n2d.HasMeta("network_id"))
                        continue;
                    float d = n2d.GlobalPosition.DistanceTo(pos);
                    GD.Print($"[EntityManager]   Verificando '{n2d.Name}' em {n2d.GlobalPosition} dist={d:F1}");
                    float linkDistance = isInvisibleRefinePoint ? 24f : 5f;
                    if (d < linkDistance)
                    {
                        n2d.SetMeta("network_id", (long)entityId);
                        n2d.SetMeta("dialog_id", dialogId);
                        n2d.SetMeta(MetaAnimPrefix, animPrefix);
                        PrepararEntidadeYSort(n2d);
                        GD.Print($"[EntityManager] NPC {name} (ID {entityId}) vinculado ao WorldNPC existente '{n2d.Name}'");
                        root = n2d;
                        break;
                    }
                }
            }
        }

        if (root == null)
        {
            var body = new CharacterBody2D();
            body.Position = ParaPosicaoVisual(x, y);
            body.Name = $"NPC_{entityId}";
            PrepararEntidadeYSort(body);
            body.SetMeta("network_id", entityId);
            body.SetMeta("dialog_id", dialogId);
            body.AddToGroup("NPC");
            body.SetMeta(MetaAnimPrefix, animPrefix);

            var col = new CollisionShape2D();
            col.Shape = new CircleShape2D { Radius = 40f };
            body.AddChild(col);
            body.CollisionLayer = 2u;
            root = body;
        }
        else if (root is CollisionObject2D colObj)
        {
            colObj.CollisionLayer = 2u;
        }

        if (isInvisibleRefinePoint)
        {
            root.SetMeta("invisible_interaction", true);
            root.SetMeta("interaction_prompt", "[F] Refinar");
        }

        // Always add sprite, name label and prompt (even for linked nodes)
        if (!isInvisibleRefinePoint && root.GetNodeOrNull("AnimatedSprite") == null)
        {
            var sprite = new AnimatedSprite2D();
            sprite.Name = "AnimatedSprite";
            sprite.Position = new Vector2(0, -5);
            sprite.Scale = Vector2.One * 2f;

            string sheetPath = LpcSpriteFramesBuilder.PastaSpritesNpc + race + ".png";
            if (!ResourceLoader.Exists(sheetPath))
            {
                string raceFile = (race ?? "Humano").Trim().Replace(" ", "");
                sheetPath = LpcSpriteFramesBuilder.PastaSpritesRaca + raceFile + ".png";
            }
            root.SetMeta(MetaSpritePath, sheetPath);

            if (ResourceLoader.Exists(sheetPath))
            {
                var sheet = ResourceLoader.Load<Texture2D>(sheetPath);
                if (sheet != null)
                {
                    var frames = LpcSpriteFramesBuilder.Construir(sheet, animPrefix);
                    if (frames != null && frames.GetAnimationNames().Length > 0)
                    {
                        sprite.SpriteFrames = frames;
                        sprite.Play("idle_down");
                    }
                }
            }

            root.AddChild(sprite);
        }
        else if (isInvisibleRefinePoint && root.GetNodeOrNull("AnimatedSprite") is Node spriteNode)
        {
            spriteNode.QueueFree();
        }

        if (!isInvisibleRefinePoint && root.GetNodeOrNull("NameLabel") == null)
        {
            var labelName = new Label
            {
                Text = name,
                Position = new Vector2(-70, -68),
                ZIndex = 2,
                Size = new Vector2(140, 24),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            labelName.Name = "NameLabel";
            AplicarEstiloNomeNpc(labelName);
            root.AddChild(labelName);
        }
        else if (root.GetNodeOrNull("NameLabel") is Label existingNameLabel)
        {
            existingNameLabel.Text = name;
            existingNameLabel.Position = new Vector2(-70, -68);
            existingNameLabel.Size = new Vector2(140, 24);
            existingNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            existingNameLabel.VerticalAlignment = VerticalAlignment.Center;
            AplicarEstiloNomeNpc(existingNameLabel);
        }
        else if (isInvisibleRefinePoint && root.GetNodeOrNull("NameLabel") is Node nameNode)
        {
            nameNode.QueueFree();
        }

        if (root.GetNodeOrNull("InteractPrompt") == null)
        {
            var prompt = new Label();
            prompt.Text = isInvisibleRefinePoint ? "[F] Refinar" : "[F] Falar";
            prompt.Name = "InteractPrompt";
            prompt.Position = new Vector2(-20, -45);
            prompt.ZIndex = 2;
            prompt.AddThemeFontSizeOverride("font_size", 18);
            prompt.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.3f));
            prompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
            prompt.AddThemeConstantOverride("outline_size", 2);
            prompt.Visible = false;
            root.AddChild(prompt);
        }
        else if (isInvisibleRefinePoint && root.GetNodeOrNull("InteractPrompt") is Label existingPrompt)
        {
            existingPrompt.Text = "[F] Refinar";
        }

        if (root.GetParent() == null)
        {
            var _p3 = ObterMundo();
            if (_p3 != null)
                _p3.AddChild(root);
            else
                AddChild(root);
        }

        return root;
    }

    private void OnRespawnHandler(ulong entityId, float x, float y, int health, int maxHealth, int mana, int maxMana)
    {
        if (!_networkNodes.TryGetValue(entityId, out var node) || !IsInstanceValid(node))
            return;

        node.Position = ParaPosicaoVisual(x, y);

        if (node is Player player)
        {
            player.SetHealthFromServer(health, maxHealth);
            if (maxMana > 0)
                player.SetManaFromServer(mana, maxMana);
        }
        else if (node is Inimigo inimigo)
            inimigo.SetVidaAtual(health, maxHealth);

        if (node.IsInGroup("PlayersDowned"))
        {
            SetRemotePlayerRevived(node);
        }
    }

    private void OnEntityHealthUpdateHandler(ulong entityId, int health, int maxHealth)
    {
        if (_networkNodes.TryGetValue(entityId, out var node) && IsInstanceValid(node))
        {
            if (node is Inimigo inimigo)
                inimigo.SetVidaAtual(health, maxHealth);
            else if (node is Player player && entityId != _gameNet?.LocalPlayerId)
                player.SetHealthFromServer(health, maxHealth);
            else
            {
                AtualizarOverheadVidaRemota(entityId, health, maxHealth);
                if (health <= 0)
                    SetRemotePlayerDowned(node);
                else if (node.IsInGroup("PlayersDowned"))
                    SetRemotePlayerRevived(node);
            }
        }
        else if (entityId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            if (localPlayer != null && IsInstanceValid(localPlayer))
                localPlayer.SetHealthFromServer(health, maxHealth);
        }
    }

    private void OnEntityManaUpdateHandler(ulong entityId, int mana, int maxMana)
    {
        if (_networkNodes.TryGetValue(entityId, out var node) && IsInstanceValid(node) && node is Player remotePlayer)
        {
            remotePlayer.SetManaFromServer(mana, maxMana);
        }
        else if (_networkNodes.ContainsKey(entityId))
        {
            AtualizarOverheadManaRemota(entityId, mana, maxMana);
        }
        else if (entityId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            if (localPlayer != null && IsInstanceValid(localPlayer))
                localPlayer.SetManaFromServer(mana, maxMana);
        }
    }

    private void OnCombatResult(ulong attackerId, ulong targetId, int damage, bool isCrit, int targetHealth, int targetMaxHealth, int skillId)
    {
        // Reproduz o ataque de qualquer entidade remota.
        if (attackerId != _gameNet?.LocalPlayerId &&
            _networkNodes.TryGetValue(attackerId, out var attackerNode) &&
            IsInstanceValid(attackerNode))
        {
            Node2D? animTargetNode = targetId == _gameNet?.LocalPlayerId
                ? GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D
                : (_networkNodes.TryGetValue(targetId, out var remoteTarget) ? remoteTarget : null);

            if (animTargetNode != null && IsInstanceValid(animTargetNode))
            {
                Vector2 dirToTarget = (animTargetNode.Position - attackerNode.Position).Normalized();
                if (attackerNode is Inimigo attackerMob)
                    attackerMob.TriggerAttackAnimation(dirToTarget);
            }
        }

        // Update target health (including local player)
        Node2D? targetNode = null;
        if (targetId == _gameNet?.LocalPlayerId)
        {
            targetNode = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        }
        else if (_networkNodes.TryGetValue(targetId, out var netTargetNode) && IsInstanceValid(netTargetNode))
        {
            targetNode = netTargetNode;
        }

        if (targetNode != null && IsInstanceValid(targetNode))
        {
            if (damage > 0 && (EhAtaqueDeArqueiro(attackerId) || skillId is 10206 or 16))
            {
                TocarImpactoFlecha(targetNode, skillId == 10203 ? 1.65f : 1f);
                if (skillId == 10204)
                    TocarEfeitoTiroParalisante(targetNode);
                if (skillId == 10206 || skillId == 16)
                    TocarEfeitoMarcaExecutorPoison(targetNode);
                if (skillId == 10209 || skillId == 19)
                    TocarEfeitoTiroExecucao(targetNode);
            }
            if (damage > 0 && skillId == 0 && EhAtaqueBasicoDeMago(attackerId))
                TocarEfeitoAtaqueBasicoMago(targetNode, 0.45f);
            if (damage > 0 && skillId == 0 && EhAtaqueBasicoDeClerigo(attackerId))
                TocarEfeitoClerigoDano(targetNode, ClerigoBasicHitEffectPath, "ClerigoBasicHitEffect", "clerigo_basic_hit", 5, 4, 0.8f, new Vector2(0f, -28f));
            if (damage > 0 && skillId == 11201)
                TocarEfeitoAtaqueBasicoMago(targetNode, 1.45f);
            if (damage > 0 && skillId == 11203)
                TocarEfeitoRaioEstatico(targetNode);
            if (damage > 0 && skillId == 11202)
                TocarEfeitoLancaGelo(targetNode);
            if (damage > 0 && skillId == 11204)
                TocarGiroVisualTornado(targetNode);
            if (damage > 0 && skillId == 11206)
                TocarEfeitoTempestadeEletrica(targetNode);
            if (damage > 0 && skillId == 11207)
                TocarEfeitoExplosaoVulcanica(targetNode);
            if (damage > 0 && skillId == 11209)
                TocarEfeitoChuvaMeteoros(targetNode);
            if (skillId == 11205)
                TocarEfeitoPrisaoGelo(targetNode);
            if (skillId == 11210)
                TocarEfeitoSonoArcano(targetNode);
            if (damage > 0 && skillId == 12201)
                TocarEfeitoGolpeSombrio(targetNode, ObterNoCombate(attackerId));
            if (damage > 0 && skillId == 12204)
                TocarEfeitoGolpeAtordoante(targetNode, 3f);
            if (damage > 0 && skillId == 12210)
                TocarEfeitoGolpeAtordoante(targetNode, 2f);
            if (damage > 0 && skillId == 12205)
                TocarEfeitoPunhaladaNasCostas(targetNode, ObterNoCombate(attackerId));
            if (damage > 0 && skillId == 12207)
                TocarEfeitoSequenciaMortal(targetNode);
            if (damage > 0 && skillId == 12209)
                TocarEfeitoExecucaoFinal(targetNode);
            if (damage > 0 && skillId == 13002)
                TocarEfeitoInvestidaBrutal(targetNode);
            if (damage > 0 && skillId == 13001)
                TocarEfeitoClerigoDano(targetNode, SangramentoMortalEffectPath, "SangramentoMortalTickEffect", "sangramento_mortal_tick", 5, 3, 1.0f, new Vector2(0f, -34f));
            if (damage > 0 && skillId == 15001)
                TocarEfeitoClerigoDano(targetNode, RaioSagradoEffectPath, "RaioSagradoEffect", "raio_sagrado_cast", 5, 3, 0.9f, new Vector2(0f, -34f));
            if (damage > 0 && skillId == 15102)
                TocarEfeitoClerigoDano(targetNode, MarteloDesolacaoEffectPath, "MarteloDesolacaoEffect", "martelo_desolacao_cast", 5, 3, 1.65f, new Vector2(0f, -36f));
            if (damage > 0 && skillId == 15106)
            {
                TocarEfeitoClerigoDano(targetNode, ChoqueEstaticoEffectPath, "ChoqueEstaticoEffect", "choque_estatico_cast", 5, 2, 1.65f, new Vector2(0f, -30f));
                TocarEfeitoGolpeAtordoante(targetNode, 3f);
            }
            if (damage > 0 && skillId == 15109)
                TocarEfeitoClerigoDano(targetNode, MilagreDivinoEffectPath, "MilagreDivinoEffect", "milagre_divino_cast", 5, 4, 1.7f, new Vector2(0f, -48f));
            if (damage > 0 && skillId == 14100)
                TocarProjetilCorteRapido(ObterNoCombate(attackerId), targetNode);
            if (damage > 0 && skillId == 14102)
                TocarEfeitoEstocada(targetNode);
            if (damage > 0 && skillId == 14104)
                TocarEfeitoEspadaEstelar(targetNode);
            if (damage > 0 && skillId == 14103)
                TocarEfeitoClerigoDano(targetNode, GolpeDeEscudoImpactEffectPath, "GolpeDeEscudoImpactEffect", "golpe_de_escudo_hit", 5, 6, 0.65f, new Vector2(0f, 16f));
            if (skillId is 14101 or 14106)
            {
                if (targetId == attackerId)
                    TocarEfeitoDesafioCaster(targetNode);
                else
                    TocarEfeitoDesafioMob(targetNode, skillId == 14101 ? 4f : 5f);
            }
            if (damage > 0 && skillId == 14105)
                TocarEfeitoEspinhos(targetNode);
            if (skillId == 13105)
                TocarEfeitoMachadoGiratorioNoCaster(attackerId);
            if (damage > 0 && skillId == 13107)
                TocarEfeitoCarnificinaStun(targetNode, 4f);
            if (damage < 0 && skillId == 15101 && targetId != _gameNet?.LocalPlayerId)
                TocarEfeitoCuraMenor(targetNode);
            if (skillId == 15105)
                TocarEfeitoRenovacao(targetNode, 10f);
            if (skillId == 15107)
                TocarEfeitoLuzRestauradora(targetNode);
            if (skillId == 15108)
                TocarEfeitoRessurreicao(targetNode);

            if (targetNode is Inimigo inimigo)
                inimigo.SetVidaAtual(targetHealth, targetMaxHealth);
            else if (targetNode is Player player)
                player.SetHealthFromServer(targetHealth, targetMaxHealth);
            else
            {
                AtualizarOverheadVidaRemota(targetId, targetHealth, targetMaxHealth);
                if (targetHealth <= 0)
                    SetRemotePlayerDowned(targetNode);
            }

            if (damage == 0)
                return;

            bool isPlayerTakingDamage = targetId == _gameNet?.LocalPlayerId;

            var damageLabel = new Label
            {
                Text = damage < 0 ? $"+{-damage}" : damage.ToString(),
                Position = new Vector2(-10, -20),
                ZIndex = 10,
            };
            damageLabel.AddThemeFontOverride("font", GetBoldFont());

            if (damage < 0)
            {
                damageLabel.AddThemeFontSizeOverride("font_size", 26);
                damageLabel.AddThemeColorOverride("font_color", new Color(0.45f, 1.0f, 0.25f));
                damageLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
                damageLabel.AddThemeConstantOverride("outline_size", 5);
            }
            else if (isCrit)
            {
                damageLabel.Text = $"{damage}!";
                damageLabel.AddThemeFontSizeOverride("font_size", 32);
                damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.1f));
                damageLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
                damageLabel.AddThemeConstantOverride("outline_size", 6);
            }
            else
            {
                damageLabel.AddThemeFontSizeOverride("font_size", 26);
                damageLabel.AddThemeColorOverride("font_color", Colors.White);
                damageLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
                damageLabel.AddThemeConstantOverride("outline_size", 5);
            }

            targetNode.AddChild(damageLabel);

            var tween = CreateTween();
            tween.TweenProperty(damageLabel, "position", damageLabel.Position + new Vector2(0, -30), 0.8f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(damageLabel)) damageLabel.QueueFree();
            }));
            tween.Play();
        }
    }

    private bool EhAtaqueDeArqueiro(ulong attackerId)
    {
        if (attackerId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            string classeLocal = localPlayer?.NomeDaClasse ?? "";
            return classeLocal.ToLowerInvariant().Contains("arqueiro");
        }

        if (!_networkNodes.TryGetValue(attackerId, out var attackerNode) || !IsInstanceValid(attackerNode))
            return false;

        if (attackerNode.HasMeta(MetaAnimPrefix))
        {
            string prefixo = attackerNode.GetMeta(MetaAnimPrefix).AsString().ToLowerInvariant();
            if (prefixo.Contains("arqueiro") || prefixo.Contains("arco"))
                return true;
        }

        return false;
    }

    private bool EhAtaqueBasicoDeMago(ulong attackerId)
    {
        if (attackerId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            string classeLocal = localPlayer?.NomeDaClasse ?? "";
            return classeLocal.Contains("mago", System.StringComparison.OrdinalIgnoreCase);
        }

        if (!_networkNodes.TryGetValue(attackerId, out var attackerNode) || !IsInstanceValid(attackerNode))
            return false;

        if (attackerNode.HasMeta(MetaCharacterClass))
        {
            string classe = attackerNode.GetMeta(MetaCharacterClass).AsString();
            if (classe.Contains("mago", System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (classe.Contains("prist", System.StringComparison.OrdinalIgnoreCase)
                || classe.Contains("priest", System.StringComparison.OrdinalIgnoreCase)
                || classe.Contains("clerigo", System.StringComparison.OrdinalIgnoreCase)
                || classe.Contains("clérigo", System.StringComparison.OrdinalIgnoreCase)
                || classe.Contains("sacerdote", System.StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (attackerNode.HasMeta(MetaAnimPrefix))
        {
            string prefixo = attackerNode.GetMeta(MetaAnimPrefix).AsString();
            return prefixo.Contains("mago", System.StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool EhAtaqueBasicoDeClerigo(ulong attackerId)
    {
        static bool EhClasseClerigo(string value)
        {
            return value.Contains("prist", System.StringComparison.OrdinalIgnoreCase)
                || value.Contains("priest", System.StringComparison.OrdinalIgnoreCase)
                || value.Contains("clerigo", System.StringComparison.OrdinalIgnoreCase)
                || value.Contains("clérigo", System.StringComparison.OrdinalIgnoreCase)
                || value.Contains("sacerdote", System.StringComparison.OrdinalIgnoreCase);
        }

        if (attackerId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            return EhClasseClerigo(localPlayer?.NomeDaClasse ?? "");
        }

        if (!_networkNodes.TryGetValue(attackerId, out var attackerNode) || !IsInstanceValid(attackerNode))
            return false;

        if (attackerNode.HasMeta(MetaCharacterClass) && EhClasseClerigo(attackerNode.GetMeta(MetaCharacterClass).AsString()))
            return true;

        if (attackerNode.HasMeta(MetaAnimPrefix))
        {
            string prefixo = attackerNode.GetMeta(MetaAnimPrefix).AsString();
            return prefixo.Contains("maca", System.StringComparison.OrdinalIgnoreCase)
                || prefixo.Contains("maça", System.StringComparison.OrdinalIgnoreCase)
                || prefixo.Contains("martelo", System.StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void TocarEfeitoAtaqueBasicoMago(Node2D targetNode, float escala = 1f)
    {
        if (!ResourceLoader.Exists(MagoBasicHitEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(MagoBasicHitEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Scale = Vector2.One * Mathf.Max(0.1f, escala);
        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoCuraMenor(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(CuraMenorEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(CuraMenorEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 5;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "cura_menor_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "CuraMenorEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -2f),
            Scale = new Vector2(1.55f, 1.55f),
            ZIndex = 132,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoCuraEmAreaNoChao(Vector2 globalPosition)
    {
        if (!ResourceLoader.Exists(CuraEmAreaEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(CuraEmAreaEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 6;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "cura_em_area_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "CuraEmAreaEffect",
            SpriteFrames = frames,
            GlobalPosition = globalPosition + new Vector2(0f, -18f),
            Scale = new Vector2(1.35f, 1.35f),
            ZIndex = 133,
            ZAsRelative = false,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        var world = ObterMundo();
        if (world != null)
            world.AddChild(effect);
        else
            AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoClerigoDano(Node2D targetNode, string texturePath, string nodeName, string animName, int columns, int rows, float scale, Vector2 offset)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(texturePath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(texturePath);
        if (texture == null)
            return;

        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = nodeName,
            SpriteFrames = frames,
            Position = offset,
            Scale = new Vector2(scale, scale),
            ZIndex = 134,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoLuzRestauradora(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(LuzRestauradoraEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(LuzRestauradoraEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 3;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "luz_restauradora_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 20f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "LuzRestauradoraEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -22f),
            Scale = new Vector2(1.78f, 1.78f),
            ZIndex = 133,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoRenovacao(Node2D targetNode, float duration = 0f)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(RenovacaoEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(RenovacaoEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 4;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "renovacao_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, duration > 0f);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "RenovacaoEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -10f),
            Scale = new Vector2(1.65f, 1.65f),
            ZIndex = 133,
            ZAsRelative = true,
        };

        if (duration > 0f)
        {
            var oldEffect = targetNode.GetNodeOrNull<Node>("RenovacaoEffect");
            if (oldEffect != null && IsInstanceValid(oldEffect))
                oldEffect.QueueFree();

            targetNode.AddChild(effect);
            effect.Play(animName);
            var timer = GetTree()?.CreateTimer(duration);
            if (timer != null)
            {
                timer.Timeout += () =>
                {
                    if (IsInstanceValid(effect))
                        effect.QueueFree();
                };
            }
            return;
        }

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoRessurreicao(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(RessurreicaoEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(RessurreicaoEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 3;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "ressurreicao_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 12f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "RessurreicaoEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -60f),
            Scale = new Vector2(1.7f, 1.7f),
            ZIndex = 134,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoPurificacao(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(PurificacaoEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(PurificacaoEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 5;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "purificacao_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "PurificacaoEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -34f),
            Scale = new Vector2(1.66f, 1.66f),
            ZIndex = 133,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoBencaoSagrada(Node2D targetNode, float duration)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        const string nodeName = "BencaoSagradaLoopEffect";
        var existing = targetNode.GetNodeOrNull<Node2D>(nodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();

        var frames = CriarFramesDeDiretorio(BencaoSagradaEffectDir, "bencao_sagrada_cast", true, 14f, 9);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = nodeName,
            SpriteFrames = frames,
            Position = new Vector2(0f, -8f),
            Scale = new Vector2(1.55f, 1.55f),
            ZIndex = 133,
            ZAsRelative = true,
        };

        targetNode.AddChild(effect);
        effect.Play("bencao_sagrada_cast");

        if (duration <= 0f)
            return;

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = duration,
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };
        timer.Start();
    }

    private void TocarEfeitoLancaGelo(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(LancaGeloEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(LancaGeloEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoRaioEstatico(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(RaioEstaticoEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(RaioEstaticoEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoTornadoNoCentro(Vector2 globalPosition)
    {
        if (!ResourceLoader.Exists(TornadoEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(TornadoEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.ZAsRelative = false;
        effect.ZIndex = 1;
        var world = ObterMundo();
        if (world != null)
            world.AddChild(effect);
        else
            AddChild(effect);
        effect.GlobalPosition = globalPosition;
    }

    private void OnSkillAreaEffect(int skillId, float x, float y, float radius, float duration)
    {
        if (skillId == 11204)
            TocarEfeitoTornadoNoCentro(new Vector2(x, y));
        else if (skillId == 15103)
            TocarEfeitoCuraEmAreaNoChao(new Vector2(x, y));
        else if (skillId == 13107)
            TocarEfeitoCarnificinaImpactoNoChao(new Vector2(x, y));
        else if (skillId == 14108)
            AtualizarEfeitoBastiao(new Vector2(x, y), radius, duration);
    }

    private void AtualizarEfeitoBastiao(Vector2 globalPosition, float radius, float duration)
    {
        string key = CriarChaveBastiao(globalPosition);
        if (duration <= 0f)
        {
            if (_bastiaoAreaEffects.TryGetValue(key, out var existing) && IsInstanceValid(existing))
            {
                if (duration < 0f)
                {
                    AjustarFrameBastiao(existing, -duration);
                    return;
                }

                existing.QueueFree();
            }
            _bastiaoAreaEffects.Remove(key);
            return;
        }

        if (!ResourceLoader.Exists(BastiaoEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(BastiaoEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 4;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        const string animName = "bastiao_area";
        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 20f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        float diameter = Mathf.Max(64f, radius * 2f);
        float scale = Mathf.Clamp(diameter / frameWidth, 0.8f, 2.2f);
        var effect = new AnimatedSprite2D
        {
            Name = "BastiaoAreaEffect",
            SpriteFrames = frames,
            GlobalPosition = globalPosition,
            Scale = Vector2.One * scale,
            ZIndex = 2,
            ZAsRelative = false,
        };

        var world = ObterMundo();
        if (world != null)
            world.AddChild(effect);
        else
            AddChild(effect);

        effect.Play(animName);
        effect.Frame = 0;
        effect.Pause();
        _bastiaoAreaEffects[key] = effect;

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.2f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
            _bastiaoAreaEffects.Remove(key);
        };
        timer.Start();
    }

    private static string CriarChaveBastiao(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.X / 8f);
        int y = Mathf.RoundToInt(position.Y / 8f);
        return $"{x}:{y}";
    }

    private static void AjustarFrameBastiao(AnimatedSprite2D effect, float progress)
    {
        if (effect.SpriteFrames == null || !effect.SpriteFrames.HasAnimation("bastiao_area"))
            return;

        int frameCount = effect.SpriteFrames.GetFrameCount("bastiao_area");
        if (frameCount <= 0)
            return;

        int frame = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(progress, 0f, 1f) * (frameCount - 1)), 0, frameCount - 1);
        effect.Frame = frame;
        effect.Pause();
    }

    private void TocarGiroVisualTornado(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !IsInsideTree())
            return;

        var visual = targetNode.GetNodeOrNull<Node2D>("AnimatedSprite")
            ?? targetNode.FindChild("AnimatedSprite", true, false) as Node2D;
        if (visual == null || !IsInstanceValid(visual))
            return;

        float originalRotation = visual.Rotation;
        Vector2 originalPosition = visual.Position;
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "rotation", originalRotation + Mathf.Tau, 0.55f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "position", originalPosition + new Vector2(0, -18), 0.22f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.Chain();
        tween.TweenProperty(visual, "position", originalPosition, 0.28f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(visual))
            {
                visual.Rotation = originalRotation;
                visual.Position = originalPosition;
            }
        }));
    }

    private void TocarEfeitoTempestadeEletrica(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(TempestadeEletricaEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(TempestadeEletricaEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoExplosaoVulcanica(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(ExplosaoVulcanicaEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(ExplosaoVulcanicaEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoChuvaMeteoros(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(ChuvaMeteorosEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(ChuvaMeteorosEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoSonoArcano(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(SonoArcanoEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(SonoArcanoEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoPrisaoGelo(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(PrisaoGeloEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(PrisaoGeloEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarImpactoFlecha(Node2D targetNode, float escala = 1f)
    {
        if (!ResourceLoader.Exists(FlechaImpactoEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(FlechaImpactoEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Scale = Vector2.One * Mathf.Max(0.1f, escala);
        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoTiroParalisante(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(TiroParalisanteEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(TiroParalisanteEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private static void AnexarEfeitoNoAlvo(Node2D targetNode, Node2D effect)
    {
        effect.Position = Vector2.Zero;
        effect.ZAsRelative = true;
        targetNode.AddChild(effect);
    }

    private void TocarEfeitoTiroExecucao(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(TiroExecucaoEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(TiroExecucaoEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Position = Vector2.Zero;
        effect.ZIndex = 90;
        effect.ZAsRelative = false;
        targetNode.AddChild(effect);
    }

    private void TocarEfeitoMarcaExecutorPoison(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(MarcaExecutorPoisonEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(MarcaExecutorPoisonEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Position = Vector2.Zero;
        effect.ZIndex = 95;
        effect.ZAsRelative = false;
        targetNode.AddChild(effect);
    }

    private Node2D? ObterNoCombate(ulong entityId)
    {
        if (entityId == _gameNet?.LocalPlayerId)
            return GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;

        return _networkNodes.TryGetValue(entityId, out var node) && IsInstanceValid(node)
            ? node
            : null;
    }

    private void TocarEfeitoGolpesFreneticosNoCaster(ulong attackerId)
    {
        Node2D? casterNode = ObterNoCombate(attackerId);
        if (casterNode == null || !IsInstanceValid(casterNode) || !ResourceLoader.Exists(GolpesFreneticosEffectPath))
            return;

        string key = $"{attackerId}:13102";
        double now = Time.GetTicksMsec() / 1000.0;
        if (_lastCasterSkillEffectAt.TryGetValue(key, out double last) && now - last < 0.7)
            return;

        _lastCasterSkillEffectAt[key] = now;

        var scene = ResourceLoader.Load<PackedScene>(GolpesFreneticosEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Position = Vector2.Zero;
        effect.ZIndex = 109;
        effect.ZAsRelative = true;
        casterNode.AddChild(effect);
    }

    private void TocarProjetilGolpesFreneticos(Node2D casterNode, Vector2 direction)
    {
        if (casterNode == null || !IsInstanceValid(casterNode) || !ResourceLoader.Exists(GolpesFreneticosEffectPath))
            return;

        Vector2 dir = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector2.Down;
        var scene = ResourceLoader.Load<PackedScene>(GolpesFreneticosEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        var parent = casterNode.GetParent() ?? GetTree()?.CurrentScene;
        if (parent == null)
            return;

        Vector2 start = casterNode.GlobalPosition + dir * 42f + new Vector2(0f, -18f);
        Vector2 end = start + dir * 170f;
        effect.GlobalPosition = start;
        effect.Rotation = dir.Angle() + Mathf.Pi;
        effect.ZIndex = casterNode.ZIndex + 1;
        effect.ZAsRelative = false;
        parent.AddChild(effect);
        effect.GlobalPosition = start;

        var tween = effect.CreateTween();
        tween.TweenProperty(effect, "global_position", end, 0.22f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        }));
    }

    private void TocarProjetilCorteRapido(Node2D? casterNode, Node2D targetNode)
    {
        if (casterNode == null || targetNode == null || !IsInstanceValid(casterNode) || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(CorteRapidoEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(CorteRapidoEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 1;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "corte_rapido_projectile";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, true);
        frames.SetAnimationSpeed(animName, 22f);

        for (int col = 0; col < columns; col++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(col * frameWidth, 0, frameWidth, frameHeight)
            };
            frames.AddFrame(animName, atlas);
        }

        Vector2 rawDir = targetNode.GlobalPosition - casterNode.GlobalPosition;
        Vector2 dir = SnapVectorToCardinal(rawDir.LengthSquared() > 0.001f ? rawDir.Normalized() : Vector2.Down);
        Vector2 start = casterNode.GlobalPosition + dir * 34f + new Vector2(0f, -24f);
        Vector2 end = targetNode.GlobalPosition + new Vector2(0f, -24f);
        if ((end - start).LengthSquared() < 16f)
            end = start + dir * 56f;

        var effect = new AnimatedSprite2D
        {
            Name = "CorteRapidoProjectileEffect",
            SpriteFrames = frames,
            GlobalPosition = start,
            Rotation = dir.Angle() + Mathf.Pi,
            Scale = new Vector2(0.92f, 0.92f),
            ZIndex = Mathf.Max(casterNode.ZIndex, targetNode.ZIndex) + 3,
            ZAsRelative = false,
        };

        Node? parent = ObterMundo() ?? casterNode.GetParent() ?? GetTree()?.CurrentScene;
        if (parent == null)
            return;

        parent.AddChild(effect);
        effect.GlobalPosition = start;
        effect.Play(animName);

        float travelTime = Mathf.Clamp(start.DistanceTo(end) / 720f, 0.08f, 0.22f);
        var tween = effect.CreateTween();
        tween.TweenProperty(effect, "global_position", end, travelTime)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        }));
    }

    private static Vector2 SnapVectorToCardinal(Vector2 direction)
    {
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            return direction.X >= 0f ? Vector2.Right : Vector2.Left;

        return direction.Y >= 0f ? Vector2.Down : Vector2.Up;
    }

    private void TocarEfeitoMachadoGiratorioNoCaster(ulong attackerId)
    {
        Node2D? casterNode = ObterNoCombate(attackerId);
        if (casterNode == null || !IsInstanceValid(casterNode) || !ResourceLoader.Exists(MachadoGiratorioEffectPath))
            return;

        string key = $"{attackerId}:13105";
        double now = Time.GetTicksMsec() / 1000.0;
        if (_lastCasterSkillEffectAt.TryGetValue(key, out double last) && now - last < 0.65)
            return;

        _lastCasterSkillEffectAt[key] = now;
        casterNode.SetMeta(MetaMachadoGiratorioUntil, now + 1.65);

        var scene = ResourceLoader.Load<PackedScene>(MachadoGiratorioEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Position = Vector2.Zero;
        effect.ZIndex = 108;
        effect.ZAsRelative = true;
        casterNode.AddChild(effect);
        GirarVisualMachadoGiratorio(casterNode);
    }

    private void GirarVisualMachadoGiratorio(Node2D casterNode)
    {
        if (casterNode == null || !IsInstanceValid(casterNode) || !IsInsideTree())
            return;

        var animatedSprites = new List<AnimatedSprite2D>();
        ColetarAnimatedSprites(casterNode, animatedSprites);
        foreach (var sprite in animatedSprites)
        {
            if (sprite == null || !IsInstanceValid(sprite) || sprite.SpriteFrames == null || sprite.Name.ToString().Contains("Effect"))
                continue;

            string originalAnimation = sprite.Animation.ToString();
            float originalSpeed = sprite.SpeedScale;
            string prefix = ResolverPrefixoGiroMachado(casterNode, sprite);
            string[] directions = { "down", "right", "up", "left", "down", "right", "up", "left", "down", "right", "up", "left" };

            var tween = CreateTween();
            foreach (string direction in directions)
            {
                string animation = ResolverAnimacaoGiroMachado(sprite.SpriteFrames, prefix, direction);
                if (string.IsNullOrWhiteSpace(animation))
                    continue;

                tween.TweenCallback(Callable.From(() =>
                {
                    if (!IsInstanceValid(sprite) || sprite.SpriteFrames == null)
                        return;

                    sprite.Rotation = 0f;
                    sprite.SpeedScale = 1.25f;
                    if (sprite.SpriteFrames.HasAnimation(animation))
                        sprite.Play(animation);
                }));
                tween.TweenInterval(0.12f);
            }

            tween.TweenCallback(Callable.From(() =>
            {
                if (!IsInstanceValid(sprite))
                    return;

                sprite.Rotation = 0f;
                sprite.SpeedScale = originalSpeed;
                if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation(originalAnimation))
                    sprite.Play(originalAnimation);
            }));
        }
    }

    private static string ResolverPrefixoGiroMachado(Node2D casterNode, AnimatedSprite2D sprite)
    {
        if (casterNode.HasMeta("anim_prefix"))
            return casterNode.GetMeta("anim_prefix").AsString();

        string current = sprite.Animation.ToString();
        int attackIndex = current.IndexOf("_attack_", System.StringComparison.OrdinalIgnoreCase);
        if (attackIndex > 0)
            return current[..attackIndex];

        if (casterNode is Player player)
            return ClasseRegistry.ObterPrefixoAtaqueRecomendado(player.NomeDaClasse);

        string[] candidates = { "machado_guerra", "machado", "berserker", "berseker", "guerreiro" };
        foreach (string candidate in candidates)
        {
            if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation($"{candidate}_attack_down"))
                return candidate;
        }

        return "machado_guerra";
    }

    private static string ResolverAnimacaoGiroMachado(SpriteFrames frames, string prefix, string direction)
    {
        string[] candidates =
        {
            $"{prefix}_attack_{direction}",
            $"{prefix}_{direction}_attack",
            $"machado_guerra_attack_{direction}",
            $"machado_{direction}_attack",
            $"attack_{direction}",
        };

        foreach (string candidate in candidates)
        {
            if (frames.HasAnimation(candidate))
                return candidate;
        }

        return "";
    }

    private static void ColetarAnimatedSprites(Node node, List<AnimatedSprite2D> result)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is AnimatedSprite2D sprite)
                result.Add(sprite);
            ColetarAnimatedSprites(child, result);
        }
    }

    private void TocarEfeitoGolpeSombrio(Node2D targetNode, Node2D? attackerNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        var frames = new SpriteFrames();
        const string animName = "golpe_sombrio_hit";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 14f);

        for (int i = 1; i <= 8; i++)
        {
            string path = $"{GolpeSombrioEffectDir}/effect{i}.png";
            if (!ResourceLoader.Exists(path))
                continue;

            var texture = ResourceLoader.Load<Texture2D>(path);
            if (texture != null)
                frames.AddFrame(animName, texture);
        }

        if (frames.GetFrameCount(animName) == 0)
            return;

        float side = attackerNode == null || attackerNode.GlobalPosition.X <= targetNode.GlobalPosition.X ? 1f : -1f;
        var effect = new AnimatedSprite2D
        {
            Name = "GolpeSombrioHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(10f * side, -8f),
            Scale = new Vector2(0.48f * side, 0.48f),
            RotationDegrees = -12f * side,
            ZIndex = 126,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoPunhaladaNasCostas(Node2D targetNode, Node2D? attackerNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(PunhaladaNasCostasEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(PunhaladaNasCostasEffectPath);
        if (texture == null)
            return;

        const int frameSize = 100;
        int columns = texture.GetWidth() / frameSize;
        int rows = texture.GetHeight() / frameSize;
        if (columns <= 0 || rows <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "punhalada_nas_costas_hit";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameSize, row * frameSize, frameSize, frameSize)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        float side = attackerNode == null || attackerNode.GlobalPosition.X <= targetNode.GlobalPosition.X ? 1f : -1f;
        var effect = new AnimatedSprite2D
        {
            Name = "PunhaladaNasCostasHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(6f * side, -18f),
            Scale = new Vector2(1.05f * side, 1.05f),
            RotationDegrees = -8f * side,
            ZIndex = 128,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoEstocada(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        var frames = CriarFramesDeDiretorio(EstocadaEffectDir, "estocada_hit", false, 18f, 12);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = "EstocadaHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -28f),
            Scale = new Vector2(0.85f, 0.85f),
            ZIndex = 132,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play("estocada_hit");
    }

    private void TocarEfeitoGolpeAtordoante(Node2D targetNode, float duration)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        var frames = new SpriteFrames();
        const string animName = "golpe_atordoante_hit";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, true);
        frames.SetAnimationSpeed(animName, 12f);

        for (int i = 1; i <= 8; i++)
        {
            string path = $"{GolpeAtordoanteEffectDir}/effect{i}.png";
            if (!ResourceLoader.Exists(path))
                continue;

            var texture = ResourceLoader.Load<Texture2D>(path);
            if (texture != null)
                frames.AddFrame(animName, texture);
        }

        if (frames.GetFrameCount(animName) == 0)
            return;

        var old = targetNode.GetNodeOrNull<Node2D>("GolpeAtordoanteStunEffect");
        if (old != null && IsInstanceValid(old))
            old.QueueFree();

        var effect = new AnimatedSprite2D
        {
            Name = "GolpeAtordoanteStunEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -72f),
            Scale = new Vector2(0.72f, 0.72f),
            ZIndex = 129,
            ZAsRelative = true,
        };

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.15f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
        timer.Start();
    }

    private void TocarEfeitoCarnificinaImpactoNoChao(Vector2 globalPosition)
    {
        var frames = CriarFramesDeDiretorio(CarnificinaImpactEffectDir, "carnificina_impacto", false, 22f, 24);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = "CarnificinaImpactEffect",
            SpriteFrames = frames,
            GlobalPosition = globalPosition,
            Position = globalPosition,
            Scale = new Vector2(1.05f, 1.05f),
            ZIndex = 130,
            ZAsRelative = false,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        Node? parent = GetTree()?.CurrentScene?.FindChild("World", true, false) ?? GetTree()?.CurrentScene;
        parent?.AddChild(effect);
        effect.GlobalPosition = globalPosition;
        effect.Play("carnificina_impacto");
    }

    private void TocarEfeitoCarnificinaStun(Node2D targetNode, float duration)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        var frames = CriarFramesDeDiretorio(CarnificinaStunEffectDir, "carnificina_stun", true, 8f, 3);
        if (frames == null)
            return;

        var old = targetNode.GetNodeOrNull<Node2D>("CarnificinaStunEffect");
        if (old != null && IsInstanceValid(old))
            old.QueueFree();

        var effect = new AnimatedSprite2D
        {
            Name = "CarnificinaStunEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -74f),
            Scale = new Vector2(0.95f, 0.95f),
            ZIndex = 130,
            ZAsRelative = true,
        };

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.15f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play("carnificina_stun");
        timer.Start();
    }

    private static SpriteFrames? CriarFramesDeDiretorio(string directory, string animName, bool loop, float speed, int maxFrames)
    {
        string cacheKey = $"{directory}|{animName}|{loop}|{speed}|{maxFrames}";
        if (_directoryFrameCache.TryGetValue(cacheKey, out var cachedFrames))
            return cachedFrames;

        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, loop);
        frames.SetAnimationSpeed(animName, speed);

        for (int i = 1; i <= maxFrames; i++)
        {
            string path = $"{directory}/effect{i}.png";
            if (!ResourceLoader.Exists(path))
                continue;

            var texture = ResourceLoader.Load<Texture2D>(path);
            if (texture != null)
                frames.AddFrame(animName, texture);
        }

        SpriteFrames? result = frames.GetFrameCount(animName) > 0 ? frames : null;
        _directoryFrameCache[cacheKey] = result;
        return result;
    }

    private void TocarEfeitoDesafioCaster(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        var frames = CriarFramesDeDiretorio(DesafioCasterEffectDir, "desafio_cast", false, 7f, 16);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = "DesafioCasterEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -38f),
            Scale = new Vector2(1.15f, 1.15f),
            ZIndex = 134,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play("desafio_cast");
    }

    private void TocarEfeitoEspadaEstelar(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(EspadaEstelarEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(EspadaEstelarEffectPath);
        if (texture == null)
            return;

        const int columns = 6;
        const string animName = "espada_estelar_hit";
        float frameWidth = texture.GetWidth() / (float)columns;
        int frameHeight = texture.GetHeight();
        if (frameWidth <= 0f || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 14f);

        for (int col = 0; col < columns; col++)
        {
            float x0 = col * frameWidth;
            float x1 = col == columns - 1 ? texture.GetWidth() : (col + 1) * frameWidth;
            var atlas = new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(x0, 0f, x1 - x0, frameHeight)
            };
            frames.AddFrame(animName, atlas);
        }

        var effect = new AnimatedSprite2D
        {
            Name = "EspadaEstelarHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -42f),
            Scale = new Vector2(1.25f, 1.25f),
            ZIndex = 135,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoEspinhos(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(EspinhosEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(EspinhosEffectPath);
        if (texture == null)
            return;

        const int columns = 10;
        const int rows = 4;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        const string animName = "espinhos_hit";
        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 22f);

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "EspinhosHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, 12f),
            Scale = new Vector2(1.15f, 1.15f),
            ZIndex = 132,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoDesafioMob(Node2D targetNode, float duration)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        const string nodeName = "DesafioMobLoopEffect";
        var existing = targetNode.GetNodeOrNull<Node2D>(nodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();

        var frames = CriarFramesDeDiretorio(DesafioMobEffectDir, "desafio_taunt_loop", true, 12f, 16);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = nodeName,
            SpriteFrames = frames,
            Position = new Vector2(0f, -42f),
            Scale = new Vector2(1.0f, 1.0f),
            ZIndex = 134,
            ZAsRelative = true,
        };

        targetNode.AddChild(effect);
        effect.Play("desafio_taunt_loop");

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.2f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };
        timer.Start();
    }

    private void TocarEfeitoSequenciaMortal(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        const string animName = "sequencia_mortal_hit";
        var frames = CriarFramesDeDiretorio(SequenciaMortalEffectDir, animName, false, 18f, 15);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = "SequenciaMortalHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -32f),
            Scale = new Vector2(0.82f, 0.82f),
            ZIndex = 130,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoExecucaoFinal(Node2D targetNode)
    {
        if (targetNode == null || !IsInstanceValid(targetNode) || !ResourceLoader.Exists(ExecucaoFinalEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(ExecucaoFinalEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 5;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "execucao_final_hit";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 18f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "ExecucaoFinalHitEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -34f),
            Scale = new Vector2(0.82f, 0.82f),
            ZIndex = 130,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        targetNode.AddChild(effect);
        effect.Play(animName);
    }

    private void OnStatusEffect(string effectId, string displayName, bool isDebuff, float duration, int power, string iconPath)
    {
        if (effectId == "stun")
        {
            var stunnedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (stunnedPlayer != null && IsInstanceValid(stunnedPlayer))
                TocarEfeitoGolpeAtordoante(stunnedPlayer, duration);
            return;
        }

        if (effectId == "carnificina_stun")
        {
            var stunnedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (stunnedPlayer != null && IsInstanceValid(stunnedPlayer))
                TocarEfeitoCarnificinaStun(stunnedPlayer, duration);
            return;
        }

        if (effectId == "reflexos_assassinos")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoReflexosAssassinos(buffedPlayer);
            return;
        }

        if (effectId == "arcane_shield")
        {
            var shieldedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (shieldedPlayer != null && IsInstanceValid(shieldedPlayer))
            {
                if (duration > 0f)
                    TocarEfeitoEscudoArcano(shieldedPlayer, duration);
                else
                    RemoverEfeitoEscudoArcano(shieldedPlayer);
            }
            return;
        }

        if (effectId == "escudo_protetor")
        {
            var shieldedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (shieldedPlayer != null && IsInstanceValid(shieldedPlayer))
                TocarEfeitoEscudoProtetor(shieldedPlayer, duration);
            return;
        }

        if (effectId == "furia_elemental")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoFuriaElemental(buffedPlayer);
            return;
        }

        if (effectId == "furia_berserker")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoBuffBerserker(buffedPlayer, FuriaBerserkerEffectPath, "FuriaBerserkerLoopEffect", duration);
            return;
        }

        if (effectId == "frenesi_berserker")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoBuffBerserker(buffedPlayer, FrenesiBerserkerEffectPath, "FrenesiBerserkerLoopEffect", duration);
            return;
        }

        if (effectId == "deus_da_guerra")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoBuffBerserker(buffedPlayer, DeusDaGuerraEffectPath, "DeusDaGuerraLoopEffect", duration);
            return;
        }

        if (effectId == "sangue_de_ferro")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoBuffBerserker(buffedPlayer, SangueDeFerroEffectPath, "SangueDeFerroLoopEffect", duration);
            return;
        }

        if (effectId == "sede_de_sangue")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoSedeDeSangue(buffedPlayer);
            return;
        }

        if (effectId == "forca_brutal")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoForcaBrutal(buffedPlayer);
            return;
        }

        if (effectId == "fortificacao")
        {
            var fortifiedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (fortifiedPlayer != null && IsInstanceValid(fortifiedPlayer))
                TocarEfeitoFortificacao(fortifiedPlayer, duration);
            return;
        }

        if (effectId == "muralha_inabalavel")
        {
            var fortifiedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (fortifiedPlayer != null && IsInstanceValid(fortifiedPlayer))
                TocarEfeitoMuralhaInabalavel(fortifiedPlayer, duration);
            return;
        }

        if (effectId == "berserk")
        {
            var buffedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (buffedPlayer != null && IsInstanceValid(buffedPlayer))
                TocarEfeitoBerserk(buffedPlayer);
            return;
        }

        if (effectId == "purificacao")
        {
            var purifiedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (purifiedPlayer != null && IsInstanceValid(purifiedPlayer))
                TocarEfeitoPurificacao(purifiedPlayer);
            return;
        }

        if (effectId == "bencao_sagrada")
        {
            var blessedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (blessedPlayer != null && IsInstanceValid(blessedPlayer))
                TocarEfeitoBencaoSagrada(blessedPlayer, duration);
            return;
        }

        if (effectId == "bencao_divina")
        {
            var blessedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (blessedPlayer != null && IsInstanceValid(blessedPlayer))
                TocarEfeitoBencaoSagrada(blessedPlayer, duration);
            return;
        }

        if (effectId != "boss_slime_slow")
            return;

        var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
        if (localPlayer != null && IsInstanceValid(localPlayer))
            TocarEfeitoBossSlimeSlow(localPlayer);
    }

    private void OnSkillVisualEffect(ulong casterId, ulong targetId, int skillId, float x, float y, float duration)
    {
        Node2D? targetNode = ObterNoVisualDeSkill(targetId);
        Node2D? casterNode = ObterNoVisualDeSkill(casterId);

        // Evita tocar duas vezes no proprio cliente quando o StatusEffect local ja cobre o buff.
        if (targetId == _gameNet?.LocalPlayerId && casterId == targetId && SkillVisualJaCobertaLocalmente(skillId))
            return;

        if (targetNode == null || !IsInstanceValid(targetNode))
            targetNode = casterNode;
        if (targetNode == null || !IsInstanceValid(targetNode))
            return;

        switch (skillId)
        {
            case 12208:
                TocarEfeitoReflexosAssassinos(targetNode);
                break;
            case 11208:
                TocarEfeitoFuriaElemental(targetNode);
                break;
            case 13101:
                TocarEfeitoBuffBerserker(targetNode, FuriaBerserkerEffectPath, "FuriaBerserkerLoopEffect", duration);
                break;
            case 13106:
                TocarEfeitoBuffBerserker(targetNode, FrenesiBerserkerEffectPath, "FrenesiBerserkerLoopEffect", duration);
                break;
            case 13103:
                TocarEfeitoSedeDeSangue(targetNode);
                break;
            case 13104:
                TocarEfeitoForcaBrutal(targetNode);
                break;
            case 13109:
                TocarEfeitoBuffBerserker(targetNode, DeusDaGuerraEffectPath, "DeusDaGuerraLoopEffect", duration);
                break;
            case 13108:
                TocarEfeitoBerserk(targetNode);
                break;
            case 13003:
                TocarEfeitoBuffBerserker(targetNode, SangueDeFerroEffectPath, "SangueDeFerroLoopEffect", duration);
                break;
            case 14107:
                TocarEfeitoFortificacao(targetNode, duration);
                break;
            case 14109:
                TocarEfeitoMuralhaInabalavel(targetNode, duration);
                break;
            case 15003:
            case 15102:
                TocarEfeitoBencaoSagrada(targetNode, duration);
                break;
            case 15104:
                TocarEfeitoPurificacao(targetNode);
                break;
            case 11003:
                TocarEfeitoEscudoArcano(targetNode, duration);
                break;
        }
    }

    private Node2D? ObterNoVisualDeSkill(ulong entityId)
    {
        if (entityId == 0)
            return null;

        if (entityId == _gameNet?.LocalPlayerId)
            return GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;

        return _networkNodes.TryGetValue(entityId, out var node) && IsInstanceValid(node) ? node : null;
    }

    private static bool SkillVisualJaCobertaLocalmente(int skillId)
    {
        return skillId is 12208 or 11208 or 13101 or 13106 or 13103 or 13104 or 13109 or 13108 or 13003
            or 14107 or 14109 or 15003 or 15102 or 15104 or 11003;
    }

    private void TocarEfeitoReflexosAssassinos(Node2D playerNode)
    {
        if (playerNode == null || !IsInstanceValid(playerNode) || !ResourceLoader.Exists(ReflexosAssassinosEffectPath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(ReflexosAssassinosEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 6;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "reflexos_assassinos_cast";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 20f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = "ReflexosAssassinosCastEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -34f),
            Scale = new Vector2(0.78f, 0.78f),
            ZIndex = 131,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        playerNode.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarEfeitoEscudoArcano(Node2D playerNode, float duration)
    {
        if (!ResourceLoader.Exists(EscudoArcanoEffectPath))
            return;

        RemoverEfeitoEscudoArcano(playerNode);

        var scene = ResourceLoader.Load<PackedScene>(EscudoArcanoEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Name = ArcaneShieldEffectNodeName;
        effect.Position = Vector2.Zero;
        effect.ZAsRelative = true;
        effect.ZIndex = 100;
        playerNode.AddChild(effect);

        if (duration > 0f)
        {
            var timer = new Timer
            {
                OneShot = true,
                WaitTime = duration,
            };
            effect.AddChild(timer);
            timer.Timeout += () =>
            {
                if (IsInstanceValid(effect))
                    effect.QueueFree();
            };
            timer.Start();
        }
    }

    private static void RemoverEfeitoEscudoArcano(Node2D playerNode)
    {
        var existing = playerNode.GetNodeOrNull<Node2D>(ArcaneShieldEffectNodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();
    }

    private void TocarEfeitoEscudoProtetor(Node2D playerNode, float duration)
    {
        if (playerNode == null || !IsInstanceValid(playerNode) || !ResourceLoader.Exists(EscudoProtetorEffectPath))
            return;

        const string nodeName = "EscudoProtetorLoopEffect";
        var existing = playerNode.GetNodeOrNull<Node2D>(nodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();

        var texture = ResourceLoader.Load<Texture2D>(EscudoProtetorEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 5;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "escudo_protetor_loop";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, true);
        frames.SetAnimationSpeed(animName, 16f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = nodeName,
            SpriteFrames = frames,
            Position = new Vector2(0f, -20f),
            Scale = new Vector2(1.05f, 1.05f),
            ZIndex = 134,
            ZAsRelative = true,
        };

        playerNode.AddChild(effect);
        effect.Play(animName);

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.2f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };
        timer.Start();
    }

    private void OnShieldUpdate(int currentShield, int maxShield)
    {
        if (currentShield > 0)
            return;

        var shieldedPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
        if (shieldedPlayer != null && IsInstanceValid(shieldedPlayer))
            RemoverEfeitoEscudoArcano(shieldedPlayer);
    }

    private void TocarEfeitoFuriaElemental(Node2D playerNode)
    {
        if (!ResourceLoader.Exists(FuriaElementalEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(FuriaElementalEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(playerNode, effect);
    }

    private void TocarEfeitoInvestidaBrutal(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(InvestidaBrutalImpactEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(InvestidaBrutalImpactEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(targetNode, effect);
    }

    private void TocarEfeitoSedeDeSangue(Node2D playerNode)
    {
        if (!ResourceLoader.Exists(SedeDeSangueEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(SedeDeSangueEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(playerNode, effect);
    }

    private void TocarEfeitoForcaBrutal(Node2D playerNode)
    {
        if (!ResourceLoader.Exists(ForcaBrutalEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(ForcaBrutalEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        AnexarEfeitoNoAlvo(playerNode, effect);
    }

    private void TocarEfeitoFortificacao(Node2D playerNode, float duration)
    {
        if (playerNode == null || !IsInstanceValid(playerNode) || !ResourceLoader.Exists(FortificacaoEffectPath))
            return;

        const string nodeName = "FortificacaoLoopEffect";
        var existing = playerNode.GetNodeOrNull<Node2D>(nodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();

        var texture = ResourceLoader.Load<Texture2D>(FortificacaoEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 5;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "fortificacao_loop";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, true);
        frames.SetAnimationSpeed(animName, 14f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = nodeName,
            SpriteFrames = frames,
            Position = new Vector2(0f, 6f),
            Scale = new Vector2(1.25f, 1.25f),
            ZIndex = 134,
            ZAsRelative = true,
        };

        playerNode.AddChild(effect);
        effect.Play(animName);

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.2f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };
        timer.Start();
    }

    private void TocarEfeitoMuralhaInabalavel(Node2D playerNode, float duration)
    {
        if (playerNode == null || !IsInstanceValid(playerNode) || !ResourceLoader.Exists(MuralhaInabalavelEffectPath))
            return;

        const string nodeName = "MuralhaInabalavelLoopEffect";
        var existing = playerNode.GetNodeOrNull<Node2D>(nodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();

        var texture = ResourceLoader.Load<Texture2D>(MuralhaInabalavelEffectPath);
        if (texture == null)
            return;

        const int columns = 5;
        const int rows = 4;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight() / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        var frames = new SpriteFrames();
        const string animName = "muralha_inabalavel_loop";
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, true);
        frames.SetAnimationSpeed(animName, 14f);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(col * frameWidth, row * frameHeight, frameWidth, frameHeight)
                };
                frames.AddFrame(animName, atlas);
            }
        }

        var effect = new AnimatedSprite2D
        {
            Name = nodeName,
            SpriteFrames = frames,
            Position = new Vector2(0f, 18f),
            Scale = new Vector2(1.35f, 1.35f),
            ZIndex = 135,
            ZAsRelative = true,
        };

        playerNode.AddChild(effect);
        effect.Play(animName);

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = Mathf.Max(0.2f, duration),
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };
        timer.Start();
    }

    private void TocarEfeitoBerserk(Node2D playerNode)
    {
        if (playerNode == null || !IsInstanceValid(playerNode))
            return;

        var frames = CriarFramesDeDiretorio(BerserkEffectDir, "berserk_cast", false, 6f, 8);
        if (frames == null)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = "BerserkCastEffect",
            SpriteFrames = frames,
            Position = new Vector2(0f, -56f),
            Scale = new Vector2(0.82f, 0.82f),
            ZIndex = 135,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        playerNode.AddChild(effect);
        var tween = effect.CreateTween();
        tween.TweenProperty(effect, "position", new Vector2(0f, -112f), 1.25f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        effect.Play("berserk_cast");
    }

    private static void TocarEfeitoBuffBerserker(Node2D playerNode, string effectPath, string nodeName, float duration)
    {
        if (playerNode == null || !IsInstanceValid(playerNode) || !ResourceLoader.Exists(effectPath))
            return;

        var existing = playerNode.GetNodeOrNull<Node2D>(nodeName);
        if (existing != null && IsInstanceValid(existing))
            existing.QueueFree();

        var scene = ResourceLoader.Load<PackedScene>(effectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Name = nodeName;
        AnexarEfeitoNoAlvo(playerNode, effect);

        if (duration <= 0f)
            return;

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = duration,
        };
        effect.AddChild(timer);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };
        timer.Start();
    }

    private void OnBossCast(ulong bossId, string effectId, string skillName, float castSeconds, float effectDuration, bool isBuff)
    {
        if (castSeconds > 0f)
        {
            _bossHPBar?.StartCast(skillName, castSeconds);

            if (_networkNodes.TryGetValue(bossId, out var bossNode) && IsInstanceValid(bossNode))
                TocarBarraCastAcimaDoBoss(bossNode, skillName, castSeconds);
        }

        if (effectDuration > 0f && castSeconds <= 0f)
            _bossHPBar?.AddBossStatus(effectId, skillName, isBuff, effectDuration);
    }

    private void TocarEfeitoBossSlimeSlow(Node2D targetNode)
    {
        if (!ResourceLoader.Exists(BossSlimeSlowEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(BossSlimeSlowEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.Position = Vector2.Zero;
        effect.ZIndex = 95;
        effect.ZAsRelative = false;
        targetNode.AddChild(effect);
    }

    private static void TocarBarraCastAcimaDoBoss(Node2D bossNode, string skillName, float castSeconds)
    {
        if (castSeconds <= 0f)
            return;

        var antiga = bossNode.GetNodeOrNull<Node>("BossCastBarAcima");
        if (antiga != null && IsInstanceValid(antiga))
            antiga.QueueFree();

        var barra = new BossCastBarAcima(skillName, castSeconds)
        {
            Name = "BossCastBarAcima",
            Position = new Vector2(-60, -128),
            ZIndex = 80,
            ZAsRelative = false,
        };
        bossNode.AddChild(barra);
    }

    private void OnEntityDied(ulong entityId, ulong killerId)
    {
        if (_networkNodes.TryGetValue(entityId, out var node) && IsInstanceValid(node))
        {
            if (node is Inimigo inimigo)
            {
                inimigo.TocarMorteVisual();
                EsquecerEntidadeRede(entityId);

                if (_gameNet != null)
                    _gameNet.RemoveEntity(entityId);

                return;
            }

            if (node.IsInGroup("RemotePlayers") || node.Name.ToString().StartsWith("Player_", StringComparison.Ordinal))
            {
                SetRemotePlayerDowned(node);
                return;
            }

            node.QueueFree();
        }
        EsquecerEntidadeRede(entityId);

        if (_gameNet != null)
            _gameNet.RemoveEntity(entityId);
    }

    private void OnGainExp(ulong entityId, int amount, long totalExp)
    {
        if (entityId != _gameNet?.LocalPlayerId) return;

        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player != null)
        {
            var prog = player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
            if (prog != null)
            {
                int nivel = prog.Nivel;
                // Safety: if component still has default level, use pending from server
                if (nivel <= 1 && _gameNet._pendingLevel > 1)
                    nivel = _gameNet._pendingLevel;
                prog.DefinirProgresso(nivel, (int)totalExp);
            }
        }

        var expLabel = new Label
        {
            Text = $"+{amount} XP",
            Position = new Vector2(0, -60),
            ZIndex = 10,
        };
        expLabel.AddThemeFontOverride("font", GetBoldFont());
        expLabel.AddThemeFontSizeOverride("font_size", 18);
        expLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1.0f));
        expLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
        expLabel.AddThemeConstantOverride("outline_size", 4);

        if (player != null)
        {
            player.AddChild(expLabel);
            var tween = CreateTween();
            tween.TweenProperty(expLabel, "position", expLabel.Position + new Vector2(0, -30), 1.0f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(expLabel)) expLabel.QueueFree();
            }));
            tween.Play();
        }
    }

    private void OnLevelUp(ulong entityId, int newLevel, int remainingXp)
    {
        if (entityId != _gameNet?.LocalPlayerId) return;

        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player != null)
        {
            var prog = player.FindChild("LevelProgressionComponent", true, false) as LevelProgressionComponent;
            if (prog != null)
            {
                prog.DefinirProgresso(newLevel, remainingXp);
                prog.EmitSignal(LevelProgressionComponent.SignalName.SubiuDeLevel, newLevel);
            }

            TocarEfeitoLevelUp(player);
        }

    }

    private void TocarEfeitoLevelUp(Player player)
    {
        const string animName = "level_up";
        const string basePath = "res://skills/Animacao/Animacao de Mapa/Level up/effect";

        TocarSomLevelUp(player);

        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, false);
        frames.SetAnimationSpeed(animName, 10f);

        for (int i = 1; i <= 17; i++)
        {
            string path = $"{basePath}{i}.png";
            var texture = CarregarTexturaLevelUp(path);
            if (texture != null)
                frames.AddFrame(animName, texture);
            else
                GD.PrintErr($"[LEVEL UP FX] Frame ausente: {path}");
        }

        if (frames.GetFrameCount(animName) == 0)
            return;

        var effect = new AnimatedSprite2D
        {
            Name = "LevelUpEffect",
            SpriteFrames = frames,
            Position = new Vector2(0, 10),
            Scale = new Vector2(1.15f, 1.15f),
            ZIndex = 20,
            ZAsRelative = true,
        };

        effect.AnimationFinished += () =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        };

        player.AddChild(effect);
        effect.Play(animName);
    }

    private void TocarSomLevelUp(Player player)
    {
        var stream = CarregarAudioLevelUp();
        if (stream == null)
        {
            GD.PrintErr("[LEVEL UP FX] Audio Level_up nao encontrado.");
            return;
        }

        var audio = new AudioStreamPlayer2D
        {
            Name = "LevelUpAudio",
            Stream = stream,
            VolumeDb = -1.5f,
            MaxDistance = 900f,
            Attenuation = 0.25f,
        };

        audio.Finished += () =>
        {
            if (IsInstanceValid(audio))
                audio.QueueFree();
        };

        player.AddChild(audio);
        audio.Play();
    }

    private static AudioStream? CarregarAudioLevelUp()
    {
        string[] paths =
        {
            "res://audio/Level_up.wav",
            "res://Audio/Level_up.wav",
        };

        foreach (string path in paths)
        {
            if (ResourceLoader.Exists(path))
                return ResourceLoader.Load<AudioStream>(path);
        }

        return null;
    }

    private static Texture2D? CarregarTexturaLevelUp(string path)
    {
        if (ResourceLoader.Exists(path))
            return ResourceLoader.Load<Texture2D>(path);

        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (string.IsNullOrWhiteSpace(absolutePath) || !System.IO.File.Exists(absolutePath))
            return null;

        var image = Image.LoadFromFile(absolutePath);
        if (image == null || image.IsEmpty())
            return null;

        return ImageTexture.CreateFromImage(image);
    }

    private void OnStatUpdate(int baseForca, int baseAgilidade, int baseDestreza, int baseInteligencia, int statPoints, int totalForca, int totalAgilidade, int totalDestreza, int totalInteligencia, int maxHealth, int maxMana, int defesaFisica, int defesaMagica, float chanceCritica, float danoCritico, float evasao, float velocidadeMovimento, float velocidadeAtaque, float precisao, float tenacidade, float penetracaoArmadura, float regeneracaoVida, float regeneracaoMana, float rouboVida, float rouboMana, float reducaoCooldown, int danoPvp, int defesaPvp, float bonusExperiencia, float reflexaoDano, float resistenciaControle, int baseVitalidade, int baseSorte, int totalVitalidade, int totalSorte)
    {
        if (_gameNet == null) return;
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip != null)
            equip.ImportarEstado(baseForca, baseAgilidade, baseDestreza, baseInteligencia, statPoints, baseVitalidade, baseSorte);
    }

    private void OnItemUseResult(int health, int maxHealth, int mana, int maxMana, int itemId, float cooldownSeconds)
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;
        player.SetHealthFromServer(health, maxHealth);
        player.SetManaFromServer(mana, maxMana);
    }

    private void OnSkillUseResult(int slotIndex, int skillId, bool success, float cooldownSeconds)
    {
        if (!success)
            return;

        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
        if (player == null || !IsInstanceValid(player))
            return;

        if (skillId == 15101)
            TocarEfeitoCuraMenor(player);
        else if (skillId == 15107)
            TocarEfeitoLuzRestauradora(player);
    }

    private static void SetRemotePlayerDowned(Node2D node)
    {
        if (node == null || !IsInstanceValid(node))
            return;

        if (!node.IsInGroup("PlayersDowned"))
            node.AddToGroup("PlayersDowned");

        node.SetMeta("remote_downed", true);
        var sprite = node.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite?.SpriteFrames == null)
            return;

        if (!sprite.SpriteFrames.HasAnimation("death"))
        {
            sprite.Stop();
            return;
        }

        if (sprite.Animation.ToString() != "death")
        {
            sprite.SpriteFrames.SetAnimationLoop("death", false);
            sprite.SpeedScale = 1f;
            sprite.Play("death");
            SincronizarOverlaysRemotos(node, sprite);
        }

        if (!node.HasMeta("remote_death_lock_connected"))
        {
            node.SetMeta("remote_death_lock_connected", true);
            sprite.AnimationFinished += () =>
            {
                if (node == null || !IsInstanceValid(node) || !node.HasMeta("remote_downed"))
                    return;
                if (sprite == null || !IsInstanceValid(sprite) || sprite.SpriteFrames == null)
                    return;
                if (sprite.Animation.ToString() != "death")
                    return;

                int frameCount = sprite.SpriteFrames.GetFrameCount("death");
                sprite.SpeedScale = 0f;
                sprite.Stop();
                if (frameCount > 0)
                    sprite.Frame = frameCount - 1;
            };
        }
    }

    private static void SetRemotePlayerRevived(Node2D node)
    {
        if (node == null || !IsInstanceValid(node))
            return;

        node.RemoveFromGroup("PlayersDowned");
        if (node.HasMeta("remote_downed"))
            node.RemoveMeta("remote_downed");

        var sprite = node.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite?.SpriteFrames == null)
            return;

        sprite.SpeedScale = 1f;
        if (sprite.SpriteFrames.HasAnimation("idle_down"))
        {
            sprite.Play("idle_down");
            SincronizarOverlaysRemotos(node, sprite);
        }
    }

    public static void UpdateRemoteAnimation(Node2D entity, Vector2 direction, bool moving, bool sprinting = false)
    {
        if (!IsInstanceValid(entity)) return;
        if (entity.IsInGroup("PlayersDowned") || entity.HasMeta("remote_downed"))
            return;
        if (EstaComAnimacaoTravada(entity, MetaMachadoGiratorioUntil))
            return;

        if (entity is Inimigo inimigo)
        {
            inimigo.AtualizarAnimacaoDeRede(direction, moving);
            return;
        }

        var sprite = entity.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite == null || sprite.SpriteFrames == null) return;

        string currentAnim = sprite.Animation.ToString();
        if (currentAnim.Contains("attack") &&
            sprite.Frame < sprite.SpriteFrames.GetFrameCount(currentAnim) - 1)
            return;

        string dirName = DirectionUtil.VectorToCardinal(direction);

        string prefix = "";
        if (entity.HasMeta("anim_prefix"))
            prefix = entity.GetMeta("anim_prefix").AsString();

        string state = moving && direction.LengthSquared() > 0.01f
            ? (sprinting ? "run" : "walk")
            : "idle";
        string targetAnim = $"{state}_{dirName}";

        if (sprite.SpriteFrames.HasAnimation(targetAnim) && currentAnim != targetAnim)
        {
            sprite.Play(targetAnim);
            SincronizarOverlaysRemotos(entity, sprite);
        }
    }

    private static bool EstaComAnimacaoTravada(Node2D entity, string metaName)
    {
        if (!entity.HasMeta(metaName))
            return false;

        double until = entity.GetMeta(metaName).AsDouble();
        double now = Time.GetTicksMsec() / 1000.0;
        if (until > now)
            return true;

        entity.RemoveMeta(metaName);
        return false;
    }

    private static void TriggerRemotePlayerAttack(Node2D entity, Vector2 direction)
    {
        var sprite = entity.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite?.SpriteFrames == null) return;

        string prefix = entity.HasMeta("anim_prefix")
            ? entity.GetMeta("anim_prefix").AsString()
            : "guerreiro";
        string dirName = DirectionUtil.VectorToCardinal(direction);
        string animation = $"{prefix}_attack_{dirName}";
        if (sprite.SpriteFrames.HasAnimation(animation))
        {
            if (prefix.Contains("machado", System.StringComparison.OrdinalIgnoreCase))
                sprite.SpeedScale = dirName is "left" or "right" ? 1.0f : 1.15f;
            else
                sprite.SpeedScale = 2.0f;
            sprite.Play(animation);
            SincronizarOverlaysRemotos(entity, sprite);
            if (!sprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(() => sprite.SpeedScale = 1.0f)))
                sprite.AnimationFinished += () => sprite.SpeedScale = 1.0f;
        }
    }

    private static void TriggerRemotePlayerBackJump(Node2D entity, Vector2 direction)
    {
        var sprite = entity.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite?.SpriteFrames == null) return;

        string dirName = DirectionUtil.VectorToCardinal(direction);
        string animation = ResolverAnimacaoBackJump(sprite.SpriteFrames, dirName);
        if (string.IsNullOrWhiteSpace(animation))
            return;

        sprite.SpriteFrames.SetAnimationLoop(animation, false);
        sprite.SpeedScale = 1.0f;
        sprite.Play(animation);
        SincronizarOverlaysRemotos(entity, sprite);
        if (!sprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(() => sprite.SpeedScale = 1.0f)))
            sprite.AnimationFinished += () => sprite.SpeedScale = 1.0f;
    }

    private static void TriggerRemotePlayerJump(Node2D entity, Vector2 direction)
    {
        var sprite = entity.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite?.SpriteFrames == null) return;

        string dirName = DirectionUtil.VectorToCardinal(direction);
        string animation = $"jump_{dirName}";
        if (!sprite.SpriteFrames.HasAnimation(animation))
            return;

        var animatedSprites = new List<AnimatedSprite2D>();
        ColetarAnimatedSprites(entity, animatedSprites);
        foreach (var animSprite in animatedSprites)
        {
            if (animSprite?.SpriteFrames == null || !animSprite.SpriteFrames.HasAnimation(animation))
                continue;

            animSprite.SpriteFrames.SetAnimationLoop(animation, false);
            animSprite.SpeedScale = 1.15f;
            animSprite.Play(animation);
            if (animSprite.Name == "AnimatedSprite")
                SincronizarOverlaysRemotos(entity, animSprite);
            if (!animSprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(() => animSprite.SpeedScale = 1.0f)))
                animSprite.AnimationFinished += () => animSprite.SpeedScale = 1.0f;
        }
    }

    private static string ResolverAnimacaoBackJump(SpriteFrames frames, string dirName)
    {
        string[] nomes =
        {
            $"salto_tras_{dirName}",
            $"salta_tras_{dirName}",
        };

        foreach (string nome in nomes)
        {
            if (frames.HasAnimation(nome))
                return nome;
        }

        return "";
    }

    public void HandleRemoteAction(ulong entityId, byte actionType, Vector2 direction)
    {
        if (actionType == PlayerActionSummonSherigan)
        {
            HandleSummonSheriganAction(entityId);
            return;
        }
        if (actionType == PlayerActionInvisibility)
        {
            ApplyRemoteInvisibility(entityId, Mathf.Max(1f, direction.X));
            return;
        }
        if (actionType == PlayerActionReveal)
        {
            SetRemoteInvisibility(entityId, false);
            return;
        }
        if (actionType == PlayerActionArcaneTeleportEnter || actionType == PlayerActionArcaneTeleportExit)
        {
            HandleArcaneTeleportAction(entityId, actionType == PlayerActionArcaneTeleportEnter, direction);
            return;
        }

        if (actionType != PlayerActionAttack
            && actionType != PlayerActionBackJump
            && actionType != PlayerActionGolpesFreneticos
            && actionType != PlayerActionCarnificinaJump) return;
        Node2D? entity = ObterNoCombate(entityId);
        if (entity == null || !IsInstanceValid(entity)) return;

        if (direction.LengthSquared() < 0.001f && _lastDirections.TryGetValue(entityId, out var lastDirection))
            direction = lastDirection;
        if (direction.LengthSquared() < 0.001f)
            direction = Vector2.Down;

        if (actionType == PlayerActionGolpesFreneticos)
        {
            TriggerRemotePlayerAttack(entity, direction.Normalized());
            TocarProjetilGolpesFreneticos(entity, direction.Normalized());
        }
        else if (actionType == PlayerActionCarnificinaJump)
        {
            TriggerRemotePlayerJump(entity, direction.Normalized());
        }
        else if (actionType == PlayerActionBackJump)
        {
            CriarRastroRemoto(entity, entity.GlobalPosition, entity.GlobalPosition - direction.Normalized() * 160f);
            TriggerRemotePlayerBackJump(entity, direction.Normalized());
        }
        else
            TriggerRemotePlayerAttack(entity, direction.Normalized());
    }

    private void HandleArcaneTeleportAction(ulong entityId, bool entering, Vector2 direction)
    {
        Node2D? entity = null;
        if (entityId == _gameNet?.LocalPlayerId)
            entity = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
        else if (_networkNodes.TryGetValue(entityId, out var remoteEntity) && IsInstanceValid(remoteEntity))
            entity = remoteEntity;

        if (entity == null || !IsInstanceValid(entity))
            return;

        if (direction.LengthSquared() < 0.001f && _lastDirections.TryGetValue(entityId, out var lastDirection))
            direction = lastDirection;
        if (direction.LengthSquared() < 0.001f)
            direction = Vector2.Down;

        Vector2 dir = direction.Normalized();
        Vector2 portalPosition = entering
            ? entity.GlobalPosition + dir * 40f
            : direction;

        TocarEfeitoTeleporteArcano(portalPosition);
        SetEntityVisualAlpha(entity, entering ? 0f : 1f);
    }

    private void TocarEfeitoTeleporteArcano(Vector2 globalPosition)
    {
        if (!ResourceLoader.Exists(TeleporteArcanoPortalEffectPath))
            return;

        var scene = ResourceLoader.Load<PackedScene>(TeleporteArcanoPortalEffectPath);
        var effect = scene?.Instantiate<Node2D>();
        if (effect == null)
            return;

        effect.GlobalPosition = globalPosition;

        var world = ObterMundo();
        if (world != null)
            world.AddChild(effect);
        else
            AddChild(effect);
    }

    private static void SetEntityVisualAlpha(Node entity, float alpha)
    {
        if (entity is CanvasItem canvas)
        {
            var color = canvas.Modulate;
            color.A = alpha;
            canvas.Modulate = color;
        }

        foreach (var child in entity.GetChildren())
        {
            if (child is CanvasItem childCanvas)
            {
                var color = childCanvas.Modulate;
                color.A = alpha;
                childCanvas.Modulate = color;
            }
        }
    }

    private void CriarRastroRemoto(Node2D entity, Vector2 origem, Vector2 destino)
    {
        if (entity == null || !IsInstanceValid(entity) || !IsInsideTree())
            return;

        var source = entity.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite");
        if (source == null || !source.Visible || source.SpriteFrames == null)
            return;

        float distancia = origem.DistanceTo(destino);
        if (distancia < RemoteDashTrailMinDistance)
            return;

        int count = Mathf.Clamp((int)(distancia / 42f), 3, RemoteDashTrailGhostCount);
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            Vector2 pos = origem.Lerp(destino, t);
            float alpha = Mathf.Lerp(0.42f, 0.14f, t);
            CriarGhostRemoto(entity, source, pos, alpha, i * 0.035f);
        }
    }

    private void CriarGhostRemoto(Node2D entity, AnimatedSprite2D source, Vector2 globalPosition, float alpha, float delay)
    {
        string anim = source.Animation.ToString();
        if (string.IsNullOrWhiteSpace(anim) || source.SpriteFrames == null || !source.SpriteFrames.HasAnimation(anim))
            return;

        int frameCount = source.SpriteFrames.GetFrameCount(anim);
        if (frameCount <= 0)
            return;

        int frame = Mathf.Clamp(source.Frame, 0, frameCount - 1);
        Texture2D texture = source.SpriteFrames.GetFrameTexture(anim, frame);
        if (texture == null)
            return;

        var ghost = new Node2D
        {
            Name = "RemoteDashTrailGhost",
            GlobalPosition = globalPosition,
            ZIndex = Mathf.Max(-1, entity.ZIndex - 1),
            ZAsRelative = false,
            Modulate = new Color(1f, 1f, 1f, alpha),
        };

        var sprite = new Sprite2D
        {
            Name = "AnimatedSprite_Ghost",
            Texture = texture,
            Position = source.Position,
            Scale = source.Scale,
            Offset = source.Offset,
            Centered = source.Centered,
            FlipH = source.FlipH,
            FlipV = source.FlipV,
            ZIndex = source.ZIndex,
            ZAsRelative = source.ZAsRelative,
            Modulate = source.Modulate,
        };
        ghost.AddChild(sprite);

        var parent = entity.GetParent();
        if (parent == null)
            return;

        parent.AddChild(ghost);
        var tween = ghost.CreateTween();
        if (delay > 0f)
            tween.TweenInterval(delay);
        tween.TweenProperty(ghost, "modulate:a", 0f, RemoteDashTrailDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(ghost))
                ghost.QueueFree();
        }));
    }

    private void ApplyRemoteInvisibility(ulong entityId, float duration)
    {
        SetRemoteInvisibility(entityId, true);

        var timer = new Timer
        {
            OneShot = true,
            WaitTime = duration,
        };
        AddChild(timer);
        timer.Timeout += () =>
        {
            SetRemoteInvisibility(entityId, false);
            timer.QueueFree();
        };
        timer.Start();
    }

    private void SetRemoteInvisibility(ulong entityId, bool invisible)
    {
        float alpha = invisible ? 0f : 1f;
        if (invisible)
        {
            var localTargetOwner = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            localTargetOwner?.LimparTargetSeFor(entityId);
        }

        if (entityId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            localPlayer?.ApplyInvisibilityVisual(invisible, alpha);
            return;
        }

        if (_networkNodes.TryGetValue(entityId, out var entity) && IsInstanceValid(entity))
            SetRemoteVisualAlpha(entity, alpha);

        if (_remoteSheriganPets.TryGetValue(entityId, out var pet) && IsInstanceValid(pet))
            SetRemoteVisualAlpha(pet, alpha);
    }

    private static void SetRemoteVisualAlpha(Node2D node, float alpha)
    {
        node.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }

    private void HandleSummonSheriganAction(ulong entityId)
    {
        if (entityId != _gameNet?.LocalPlayerId)
        {
            SpawnRemoteSherigan(entityId);
            return;
        }

        var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (localPlayer == null || !IsInstanceValid(localPlayer))
        {
            GameNetwork.Log("[PET] Summon Sherigan recebido, mas Player local nao foi encontrado.");
            return;
        }

        localPlayer.SummonSheriganPet();
    }

    private void SpawnRemoteSherigan(ulong ownerId)
    {
        if (!_networkNodes.TryGetValue(ownerId, out var owner) || !IsInstanceValid(owner))
        {
            GameNetwork.Log($"[PET] Summon Sherigan remoto recebido, mas dono {ownerId} ainda nao existe.");
            return;
        }

        if (_remoteSheriganPets.TryGetValue(ownerId, out var oldPet) && IsInstanceValid(oldPet))
        {
            oldPet.GlobalPosition = owner.GlobalPosition + new Vector2(-48, 32);
            return;
        }

        var pet = new Node2D
        {
            Name = $"SheriganRemote_{ownerId}",
            Scale = new Vector2(1.4f, 1.4f),
            ZIndex = 0,
            ZAsRelative = true,
            YSortEnabled = false,
        };

        var sprite = new AnimatedSprite2D
        {
            Name = "AnimatedSprite2D",
            SpriteFrames = CarregarSheriganFrames(),
            Scale = Vector2.One * 1.1f,
        };
        pet.AddChild(sprite);
        TocarAnimacaoSherigan(sprite, "idle_down");

        var world = ObterMundo();
        if (world != null)
            world.AddChild(pet);
        else
            AddChild(pet);

        pet.GlobalPosition = owner.GlobalPosition + new Vector2(-48, 32);
        _remoteSheriganPets[ownerId] = pet;
    }

    private static SpriteFrames? CarregarSheriganFrames()
    {
        if (!ResourceLoader.Exists(SheriganScenePath))
            return null;

        var scene = ResourceLoader.Load<PackedScene>(SheriganScenePath);
        var temp = scene?.Instantiate();
        var sprite = temp?.FindChild("AnimatedSprite2D", true, false) as AnimatedSprite2D;
        var frames = sprite?.SpriteFrames?.Duplicate(true) as SpriteFrames ?? sprite?.SpriteFrames;
        temp?.QueueFree();
        return frames;
    }

    private static void TocarAnimacaoSherigan(AnimatedSprite2D sprite, string anim)
    {
        if (sprite.SpriteFrames == null)
            return;

        string fallback = anim.ToLowerInvariant().Contains("walk")
            ? anim.Replace("walk_", "Walk_")
            : anim;

        if (sprite.SpriteFrames.HasAnimation(anim))
            sprite.Play(anim);
        else if (sprite.SpriteFrames.HasAnimation(fallback))
            sprite.Play(fallback);
    }

    private static string NormalizarPetAnimPrefix(string animPrefix, string petNome)
    {
        string value = string.IsNullOrWhiteSpace(animPrefix) ? petNome : animPrefix;
        return value.Trim().TrimEnd('_').Replace(" ", "").ToLowerInvariant();
    }

    private static SpriteFrames? CarregarPetFrames(string mobType, string petNome)
    {
        if (mobType == "sherigan")
            return CarregarSheriganFrames();

        string petFile = petNome.Replace(" ", "");
        string[] scenePaths =
        {
            $"res://characters/Inimigos/SpriteInimigo/{petFile}.tscn",
            $"res://characters/Inimigos/SpriteInimigo/{mobType}.tscn",
            $"res://characters/Pets/{petFile}PetFrames.tres",
        };

        foreach (string path in scenePaths)
        {
            if (!ResourceLoader.Exists(path))
                continue;

            if (path.EndsWith(".tres") || path.EndsWith(".res"))
            {
                var frames = ResourceLoader.Load<SpriteFrames>(path);
                if (frames != null && frames.GetAnimationNames().Length > 0)
                    return frames;
                continue;
            }

            var scene = ResourceLoader.Load<PackedScene>(path);
            var temp = scene?.Instantiate();
            var sprite = temp?.FindChild("AnimatedSprite2D", true, false) as AnimatedSprite2D;
            var loadedFrames = sprite?.SpriteFrames?.Duplicate(true) as SpriteFrames ?? sprite?.SpriteFrames;
            temp?.QueueFree();
            if (loadedFrames != null && loadedFrames.GetAnimationNames().Length > 0)
                return loadedFrames;
        }

        return MobSpriteFramesBuilder.GetOrBuild(mobType);
    }

    private static void TocarAnimacaoPet(AnimatedSprite2D sprite, string prefix, string anim)
    {
        if (sprite?.SpriteFrames == null)
            return;

        string[] candidates =
        {
            anim,
            $"{prefix}_{anim}",
            anim.Replace("walk_", "Walk_"),
            $"{prefix}_{anim.Replace("walk_", "Walk_")}",
            anim.Replace("attack_", "Attack_"),
            $"{prefix}_{anim.Replace("attack_", "Attack_")}",
            anim.Replace("idle_", "Idle_"),
            $"{prefix}_{anim.Replace("idle_", "Idle_")}",
        };

        foreach (string candidate in candidates)
        {
            if (sprite.SpriteFrames.HasAnimation(candidate))
            {
                sprite.Play(candidate);
                return;
            }
        }

        var names = sprite.SpriteFrames.GetAnimationNames();
        if (names.Length > 0)
            sprite.Play(names[0]);
    }

    private static void UpdateRemotePetAnimation(Node2D pet, Vector2 direction, bool moving, byte aiState)
    {
        var sprite = pet.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite?.SpriteFrames == null)
            return;

        string prefix = pet.HasMeta("pet_anim_prefix") ? pet.GetMeta("pet_anim_prefix").AsString() : "";
        string dir = direction.LengthSquared() > 0.001f ? DirectionUtil.VectorToCardinal(direction) : "down";
        string anim = aiState == 2 ? $"attack_{dir}" : moving ? $"walk_{dir}" : $"idle_{dir}";
        string current = sprite.Animation.ToString();
        if (current == anim || (!string.IsNullOrWhiteSpace(prefix) && current == $"{prefix}_{anim}"))
            return;

        TocarAnimacaoPet(sprite, prefix, anim);
    }

    private void OnProjectileSpawn(ulong entityId, float originX, float originY, float dirX, float dirY, byte projectileType)
    {
        if (_gameNet == null) return;
        if (projectileType != 2 && entityId != _gameNet.LocalPlayerId && !_networkNodes.TryGetValue(entityId, out var _)) return;

        if (projectileType == 3)
        {
            SpawnSangramentoMortalProjectile(originX, originY, dirX, dirY);
            return;
        }

        string scenePath = projectileType switch
        {
            0 => "res://resources/Projetil/ProjetilArqueiro.tscn",
            1 => "res://resources/Projetil/ProjetilMage.tscn",
            2 => "res://resources/Projetil/ProjetilBossSlime.tscn",
            _ => "res://resources/Projetil/Projetil.tscn",
        };

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null) return;

        var projetil = scene.Instantiate<Node2D>();
        if (projetil == null) return;

        projetil.GlobalPosition = new Vector2(originX, originY);
        var world = ObterMundo();
        if (world != null)
            world.AddChild(projetil);
        else
            GetTree().CurrentScene.AddChild(projetil);

        if (projetil is Projetil proj)
        {
            proj.VisualOnlyOnline = true;
            proj.Speed = projectileType switch
            {
                0 => 780.0f,
                1 => 700.0f,
                2 => 620.0f,
                _ => 680.0f,
            };
            proj.DefinirDirecao(new Vector2(dirX, dirY));
        }
    }

    private void SpawnSangramentoMortalProjectile(float originX, float originY, float dirX, float dirY)
    {
        if (!ResourceLoader.Exists(SangramentoMortalProjectilePath))
            return;

        var texture = ResourceLoader.Load<Texture2D>(SangramentoMortalProjectilePath);
        if (texture == null)
            return;

        const int columns = 6;
        int frameWidth = texture.GetWidth() / columns;
        int frameHeight = texture.GetHeight();
        if (frameWidth <= 0 || frameHeight <= 0)
            return;

        const string animName = "sangramento_mortal_axe";
        var frames = new SpriteFrames();
        frames.AddAnimation(animName);
        frames.SetAnimationLoop(animName, true);
        frames.SetAnimationSpeed(animName, 18f);
        for (int col = 0; col < columns; col++)
        {
            frames.AddFrame(animName, new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(col * frameWidth, 0, frameWidth, frameHeight)
            });
        }

        Vector2 direction = new Vector2(dirX, dirY).Normalized();
        if (direction == Vector2.Zero)
            direction = Vector2.Right;

        var effect = new AnimatedSprite2D
        {
            Name = "SangramentoMortalProjectile",
            SpriteFrames = frames,
            GlobalPosition = new Vector2(originX, originY),
            Scale = new Vector2(0.45f, 0.45f),
            Rotation = direction.Angle(),
            ZIndex = 122,
            ZAsRelative = false,
        };

        var world = ObterMundo();
        if (world != null)
            world.AddChild(effect);
        else
            AddChild(effect);

        effect.Play(animName);
        var tween = CreateTween();
        tween.TweenProperty(effect, "global_position", effect.GlobalPosition + direction * 340f, 0.45f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(effect))
                effect.QueueFree();
        }));
    }

    private void OnLootSpawn(ulong lootId, float x, float y, int itemId, int quantity)
    {
        if (_lootNodes.ContainsKey(lootId)) return;

        var root = new Area2D();
        root.Position = ParaPosicaoVisual(x, y);
        root.Name = $"Loot_{lootId}";
        // Fica acima do chao e abaixo das entidades/personagens.
        root.ZIndex = 0;
        root.ZAsRelative = true;
        root.YSortEnabled = false;
        root.Scale = Vector2.Zero;
        root.SetMeta("loot_id", (long)lootId);
        root.SetMeta("item_id", itemId);
        root.SetMeta("quantity", quantity);

        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = 30f };
        root.AddChild(col);

        Color rarityColor = new Color(1.0f, 0.9f, 0.0f);
        ItemResource? itemRes = null;
        if (itemId > 0 && _gameNet?.ItemDB != null)
        {
            itemRes = _gameNet.ItemDB.GetItem(itemId);
            if (itemRes != null)
                rarityColor = RarityColors.GetValueOrDefault(itemRes.Raridade, rarityColor);
            else
                GameNetwork.LogError($"[LOOT] ItemResource nao encontrado para loot itemId={itemId}, qty={quantity}");
        }

        var visuals = new Node2D();
        visuals.Name = "Visuals";
        visuals.ZIndex = 0;
        visuals.ZAsRelative = true;
        visuals.YSortEnabled = false;
        root.AddChild(visuals);

		Texture2D? iconTexture = itemRes?.Icone ?? CarregarIconeLootFallback(itemId);
		if (iconTexture != null)
		{
			var icon = new Sprite2D
			{
                Name = "ItemIcon",
                Texture = iconTexture,
                Position = new Vector2(0, -12),
                ZIndex = 0,
                ZAsRelative = true,
            };

			float maxSide = Mathf.Max(iconTexture.GetWidth(), iconTexture.GetHeight());
			if (maxSide > 0f)
			{
				float iconScale = itemId == 0 ? 40f / maxSide : 48f / maxSide;
				icon.Scale = new Vector2(iconScale, iconScale);
			}

			visuals.AddChild(icon);
		}
        else
        {
            var fallback = new Label
            {
                Name = "ItemIconFallback",
                Text = "?",
                Position = new Vector2(-14, -34),
                Size = new Vector2(28, 28),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ZIndex = 0,
                ZAsRelative = true,
            };
            fallback.AddThemeFontSizeOverride("font_size", 24);
            fallback.AddThemeColorOverride("font_color", rarityColor);
            fallback.AddThemeConstantOverride("outline_size", 2);
            fallback.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            visuals.AddChild(fallback);
        }

        var labelName = new Label();
        string itemName = itemId == 0 ? "Gold" : (itemRes?.Nome ?? NomeLootFallback(itemId));
        labelName.Text = $"{itemName} x{quantity}";
        labelName.Position = new Vector2(-54, 8);
        labelName.Size = new Vector2(108, 24);
        labelName.HorizontalAlignment = HorizontalAlignment.Center;
        labelName.ZIndex = 0;
        labelName.ZAsRelative = true;
        labelName.AddThemeFontSizeOverride("font_size", 12);
        labelName.AddThemeColorOverride("font_color", rarityColor);
        labelName.AddThemeConstantOverride("outline_size", 2);
        labelName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        visuals.AddChild(labelName);

        var prompt = new Label();
        prompt.Text = "[F]";
        prompt.Name = "LootPrompt";
        prompt.Position = new Vector2(-14, -55);
        prompt.ZIndex = 2;
        prompt.ZAsRelative = true;
        prompt.AddThemeFontSizeOverride("font_size", 20);
        prompt.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.3f));
        prompt.AddThemeConstantOverride("shadow_offset_x", 1);
        prompt.AddThemeConstantOverride("shadow_offset_y", 1);
        prompt.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        prompt.Visible = false;
        visuals.AddChild(prompt);

        ItemTooltip tooltip = GetNodeOrNull<ItemTooltip>("/root/main/UI/ItemTooltip");
        root.MouseEntered += () =>
        {
        if (tooltip != null && itemId > 0 && _gameNet?.ItemDB != null)
                {
                    var res = _gameNet.ItemDB.GetItem(itemId);
                    if (res != null)
                        tooltip.Mostrar(res, root.GetGlobalMousePosition());
                }
            };
            root.MouseExited += () => tooltip?.Esconder();

            root.AddToGroup("Loot");
            var _p4 = ObterMundo();
            if (_p4 != null)
                _p4.AddChild(root);
            else
                AddChild(root);
            _lootNodes[lootId] = root;

            var spawnTween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            spawnTween.TweenProperty(root, "scale", Vector2.One * 1.15f, 0.25f);
            spawnTween.TweenProperty(root, "scale", Vector2.One, 0.1f);
        }

    private static Texture2D? CarregarIconeLootFallback(int itemId)
    {
        string path = itemId switch
        {
            0 => "res://Itens/Incones/Moeda de Gold.png",
            110 => "res://Itens/Incones/Porcao de Vida.png",
            111 => "res://Itens/Incones/Porcao de Mana.png",
            >= 1001 and <= 1021 => "res://Itens/Incones/Arco 1.png",
            >= 1051 and <= 1071 => "res://Itens/Incones/Alvaja de Arqueiro Goblin.png",
            >= 2001 and <= 2021 => "res://Itens/Incones/Adaga 1.png",
            >= 2051 and <= 2071 => "res://Itens/Incones/Adagas de Pirata 1.png",
            >= 3001 and <= 3021 => "res://Itens/Incones/Machado 1.png",
            >= 3051 and <= 3071 => "res://Itens/Incones/Machados Perdisos 1.png",
            >= 4001 and <= 4021 => "res://Itens/Incones/Espada 1.png",
            >= 4051 and <= 4071 => "res://Itens/Incones/Escudo de Goglin.png",
            >= 5001 and <= 5021 => "res://Itens/Incones/Cajado 1.png",
            >= 5051 and <= 5071 => "res://Itens/Incones/Escudo Magico 1.png",
            >= 6001 and <= 6021 => "res://Itens/Incones/Martelo quebrada.png",
            >= 6051 and <= 6071 => "res://Itens/Incones/Escudo de Goglin.png",
            >= 300000 and < 301000 => "res://Itens/Incones/Luva de couro.png",
            _ => "res://Itens/Incones/Bag 3.png",
        };

        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static string NomeLootFallback(int itemId)
    {
        return itemId switch
        {
            110 => "Pocao de Vida",
            111 => "Pocao de Mana",
            >= 1001 and <= 1021 => "Arco",
            >= 1051 and <= 1071 => "Aljava",
            >= 2001 and <= 2021 => "Adaga",
            >= 2051 and <= 2071 => "Adaga Secundaria",
            >= 3001 and <= 3021 => "Machado",
            >= 3051 and <= 3071 => "Bumerangue",
            >= 4001 and <= 4021 => "Espada",
            >= 4051 and <= 4071 => "Escudo",
            >= 5001 and <= 5021 => "Cajado",
            >= 5051 and <= 5071 => "Orbe",
            >= 6001 and <= 6021 => "Martelo",
            >= 6051 and <= 6071 => "Escudo Sagrado",
            >= 300000 and < 301000 => "Equipamento Normal",
            _ => $"Item {itemId}",
        };
    }

        private void OnLojinhaSpawn(ulong lojinhaId, string ownerName, string shopName, string ownerClass, string ownerRace, bool isOpen, float x, float y)
        {
            if (_lojinhaNodes.TryGetValue(lojinhaId, out var oldNode) && IsInstanceValid(oldNode))
                oldNode.QueueFree();
            _lojinhaNodes.Remove(lojinhaId);

            var root = new Area2D();
            root.Position = new Vector2(x, y);
            root.Name = $"Lojinha_{lojinhaId}";
            PrepararEntidadeYSort(root);
            root.SetMeta("lojinha_id", (long)lojinhaId);

            var col = new CollisionShape2D();
            col.Shape = new CircleShape2D { Radius = 30f };
            root.AddChild(col);

            var visuals = new Node2D();
            visuals.Name = "Visuals";
            root.AddChild(visuals);

            var sprite = new AnimatedSprite2D
            {
                Name = "AnimatedSprite",
                Scale = new Vector2(2f, 2f),
            };

            string animPrefix = ClasseRegistry.ObterPrefixoAtaqueRecomendado(ownerClass);
            var resolvedFrames = Player.CriarSpriteFramesParaRacaClasse(ownerRace, ownerClass, out string sheetPath, out _);
            if (resolvedFrames != null)
            {
                sprite.SpriteFrames = resolvedFrames;
                sprite.Play("idle_down");
            }
            else if (!string.IsNullOrWhiteSpace(sheetPath) && ResourceLoader.Exists(sheetPath))
            {
                var sheet = ResourceLoader.Load<Texture2D>(sheetPath);
                if (sheet != null)
                {
                    var frames = LpcSpriteFramesBuilder.Construir(sheet, animPrefix);
                    if (frames != null && frames.GetAnimationNames().Length > 0)
                    {
                        sprite.SpriteFrames = frames;
                        sprite.Play("idle_down");
                    }
                }
            }
            visuals.AddChild(sprite);

            if (sprite.SpriteFrames == null)
            {
                var icon = new Sprite2D
                {
                    Texture = GD.Load<Texture2D>("res://Itens/Incones/Bag 3.png"),
                    Position = new Vector2(0, -12),
                    ZIndex = 0,
                    ZAsRelative = true,
                    Scale = new Vector2(2, 2),
                };
                visuals.AddChild(icon);
            }

            string title = string.IsNullOrWhiteSpace(shopName) ? $"Loja de {ownerName}" : shopName;

            var label = new Label();
            label.Text = title;
            label.Position = new Vector2(-96, -84);
            label.Size = new Vector2(192, 24);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.ZIndex = 0;
            label.ZAsRelative = true;
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", isOpen ? new Color(0.6f, 1.0f, 0.6f) : new Color(1.0f, 0.82f, 0.35f));
            label.AddThemeConstantOverride("outline_size", 3);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            label.AddThemeConstantOverride("shadow_offset_x", 2);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            label.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
            visuals.AddChild(label);

            var status = new Label();
            status.Text = isOpen ? "LOJA ABERTA" : "CONFIGURANDO";
            status.Position = new Vector2(-80, -62);
            status.Size = new Vector2(160, 20);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            status.ZIndex = 0;
            status.ZAsRelative = true;
            status.AddThemeFontSizeOverride("font_size", 12);
            status.AddThemeColorOverride("font_color", isOpen ? new Color(0.35f, 0.9f, 0.45f) : new Color(0.95f, 0.75f, 0.25f));
            status.AddThemeConstantOverride("outline_size", 2);
            status.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            visuals.AddChild(status);

            var prompt = new Label();
            prompt.Text = "[F] Abrir loja";
            prompt.Name = "LojinhaPrompt";
            prompt.Position = new Vector2(-72, -112);
            prompt.Size = new Vector2(144, 26);
            prompt.HorizontalAlignment = HorizontalAlignment.Center;
            prompt.ZIndex = 2;
            prompt.ZAsRelative = true;
            prompt.AddThemeFontSizeOverride("font_size", 18);
            prompt.AddThemeColorOverride("font_color", new Color(0.75f, 1.0f, 0.35f));
            prompt.AddThemeConstantOverride("outline_size", 3);
            prompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            prompt.Visible = false;
            visuals.AddChild(prompt);

            root.InputEvent += (viewport, inputEvent, shapeIdx) =>
            {
                if (inputEvent is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
                {
                    _gameNet?.SendLojinhaOpen(lojinhaId);
                }
            };

            var world = ObterMundo();
            if (world != null)
                world.AddChild(root);
            else
                AddChild(root);
            _lojinhaNodes[lojinhaId] = root;
        }

        private void OnLojinhaDespawn(ulong lojinhaId)
        {
            if (_lojinhaNodes.TryGetValue(lojinhaId, out var node) && IsInstanceValid(node))
                node.QueueFree();
            _lojinhaNodes.Remove(lojinhaId);
        }

        private void OnLootDespawn(ulong lootId)
    {
        if (_lootNodes.TryGetValue(lootId, out var node) && IsInstanceValid(node))
        {
            node.QueueFree();
        }
        _lootNodes.Remove(lootId);
    }

    public void PushRemotePosition(ulong entityId, Vector2 position, Vector2 direction, bool moving, bool sprinting, byte aiState = 0)
    {
        _remoteStates[entityId] = new RemoteState
        {
            Position = ParaPosicaoVisual(position),
            Direction = direction,
            Moving = moving,
            Sprinting = sprinting,
            AIState = aiState,
            Timestamp = Time.GetTicksMsec() / 1000.0,
        };
    }

    public override void _Process(double delta)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        AtualizarInteracaoLojinha();

        List<ulong>? invalidRemoteIds = null;
        foreach (var kvp in _remoteStates)
        {
            if (!_networkNodes.TryGetValue(kvp.Key, out var node))
                continue;

            if (!IsInstanceValid(node))
            {
                invalidRemoteIds ??= new List<ulong>();
                invalidRemoteIds.Add(kvp.Key);
                continue;
            }

            // Nao sobrescrever posicao do proprio jogador local (colisao local e soberana)
            if (kvp.Key == _gameNet?.LocalPlayerId)
                continue;

            RemoteState cur = kvp.Value;

            // Use a direção autoritativa do servidor. O delta é apenas fallback.
            Vector2 computedDir = cur.Direction;
            if (computedDir.LengthSquared() < 0.001f &&
                cur.Moving &&
                _previousPositions.TryGetValue(kvp.Key, out var prevPos))
            {
                Vector2 posDelta = cur.Position - prevPos;
                if (posDelta.LengthSquared() > 25.0f)
                    computedDir = posDelta.Normalized();
            }
            _previousPositions[kvp.Key] = cur.Position;

            // Track last non-zero direction for idle facing
            if (computedDir.LengthSquared() > 0.001f)
                _lastDirections[kvp.Key] = computedDir;

            // Mantem a ultima direcao ao parar, sem zerar o lado para o qual olha.
            Vector2 animDir = computedDir;
            if (animDir.LengthSquared() < 0.001f && _lastDirections.TryGetValue(kvp.Key, out var lastDir))
                animDir = lastDir;

            if (node is Inimigo inimigo)
            {
                inimigo.SetNetworkState(cur.Position, animDir, cur.Moving, cur.AIState);
                AtualizarCorNomeMob(inimigo, cur.AIState);
            }
            else if (node.IsInGroup("RemotePets"))
            {
                bool petMoveuVisualmente = cur.Moving && node.GlobalPosition.DistanceSquaredTo(cur.Position) > 2.25f;
                float lerpWeight = 1.0f - Mathf.Exp(-(float)delta * (petMoveuVisualmente ? 12f : 20f));
                node.Position = node.Position.Lerp(cur.Position, lerpWeight);
                UpdateRemotePetAnimation(node, animDir, petMoveuVisualmente, cur.AIState);
            }
            else
            {
                float distanciaAtualizacao = node.GlobalPosition.DistanceTo(cur.Position);
                if (distanciaAtualizacao >= RemoteDashTrailMinDistance
                    && (!_lastDashTrailAt.TryGetValue(kvp.Key, out var lastTrail) || now - lastTrail >= 0.22))
                {
                    CriarRastroRemoto(node, node.GlobalPosition, cur.Position);
                    _lastDashTrailAt[kvp.Key] = now;
                }

                float lerpWeight = 1.0f - Mathf.Exp(-(float)delta * (cur.Moving ? 15f : 25f));
                node.Position = node.Position.Lerp(cur.Position, lerpWeight);
                UpdateRemotePlayerAnimationCached(kvp.Key, node, animDir, cur.Moving, cur.Sprinting);
                UpdateRemoteSheriganPet(kvp.Key, node, animDir, cur.Moving, delta);
            }
        }

        if (invalidRemoteIds != null)
        {
            foreach (ulong id in invalidRemoteIds)
            {
                _networkNodes.Remove(id);
                _remoteStates.Remove(id);
                _previousPositions.Remove(id);
                _lastDirections.Remove(id);
                _lastDashTrailAt.Remove(id);
                _remotePlayerSprites.Remove(id);
            }
        }

        UpdateVisibilityCulling(delta);
    }

    private void UpdateRemotePlayerAnimationCached(ulong entityId, Node2D entity, Vector2 direction, bool moving, bool sprinting = false)
    {
        if (!IsInstanceValid(entity)) return;
        if (entity.IsInGroup("PlayersDowned") || entity.HasMeta("remote_downed"))
            return;
        if (EstaComAnimacaoTravada(entity, MetaMachadoGiratorioUntil))
            return;

        if (!_remotePlayerSprites.TryGetValue(entityId, out var sprite) || !IsInstanceValid(sprite))
        {
            sprite = entity.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite")
                ?? entity.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
            if (sprite == null)
                return;
            _remotePlayerSprites[entityId] = sprite;
        }

        if (sprite.SpriteFrames == null)
            return;

        string currentAnim = sprite.Animation.ToString();
        if (currentAnim.Contains("attack") &&
            sprite.Frame < sprite.SpriteFrames.GetFrameCount(currentAnim) - 1)
            return;

        string dirName = DirectionUtil.VectorToCardinal(direction);
        string state = moving && direction.LengthSquared() > 0.01f
            ? (sprinting ? "run" : "walk")
            : "idle";
        string targetAnim = $"{state}_{dirName}";

        if (currentAnim == targetAnim || !sprite.SpriteFrames.HasAnimation(targetAnim))
            return;

        sprite.Play(targetAnim);
        SincronizarOverlaysRemotos(entity, sprite);
    }

    private static void SincronizarOverlaysRemotos(Node2D entity, AnimatedSprite2D baseSprite)
    {
        if (entity == null || baseSprite?.SpriteFrames == null)
            return;

        string anim = baseSprite.Animation.ToString();
        int frame = baseSprite.Frame;
        float speed = baseSprite.SpeedScale;
        bool tocando = baseSprite.IsPlaying();

        foreach (var child in entity.GetChildren())
        {
            if (child is not AnimatedSprite2D overlay)
                continue;
            if (!overlay.Name.ToString().StartsWith(RemotePaperdollPrefix, System.StringComparison.Ordinal))
                continue;
            if (!overlay.Visible || overlay.SpriteFrames == null)
                continue;

            string animOverlay = ResolverAnimacaoOverlayRemota(overlay.SpriteFrames, anim);
            if (string.IsNullOrEmpty(animOverlay))
                continue;

            if (overlay.Animation.ToString() != animOverlay)
                overlay.Play(animOverlay);
            else if (tocando && !overlay.IsPlaying())
                overlay.Play();

            int frameCount = overlay.SpriteFrames.GetFrameCount(animOverlay);
            overlay.Frame = frameCount > 0 ? Mathf.Clamp(frame, 0, frameCount - 1) : 0;
            overlay.SpeedScale = speed;

            if (!tocando && overlay.IsPlaying())
                overlay.Stop();
        }
    }

    private static string ResolverAnimacaoOverlayRemota(SpriteFrames frames, string anim)
    {
        if (frames == null || string.IsNullOrWhiteSpace(anim))
            return "";
        if (frames.HasAnimation(anim))
            return anim;

        string dir = ExtrairDirecaoAnimacaoRemota(anim);
        if (string.IsNullOrEmpty(dir))
            return "";

        if ((anim.StartsWith("walk_", System.StringComparison.Ordinal) || anim.Contains("_walk", System.StringComparison.Ordinal)) && frames.HasAnimation($"walk_{dir}"))
            return $"walk_{dir}";
        if ((anim.StartsWith("run_", System.StringComparison.Ordinal) || anim.Contains("_run", System.StringComparison.Ordinal)) && frames.HasAnimation($"run_{dir}"))
            return $"run_{dir}";
        if (anim.StartsWith("idle_", System.StringComparison.Ordinal) && frames.HasAnimation($"idle_{dir}"))
            return $"idle_{dir}";
        if (anim.StartsWith("jump_", System.StringComparison.Ordinal) && frames.HasAnimation($"jump_{dir}"))
            return $"jump_{dir}";
        if (anim.Contains("_attack_", System.StringComparison.Ordinal))
        {
            foreach (StringName name in frames.GetAnimationNames())
            {
                string candidate = name.ToString();
                if (candidate.EndsWith($"_attack_{dir}", System.StringComparison.Ordinal))
                    return candidate;
            }
        }

        return "";
    }

    private static string ExtrairDirecaoAnimacaoRemota(string anim)
    {
        if (anim.EndsWith("_down", System.StringComparison.Ordinal)) return "down";
        if (anim.EndsWith("_up", System.StringComparison.Ordinal)) return "up";
        if (anim.EndsWith("_left", System.StringComparison.Ordinal)) return "left";
        if (anim.EndsWith("_right", System.StringComparison.Ordinal)) return "right";
        if (anim.Contains("_down_", System.StringComparison.Ordinal)) return "down";
        if (anim.Contains("_up_", System.StringComparison.Ordinal)) return "up";
        if (anim.Contains("_left_", System.StringComparison.Ordinal)) return "left";
        if (anim.Contains("_right_", System.StringComparison.Ordinal)) return "right";
        return "";
    }

    private void UpdateRemoteSheriganPet(ulong ownerId, Node2D owner, Vector2 ownerDirection, bool ownerMoving, double delta)
    {
        if (!_remoteSheriganPets.TryGetValue(ownerId, out var pet) || !IsInstanceValid(pet))
            return;

        Vector2 offset = ownerDirection.LengthSquared() > 0.001f
            ? -ownerDirection.Normalized() * 54f + new Vector2(0, 18)
            : new Vector2(-48, 32);
        Vector2 desired = owner.GlobalPosition + offset;
        float lerpWeight = 1.0f - Mathf.Exp(-(float)delta * 12f);
        pet.GlobalPosition = pet.GlobalPosition.Lerp(desired, lerpWeight);

        var sprite = pet.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite == null)
            return;

        string dir = ownerDirection.LengthSquared() > 0.001f
            ? DirectionUtil.VectorToCardinal(ownerDirection)
            : "down";
        string anim = ownerMoving ? $"walk_{dir}" : $"idle_{dir}";
        if (sprite.Animation.ToString() != anim && sprite.Animation.ToString() != anim.Replace("walk_", "Walk_"))
            TocarAnimacaoSherigan(sprite, anim);
    }

    private void AtualizarInteracaoLojinha()
    {
        _lojinhaInteracaoAtual = 0;

        if (_lojinhaNodes.Count == 0)
            return;

        Node2D? player = ObterPlayerLocal();
        if (player == null)
            return;

        float melhorDistancia = 95f;
        ulong melhorLojinha = 0;

        foreach (var kvp in _lojinhaNodes)
        {
            var node = kvp.Value;
            if (!IsInstanceValid(node))
                continue;

            float distancia = player.GlobalPosition.DistanceTo(node.GlobalPosition);
            if (distancia <= melhorDistancia)
            {
                melhorDistancia = distancia;
                melhorLojinha = kvp.Key;
            }
        }

        _lojinhaInteracaoAtual = melhorLojinha;

        foreach (var kvp in _lojinhaNodes)
        {
            var node = kvp.Value;
            if (!IsInstanceValid(node))
                continue;

            var prompt = node.FindChild("LojinhaPrompt", true, false) as Label;
            if (prompt != null)
                prompt.Visible = kvp.Key == melhorLojinha;
        }
    }

    private static void AtualizarCorNomeMob(Inimigo inimigo, byte aiState)
    {
        if (inimigo.GetNodeOrNull<RichTextLabel>("MobNameLabel") is not { } label)
            return;

        bool perseguindoOuAtacando = aiState == 2 || aiState == 3;
        var color = inimigo.IsBoss || perseguindoOuAtacando
            ? new Color(1.0f, 0.22f, 0.18f)
            : Colors.White;
        AplicarEstiloNomeMob(label, color);
    }

    private static void AplicarEstiloNomeMob(RichTextLabel label, Color cor)
    {
        label.AddThemeFontOverride("normal_font", GetBoldFont());
        label.AddThemeFontOverride("bold_font", GetBoldFont());
        label.AddThemeFontSizeOverride("normal_font_size", 20);
        label.AddThemeFontSizeOverride("bold_font_size", 20);
        label.AddThemeColorOverride("default_color", cor);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", 4);
    }

    private static void AplicarEstiloNomeMob(Label label, Color cor)
    {
        label.AddThemeFontOverride("font", GetBoldFont());
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", cor);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", 4);
    }

    private static void AplicarEstiloNomeNpc(Label label)
    {
        label.AddThemeFontOverride("font", GetBoldFont());
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 0.86f, 0.16f, 1f));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1f));
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
    }

    private static bool EhMobPassivoVisual(string mobType)
    {
        return mobType is "slime" or "slimeElite" or "cogumelo" or "cogumeloElite" or "plantaCarnivora" or "plantaCarnivoraElite";
    }

    private Node2D? ObterPlayerLocal()
    {
        if (_gameNet != null
            && _gameNet.LocalPlayerId != 0
            && _networkNodes.TryGetValue(_gameNet.LocalPlayerId, out var networkPlayer)
            && IsInstanceValid(networkPlayer))
            return networkPlayer;

        return GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
    }

    private void UpdateVisibilityCulling(double delta)
    {
        _visibilityCullAccumulator += delta;
        if (_visibilityCullAccumulator < VisibilityCullInterval)
            return;

        _visibilityCullAccumulator = 0;

        if (!TryGetCameraWorldRect(out var visibleRect))
            return;

        foreach (var kvp in _networkNodes)
        {
            var node = kvp.Value;
            if (!IsInstanceValid(node))
                continue;

            if (kvp.Key == _gameNet?.LocalPlayerId)
                continue;

            ApplyVisibilityCull(node, visibleRect.HasPoint(node.GlobalPosition));
        }

        foreach (var kvp in _lootNodes)
        {
            var node = kvp.Value;
            if (!IsInstanceValid(node))
                continue;

            ApplyVisibilityCull(node, visibleRect.HasPoint(node.GlobalPosition));
        }
    }

    private bool TryGetCameraWorldRect(out Rect2 rect)
    {
        rect = default;

        var viewport = GetViewport();
        if (viewport == null)
            return false;

        Vector2 viewportSize = viewport.GetVisibleRect().Size;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            return false;

        var camera = viewport.GetCamera2D();
        Vector2 center;
        Vector2 zoom = Vector2.One;

        if (camera != null && IsInstanceValid(camera))
        {
            center = camera.GlobalPosition;
            zoom = new Vector2(
                Mathf.IsZeroApprox(camera.Zoom.X) ? 1f : camera.Zoom.X,
                Mathf.IsZeroApprox(camera.Zoom.Y) ? 1f : camera.Zoom.Y);
        }
        else
        {
            var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
            if (player == null || !IsInstanceValid(player))
                return false;

            center = player.GlobalPosition;
        }

        Vector2 worldSize = viewportSize / zoom;
        rect = new Rect2(center - worldSize * 0.5f, worldSize).Grow(VisibilityCullMargin);
        return true;
    }

    private static void ApplyVisibilityCull(Node2D node, bool shouldBeVisible)
    {
        if (node.Visible == shouldBeVisible)
            return;

        node.Visible = shouldBeVisible;
        foreach (var child in node.FindChildren("*", "AnimatedSprite2D", true, false))
        {
            if (child is not AnimatedSprite2D sprite)
                continue;

            if (shouldBeVisible)
                PlaySpriteSafely(sprite);
            else
                sprite.Stop();
        }
    }

    private static void PlaySpriteSafely(AnimatedSprite2D sprite)
    {
        SpriteFrames frames = sprite.SpriteFrames;
        if (frames == null)
            return;

        string current = sprite.Animation.ToString();
        if (!string.IsNullOrWhiteSpace(current) && frames.HasAnimation(current))
        {
            sprite.Play(current);
            return;
        }

        var names = frames.GetAnimationNames();
        if (names.Length > 0)
            sprite.Play(names[0]);
    }

    public void ClearAll()
    {
        foreach (var kvp in _networkNodes)
        {
            if (IsInstanceValid(kvp.Value))
                kvp.Value.QueueFree();
        }
        _networkNodes.Clear();

        foreach (var kvp in _lootNodes)
        {
            if (IsInstanceValid(kvp.Value))
                kvp.Value.QueueFree();
        }
        _lootNodes.Clear();

        foreach (var kvp in _remoteSheriganPets)
        {
            if (IsInstanceValid(kvp.Value))
                kvp.Value.QueueFree();
        }
        _remoteSheriganPets.Clear();

        _remoteStates.Clear();
        _lastDirections.Clear();
        _previousPositions.Clear();
        _lastDashTrailAt.Clear();
        _remotePlayerSprites.Clear();
        _pendingSpawns.Clear();
        _worldNode = null;
        _flushRetryCount = 0;
        _serverDataApplyRetryCount = 0;
        _sceneReady = false;
    }

    public void RemoveNetworkEntity(ulong entityId)
    {
        if (_networkNodes.TryGetValue(entityId, out var registered) && IsInstanceValid(registered))
        {
            if (!EstaMorrendoVisualmente(registered))
                registered.QueueFree();
        }

        EsquecerEntidadeRede(entityId);

        // Remove também cópias órfãs criadas por pacotes duplicados de versões antigas.
        var root = GetTree()?.Root;
        if (root == null) return;

        foreach (var candidate in root.FindChildren("*", "Node2D", true, false))
        {
            if (candidate is not Node2D node || !IsInstanceValid(node) || !node.HasMeta("network_id"))
                continue;

            ulong candidateId = node.GetMeta("network_id").AsUInt64();
            if (candidateId == entityId)
            {
                if (!EstaMorrendoVisualmente(node))
                    node.QueueFree();
            }
        }
    }

    private void EsquecerEntidadeRede(ulong entityId)
    {
        _networkNodes.Remove(entityId);
        _remoteStates.Remove(entityId);
        _lastDirections.Remove(entityId);
        _previousPositions.Remove(entityId);
        _lastDashTrailAt.Remove(entityId);
        _remotePlayerSprites.Remove(entityId);
        _pendingSpawns.RemoveAll(spawn => spawn.EntityId == entityId);
        if (_remoteSheriganPets.TryGetValue(entityId, out var pet) && IsInstanceValid(pet))
            pet.QueueFree();
        _remoteSheriganPets.Remove(entityId);
    }

    private static bool EstaMorrendoVisualmente(Node node)
    {
        return node.HasMeta("dying") && node.GetMeta("dying").AsBool();
    }

    public override void _ExitTree()
    {
        _isExitingTree = true;
        ClearAll();
        if (_gameNet != null)
        {
            _gameNet.OnEntitySpawned -= OnEntitySpawned;
            _gameNet.OnEnterWorld -= OnEnterWorldHandler;
            _gameNet.OnCombatResult -= OnCombatResult;
            _gameNet.OnEntityDied -= OnEntityDied;
            _gameNet.OnGainExp -= OnGainExp;
            _gameNet.OnLevelUp -= OnLevelUp;
		_gameNet.OnEntityHealthUpdate -= OnEntityHealthUpdateHandler;
		_gameNet.OnEntityManaUpdate -= OnEntityManaUpdateHandler;
		_gameNet.OnRespawn -= OnRespawnHandler;
            _gameNet.OnLootSpawn -= OnLootSpawn;
            _gameNet.OnLootDespawn -= OnLootDespawn;
            _gameNet.OnLojinhaSpawn -= OnLojinhaSpawn;
            _gameNet.OnLojinhaDespawn -= OnLojinhaDespawn;
            _gameNet.OnStatUpdate -= OnStatUpdate;
            _gameNet.OnProjectileSpawn -= OnProjectileSpawn;
            _gameNet.OnItemUseResult -= OnItemUseResult;
            _gameNet.OnSkillUseResult -= OnSkillUseResult;
            _gameNet.OnPartyData -= OnPartyDataChanged;
            _gameNet.OnPartyMemberUpdate -= OnPartyMemberChanged;
            _gameNet.OnGuildData -= OnGuildDataChanged;
            _gameNet.OnGuildMemberUpdate -= OnGuildMemberChanged;
            _gameNet.OnGuildCleared -= OnGuildClearedHandler;
            _gameNet.OnStatusEffect -= OnStatusEffect;
            _gameNet.OnShieldUpdate -= OnShieldUpdate;
            _gameNet.OnBossCast -= OnBossCast;
            _gameNet.OnSkillAreaEffect -= OnSkillAreaEffect;
            _gameNet.OnSkillVisualEffect -= OnSkillVisualEffect;
            _gameNet.OnEquipmentVisualUpdate -= OnEquipmentVisualUpdate;
        }
    }

    private static Font _boldFont = null!;
    private static Font GetBoldFont()
    {
        if (_boldFont != null) return _boldFont;
        var fnt = ResourceLoader.Load<Font>("res://fonts/Montserrat-Variable.ttf");
        if (fnt != null)
        {
            var v = new FontVariation();
            v.SetBaseFont(fnt);
            v.SetVariationEmbolden(1.0f);
            _boldFont = v;
        }
        else
        {
            _boldFont = ThemeDB.GetProjectTheme().DefaultFont;
        }
        return _boldFont;
    }

    private sealed partial class BossCastBarAcima : Panel
    {
        private readonly string _skillName;
        private readonly float _total;
        private float _elapsed;
        private ProgressBar? _bar;

        public BossCastBarAcima(string skillName, float castSeconds)
        {
            _skillName = string.IsNullOrWhiteSpace(skillName) ? "Preparando" : skillName;
            _total = Mathf.Max(0.05f, castSeconds);
            CustomMinimumSize = new Vector2(120, 16);
            Size = new Vector2(120, 16);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Ready()
        {
            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.02f, 0.025f, 0.035f, 0.88f),
                BorderColor = new Color(0.1f, 0.55f, 0.95f, 0.95f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
            });

            _bar = new ProgressBar
            {
                MaxValue = _total,
                Value = 0,
                ShowPercentage = false,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _bar.SetAnchorsPreset(LayoutPreset.FullRect);
            _bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
            {
                BgColor = new Color(0.35f, 0.72f, 1f, 0.95f),
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
            });
            _bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.055f, 0.075f, 0.9f),
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
            });
            AddChild(_bar);

            var label = new Label
            {
                Text = _skillName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 2,
            };
            label.SetAnchorsPreset(LayoutPreset.FullRect);
            label.AddThemeFontSizeOverride("font_size", 8);
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 2);
            AddChild(label);
        }

        public override void _Process(double delta)
        {
            _elapsed = Mathf.Min(_total, _elapsed + (float)delta);
            if (_bar != null)
                _bar.Value = _elapsed;

            if (_elapsed >= _total)
                QueueFree();
        }
    }
}
