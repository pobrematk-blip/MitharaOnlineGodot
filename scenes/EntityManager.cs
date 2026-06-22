#nullable enable
using Godot;
using System.Collections.Generic;
using Mithara.Network;

public partial class EntityManager : Node
{
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
    private Node2D? _worldNode;

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
    private const double InterpolationDelay = 0.08;
    private const int PendingMonsterSpawnBatchSize = 8;
    private const float VisibilityCullMargin = 320f;
    private const double VisibilityCullInterval = 0.15;
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
                GameNetwork.LogError($"ObterMundo: 'World' n?o encontrado (Root tem {rootChildCount} filhos)");
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

    private static void PrepararEntidadeYSort(Node2D node)
    {
        node.ZIndex = 0;
        node.ZAsRelative = true;
        node.YSortEnabled = false;
    }

    private const string MetaAnimPrefix = "anim_prefix";
    private const string MetaSpritePath = "sprite_path";

    private bool _sceneReady;
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
        _gameNet.OnRespawn += OnRespawnHandler;
        _gameNet.OnLootSpawn += OnLootSpawn;
        _gameNet.OnLootDespawn += OnLootDespawn;
        _gameNet.OnLojinhaSpawn += OnLojinhaSpawn;
        _gameNet.OnLojinhaDespawn += OnLojinhaDespawn;
        _gameNet.OnStatUpdate += OnStatUpdate;
        _gameNet.OnProjectileSpawn += OnProjectileSpawn;
        _gameNet.OnItemUseResult += OnItemUseResult;

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
    }

    public override void _UnhandledInput(InputEvent @event)
    {
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
                _gameNet._pendingStatPoints);
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
        if (_serverDataApplyRetryCount++ >= 20)
            return;

        CallDeferred(nameof(ApplyServerDataAfterEnterWorld));
    }

    private void FlushPendingSpawns()
    {
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
            GameNetwork.Log($"OnEntitySpawned: ignorando duplicado {entityType} '{name}' ({entityId})");
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
            GameNetwork.Log($"OnEntitySpawned: atualizando spawn pendente {entityType} '{name}' ({entityId})");
            return;
        }

        if (!_sceneReady)
        {
            GameNetwork.Log($"Cena nao pronta, buffering spawn {entityType} '{name}' ({entityId})");
            _pendingSpawns.Add(new SpawnEvent
            {
                EntityId = entityId, EntityType = entityType, Name = name,
                X = x, Y = y, Level = level, Health = health, MaxHealth = maxHealth,
                Extra1 = extraData1, Extra2 = extraData2, Extra3 = extraData3,
            });
            return;
        }

        GameNetwork.Log($"Processando spawn {entityType} '{name}' ({entityId}) em tempo real");
        ProcessSpawn(entityId, entityType, name, x, y, level, health, maxHealth, extraData1, extraData2, extraData3);
    }

    private void ProcessSpawn(ulong entityId, string entityType, string name, float x, float y, int level, int health, int maxHealth, string extraData1, string extraData2, string extraData3)
    {
        if (_networkNodes.ContainsKey(entityId))
            return;

        Node2D? node = entityType switch
        {
            "player" => CreatePlayerEntity(entityId, name, x, y, level, health, maxHealth, extraData1, extraData2, extraData3),
            "monster" or "boss" => CreateMonsterEntity(entityId, name, x, y, level, health, maxHealth, extraData1, entityType == "boss"),
            "npc" => CreateNpcEntity(entityId, name, x, y, extraData1, extraData2, extraData3),
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
            GameNetwork.Log($"CreatePlayerEntity: pulando proprio jogador {name} (ID={entityId})");
            return null!;
        }

        GD.Print($"[EntityManager] Criando entidade de jogador: {name} (ID={entityId}) em ({x:F1}, {y:F1}) classe={characterClass} raca={race}");

        var root = new Node2D();
        root.Position = new Vector2(x, y);
        root.Name = $"Player_{entityId}";
        PrepararEntidadeYSort(root);
        root.SetMeta("network_id", entityId);
        root.SetMeta("player_name", name);

        string animPrefix = ClasseRegistry.ObterPrefixoAtaqueRecomendado(characterClass);
        root.SetMeta(MetaAnimPrefix, animPrefix);

        var sprite = new AnimatedSprite2D();
        sprite.Name = "AnimatedSprite";
        sprite.Scale = new Vector2(2f, 2f);

        string raceFile = (race ?? "Humano").Trim() switch
        {
            "Dark Elfo" => "DarkElfo",
            "Morto Vivo" => "MortoVivo",
            _ => (race ?? "Humano").Replace(" ", "")
        };
        string sheetPath = LpcSpriteFramesBuilder.PastaSpritesRaca + raceFile + ".png";
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
                    GameNetwork.Log($"CreatePlayerEntity: sprite frames carregados para {name} ({raceFile})");
                }
                else
                    GameNetwork.Log($"CreatePlayerEntity: frames vazios para {name} ({raceFile})");
            }
            else
                GameNetwork.Log($"CreatePlayerEntity: sheet nulo para {name} ({sheetPath})");
        }
        else
            GameNetwork.Log($"CreatePlayerEntity: sheet nao existe {sheetPath} para {name}");

        root.AddChild(sprite);

        long xp = 0;
        long xpMax = 1;
        string guildName = "";
        string guildTag = "";
        int guildEmblem = -1;
        if (!string.IsNullOrWhiteSpace(overheadData))
        {
            var data = Json.ParseString(overheadData).AsGodotDictionary();
            xp = (long)data.GetValueOrDefault("xp", 0L);
            xpMax = (long)data.GetValueOrDefault("xp_max", 1L);
            guildName = (string)data.GetValueOrDefault("guild_name", "");
            guildTag = (string)data.GetValueOrDefault("guild_tag", "");
            guildEmblem = (int)data.GetValueOrDefault("guild_emblem", -1);
        }

        var overhead = new OverheadUI
        {
            Name = "OverheadUI_Remoto",
            Position = new Vector2(-60, -90),
            ZIndex = 10,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overhead.ConfigurarRemoto(name, guildName, guildTag, guildEmblem, xp, xpMax);
        root.AddChild(overhead);

        var parent = ObterMundo();
        if (parent != null)
        {
            parent.AddChild(root);
            GameNetwork.Log($"CreatePlayerEntity: {name} adicionado ao World (parent={parent.Name})");
        }
        else
        {
            AddChild(root);
            GameNetwork.Log($"CreatePlayerEntity: {name} adicionado ao EntityManager (World null) - PODE ESTAR INVISIVEL");
        }
        return root;
    }

    public void AtualizarOverheadRemoto(ulong entityId, string nome, string guildName, string guildTag, int guildEmblem, long xp, long xpMax)
    {
        if (!_networkNodes.TryGetValue(entityId, out var node)) return;
        var overhead = node.GetNodeOrNull<OverheadUI>("OverheadUI_Remoto");
        overhead?.AtualizarDadosRemotos(nome, guildName, guildTag, guildEmblem, xp, xpMax);
    }

    private void CarregarCenasMob()
    {
        _inimigoScene = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Inimigo.tscn");
        _mobScenes["goblin"] = _inimigoScene;
        _mobScenes["lobo"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Lobo.tscn");
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
            inimigo.Position = new Vector2(x, y);
            inimigo.Name = $"Monster_{entityId}";
            PrepararEntidadeYSort(inimigo);
            inimigo.NomeDoInimigo = name;
            inimigo.VidaMaxima = maxHealth;
            inimigo.SetVidaAtual(health, maxHealth);
            inimigo.IsBoss = isBoss;
            inimigo.Level = level;
            inimigo.MobType = mobType;
            inimigo.AnimPrefix = MobSpriteFramesBuilder.ObterPrefixo(mobType);
            inimigo.SetMeta("network_id", entityId);
            inimigo.SetMeta(MetaAnimPrefix, inimigo.AnimPrefix);
            inimigo.NetworkTargetPos = new Vector2(x, y);

            string levelTag = isBoss
                ? $"[color=yellow]Lv.{level}[/color] [color=red]BOSS[/color] {name}"
                : $"[color=yellow]Lv.{level}[/color] {name}";
            var labelNome = new RichTextLabel
            {
                Text = levelTag,
                Position = new Vector2(-70, -68),
                ZIndex = 5,
                BbcodeEnabled = true,
                FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.Off,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            labelNome.AddThemeFontSizeOverride("normal_font_size", 18);
            labelNome.AddThemeColorOverride("default_color", Colors.White);
            inimigo.AddChild(labelNome);

            { var _p = ObterMundo(); if (_p != null) _p.AddChild(inimigo); else AddChild(inimigo); }
            return inimigo;
        }

        var placeholder = new Node2D();
        placeholder.Position = new Vector2(x, y);
        placeholder.Name = $"Monster_{entityId}";
        PrepararEntidadeYSort(placeholder);

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
        labelName.AddThemeFontSizeOverride("font_size", 14);
        labelName.AddThemeColorOverride("font_color", Colors.White);
        labelName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        labelName.AddThemeConstantOverride("outline_size", 2);
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

        if (existing != null)
        {
            var pos = new Vector2(x, y);
            foreach (Node node in existing)
            {
                if (node is Node2D n2d && IsInstanceValid(n2d))
                {
                    // Skip if already linked to another NPC
                    if (n2d.HasMeta("network_id"))
                        continue;
                    float d = n2d.GlobalPosition.DistanceTo(pos);
                    GD.Print($"[EntityManager]   Verificando '{n2d.Name}' em {n2d.GlobalPosition} dist={d:F1}");
                    if (d < 5f)
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
            body.Position = new Vector2(x, y);
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

        // Always add sprite, name label and prompt (even for linked nodes)
        if (root.GetNodeOrNull("AnimatedSprite") == null)
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

        if (root.GetNodeOrNull("NameLabel") == null)
        {
            var labelName = new Label
            {
                Text = name,
                Position = new Vector2(-60, -60),
                ZIndex = 2,
                Size = new Vector2(120, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            labelName.Name = "NameLabel";
            labelName.AddThemeFontSizeOverride("font_size", 14);
            labelName.AddThemeColorOverride("font_color", Colors.White);
            labelName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
            labelName.AddThemeConstantOverride("outline_size", 2);
            root.AddChild(labelName);
        }

        if (root.GetNodeOrNull("InteractPrompt") == null)
        {
            var prompt = new Label();
            prompt.Text = "[F] Falar";
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

    private void OnRespawnHandler(ulong entityId, float x, float y, int health, int maxHealth)
    {
        if (!_networkNodes.TryGetValue(entityId, out var node) || !IsInstanceValid(node))
            return;

        node.Position = new Vector2(x, y);

        if (node is Player player)
            player.SetHealthFromServer(health, maxHealth);
        else if (node is Inimigo inimigo)
            inimigo.SetVidaAtual(health, maxHealth);

        if (node.IsInGroup("PlayersDowned"))
        {
            node.RemoveFromGroup("PlayersDowned");
            var sprite = node.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
            if (sprite != null && sprite.SpriteFrames != null)
                sprite.Play("idle_down");
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
        }
        else if (entityId == _gameNet?.LocalPlayerId)
        {
            var localPlayer = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
            if (localPlayer != null && IsInstanceValid(localPlayer))
                localPlayer.SetHealthFromServer(health, maxHealth);
        }
    }

    private void OnCombatResult(ulong attackerId, ulong targetId, int damage, bool isCrit, int targetHealth, int targetMaxHealth)
    {
        GameNetwork.Log($"[COMBAT] OnCombatResult: attacker={attackerId} target={targetId} damage={damage} isCrit={isCrit} targetHP={targetHealth}/{targetMaxHealth}");

        if (attackerId == _gameNet?.LocalPlayerId)
        {
            string msg = isCrit
                ? $"[SISTEMA] Você causou {damage} de dano (CRÍTICO!) em #{targetId}."
                : $"[SISTEMA] Você causou {damage} de dano em #{targetId}.";
            GD.Print(msg);
        }

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
            if (targetNode is Inimigo inimigo)
                inimigo.SetVidaAtual(targetHealth, targetMaxHealth);
            else if (targetNode is Player player)
                player.SetHealthFromServer(targetHealth, targetMaxHealth);
            else if (targetHealth <= 0)
                SetRemotePlayerDowned(targetNode);

            bool isPlayerTakingDamage = targetId == _gameNet?.LocalPlayerId;

            var damageLabel = new Label
            {
                Text = damage.ToString(),
                Position = new Vector2(-10, -20),
                ZIndex = 10,
            };
            damageLabel.AddThemeFontOverride("font", GetBoldFont());

            if (isCrit)
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

    private void OnEntityDied(ulong entityId, ulong killerId)
    {
        if (_networkNodes.TryGetValue(entityId, out var node) && IsInstanceValid(node))
        {
            node.QueueFree();
        }
        _networkNodes.Remove(entityId);

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
        }

    }

    private void OnStatUpdate(int baseForca, int baseAgilidade, int baseDestreza, int baseInteligencia, int statPoints, int totalForca, int totalAgilidade, int totalDestreza, int totalInteligencia, int maxHealth, int maxMana)
    {
        if (_gameNet == null) return;
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip != null)
            equip.ImportarEstado(baseForca, baseAgilidade, baseDestreza, baseInteligencia, statPoints);
    }

    private void OnItemUseResult(int health, int maxHealth, int mana, int maxMana)
    {
        var player = GetTree()?.CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;
        player.SetHealthFromServer(health, maxHealth);
        player.SetManaFromServer(mana, maxMana);
    }

    private static void SetRemotePlayerDowned(Node2D node)
    {
        if (!node.IsInGroup("PlayersDowned"))
        {
            node.AddToGroup("PlayersDowned");
            var sprite = node.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
            if (sprite != null)
                sprite.Stop();
        }
    }

    public static void UpdateRemoteAnimation(Node2D entity, Vector2 direction, bool moving, bool sprinting = false)
    {
        if (!IsInstanceValid(entity)) return;

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
            sprite.Play(targetAnim);
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
            sprite.SpeedScale = 2.0f;
            sprite.Play(animation);
            if (!sprite.IsConnected(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(() => sprite.SpeedScale = 1.0f)))
                sprite.AnimationFinished += () => sprite.SpeedScale = 1.0f;
        }
    }

    public void HandleRemoteAction(ulong entityId, byte actionType, Vector2 direction)
    {
        if (actionType != 1) return;
        if (!_networkNodes.TryGetValue(entityId, out var entity) || !IsInstanceValid(entity)) return;

        if (direction.LengthSquared() < 0.001f && _lastDirections.TryGetValue(entityId, out var lastDirection))
            direction = lastDirection;
        if (direction.LengthSquared() < 0.001f)
            direction = Vector2.Down;

        TriggerRemotePlayerAttack(entity, direction.Normalized());
    }

    private void OnProjectileSpawn(ulong entityId, float originX, float originY, float dirX, float dirY, byte projectileType)
    {
        if (_gameNet == null || entityId == _gameNet.LocalPlayerId) return;
        if (!_networkNodes.TryGetValue(entityId, out var _)) return;

        string scenePath = projectileType switch
        {
            0 => "res://resources/Projetil/ProjetilArqueiro.tscn",
            1 => "res://resources/Projetil/ProjetilMage.tscn",
            _ => "res://resources/Projetil/Projetil.tscn",
        };

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null) return;

        var projetil = scene.Instantiate<Node2D>();
        if (projetil == null) return;

        projetil.GlobalPosition = new Vector2(originX, originY);
        GetTree().CurrentScene.AddChild(projetil);

        if (projetil is Projetil proj)
        {
            proj.DefinirDirecao(new Vector2(dirX, dirY));
        }
    }

    private void OnLootSpawn(ulong lootId, float x, float y, int itemId, int quantity)
    {
        if (_lootNodes.ContainsKey(lootId)) return;

        var root = new Area2D();
        root.Position = new Vector2(x, y);
        root.Name = $"Loot_{lootId}";
        root.ZIndex = -1;
        root.ZAsRelative = true;
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
        }

        var visuals = new Node2D();
        visuals.Name = "Visuals";
        visuals.ZAsRelative = true;
        root.AddChild(visuals);

        Texture2D? iconTexture = itemRes?.Icone ?? GD.Load<Texture2D>("res://Itens/Incones/1.png");
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
                float iconScale = 48f / maxSide;
                icon.Scale = new Vector2(iconScale, iconScale);
            }

            visuals.AddChild(icon);
        }

        var spawnTween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        spawnTween.TweenProperty(root, "scale", Vector2.One * 1.15f, 0.25f);
        spawnTween.TweenProperty(root, "scale", Vector2.One, 0.1f);

        if (itemRes != null)
        {
            var labelName = new Label();
            labelName.Text = $"{itemRes.Nome} x{quantity}";
            labelName.Position = new Vector2(-40, 8);
            labelName.ZIndex = 0;
            labelName.ZAsRelative = true;
            labelName.AddThemeFontSizeOverride("font_size", 14);
            labelName.AddThemeColorOverride("font_color", rarityColor);
            labelName.AddThemeConstantOverride("shadow_offset_x", 1);
            labelName.AddThemeConstantOverride("shadow_offset_y", 1);
            labelName.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
            visuals.AddChild(labelName);
        }

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
        }

        private void OnLojinhaSpawn(ulong lojinhaId, string ownerName, float x, float y)
        {
            if (_lojinhaNodes.ContainsKey(lojinhaId)) return;

            var root = new Area2D();
            root.Position = new Vector2(x, y);
            root.Name = $"Lojinha_{lojinhaId}";
            root.ZIndex = -1;
            root.ZAsRelative = true;
            root.SetMeta("lojinha_id", (long)lojinhaId);

            var col = new CollisionShape2D();
            col.Shape = new CircleShape2D { Radius = 30f };
            root.AddChild(col);

            var visuals = new Node2D();
            visuals.Name = "Visuals";
            root.AddChild(visuals);

            var icon = new Sprite2D
            {
                Texture = GD.Load<Texture2D>("res://Itens/Incones/1.png"),
                Position = new Vector2(0, -12),
                ZIndex = 0,
                ZAsRelative = true,
                Scale = new Vector2(2, 2),
            };
            visuals.AddChild(icon);

            var label = new Label();
            label.Text = $"[{ownerName}]";
            label.Position = new Vector2(-40, 8);
            label.ZIndex = 0;
            label.ZAsRelative = true;
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color(0.6f, 1.0f, 0.6f));
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);
            label.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
            visuals.AddChild(label);

            var prompt = new Label();
            prompt.Text = "[F]";
            prompt.Name = "LojinhaPrompt";
            prompt.Position = new Vector2(-14, -55);
            prompt.ZIndex = 2;
            prompt.ZAsRelative = true;
            prompt.AddThemeFontSizeOverride("font_size", 20);
            prompt.AddThemeColorOverride("font_color", new Color(0.6f, 1.0f, 0.3f));
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
            Position = position,
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

        foreach (var kvp in _remoteStates)
        {
            if (!_networkNodes.TryGetValue(kvp.Key, out var node))
                continue;

            if (!IsInstanceValid(node))
            {
                _networkNodes.Remove(kvp.Key);
                _remoteStates.Remove(kvp.Key);
                _previousPositions.Remove(kvp.Key);
                _lastDirections.Remove(kvp.Key);
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
            }
            else
            {
                float lerpWeight = 1.0f - Mathf.Exp(-(float)delta * (cur.Moving ? 15f : 25f));
                node.Position = node.Position.Lerp(cur.Position, lerpWeight);
                UpdateRemoteAnimation(node, animDir, cur.Moving, cur.Sprinting);
            }
        }

        UpdateVisibilityCulling(delta);
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
                sprite.Play();
            else
                sprite.Stop();
        }
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
        _remoteStates.Clear();
        _lastDirections.Clear();
        _previousPositions.Clear();
        _pendingSpawns.Clear();
        _worldNode = null;
        _flushRetryCount = 0;
        _serverDataApplyRetryCount = 0;
        _sceneReady = false;
    }

    public void RemoveNetworkEntity(ulong entityId)
    {
        if (_networkNodes.TryGetValue(entityId, out var registered) && IsInstanceValid(registered))
            registered.QueueFree();

        _networkNodes.Remove(entityId);
        _remoteStates.Remove(entityId);
        _lastDirections.Remove(entityId);
        _previousPositions.Remove(entityId);
        _pendingSpawns.RemoveAll(spawn => spawn.EntityId == entityId);

        // Remove também cópias órfãs criadas por pacotes duplicados de versões antigas.
        var root = GetTree()?.Root;
        if (root == null) return;

        foreach (var candidate in root.FindChildren("*", "Node2D", true, false))
        {
            if (candidate is not Node2D node || !IsInstanceValid(node) || !node.HasMeta("network_id"))
                continue;

            ulong candidateId = node.GetMeta("network_id").AsUInt64();
            if (candidateId == entityId)
                node.QueueFree();
        }
    }

    public override void _ExitTree()
    {
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
            _gameNet.OnRespawn -= OnRespawnHandler;
            _gameNet.OnLootSpawn -= OnLootSpawn;
            _gameNet.OnLootDespawn -= OnLootDespawn;
            _gameNet.OnLojinhaSpawn -= OnLojinhaSpawn;
            _gameNet.OnLojinhaDespawn -= OnLojinhaDespawn;
            _gameNet.OnStatUpdate -= OnStatUpdate;
            _gameNet.OnProjectileSpawn -= OnProjectileSpawn;
            _gameNet.OnItemUseResult -= OnItemUseResult;
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
}
