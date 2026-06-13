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
        ["slime"] = "goblin",
        ["Slime"] = "goblin",
        ["skeleton"] = "goblin",
        ["Esqueleto"] = "goblin",
        ["Demon Lord"] = "minotauro",
        ["boss_demon"] = "minotauro",
    };
    private readonly Dictionary<ulong, Node2D> _networkNodes = new();
    private readonly Dictionary<ulong, Node2D> _lootNodes = new();
    private Node2D? _worldNode;

    private struct RemoteState
    {
        public Vector2 Position;
        public Vector2 Direction;
        public bool Moving;
        public double Timestamp;
    }

    private readonly Dictionary<ulong, RemoteState> _remoteStates = new();
    private readonly Dictionary<ulong, Vector2> _lastDirections = new();
    private readonly Dictionary<ulong, Vector2> _previousPositions = new();
    private const double InterpolationDelay = 0.08;

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
                    GameNetwork.Log($"ObterMundo: encontrado via FindChild em {child.Name}");
                    return _worldNode;
                }
            }

            if (_worldNode == null)
            {
                int rootChildCount = root.GetChildCount();
                GameNetwork.LogError($"ObterMundo: 'World' nao encontrado (Root tem {rootChildCount} filhos)");
            }
        }
        return _worldNode;
    }

    private const string MetaAnimPrefix = "anim_prefix";
    private const string MetaSpritePath = "sprite_path";

    private bool _sceneReady;
    private readonly List<SpawnEvent> _pendingSpawns = new();
    private int _flushRetryCount;

    private struct SpawnEvent
    {
        public ulong EntityId;
        public string EntityType;
        public string Name;
        public float X, Y;
        public int Level, Health, MaxHealth;
        public string Extra1, Extra2, Extra3;
    }

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

        CarregarCenasMob();
    }

    private void OnEnterWorldHandler()
    {
        CallDeferred(nameof(FlushPendingSpawns));
        CallDeferred(nameof(EnviarDropsParaServidor));
    }

    private void EnviarDropsParaServidor()
    {
        if (_gameNet == null) return;

        var mobTypes = new System.Collections.Generic.List<string>();
        var itemIdsList = new System.Collections.Generic.List<int[]>();
        var chancesList = new System.Collections.Generic.List<double[]>();
        var minQtysList = new System.Collections.Generic.List<int[]>();
        var maxQtysList = new System.Collections.Generic.List<int[]>();

        foreach (var kv in _mobScenes)
        {
            if (kv.Value == null) continue;
            var inst = kv.Value.Instantiate<Inimigo>();
            Inimigo.PreencherDropTable(kv.Key, inst.DropTable);
            var drops = inst.DropTable;
            int count = drops.Count;
            if (count == 0) { inst.QueueFree(); continue; }

            var ids = new int[count];
            var ch = new double[count];
            var min = new int[count];
            var max = new int[count];
            for (int i = 0; i < count; i++)
            {
                ids[i] = drops[i].ItemId;
                ch[i] = drops[i].Chance;
                min[i] = drops[i].MinQty;
                max[i] = drops[i].MaxQty;
            }
            mobTypes.Add(kv.Key);
            itemIdsList.Add(ids);
            chancesList.Add(ch);
            minQtysList.Add(min);
            maxQtysList.Add(max);
            inst.QueueFree();
        }

        if (mobTypes.Count > 0)
        {
            _gameNet.SendMobDropConfig(
                mobTypes.ToArray(),
                itemIdsList.ToArray(),
                chancesList.ToArray(),
                minQtysList.ToArray(),
                maxQtysList.ToArray()
            );
            GameNetwork.Log($"Enviados {mobTypes.Count} configs de drops para o servidor");
        }
    }

    private void FlushPendingSpawns()
    {
        try
        {
            var world = ObterMundo();

            // Se World nao encontrado, criar fallback
            if (world == null)
            {
                var tree = GetTree();
                if (tree?.CurrentScene != null)
                {
                    var fallback = new Node2D();
                    fallback.Name = "World";
                    tree.CurrentScene.AddChild(fallback);
                    _worldNode = fallback;
                    world = fallback;
                    GameNetwork.Log("FlushPendingSpawns: World fallback criado em CurrentScene");
                }
                else
                {
                    var fallback = new Node2D();
                    fallback.Name = "World";
                    AddChild(fallback);
                    _worldNode = fallback;
                    world = fallback;
                    GameNetwork.Log("FlushPendingSpawns: World fallback criado no EntityManager");
                }
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
                GameNetwork.Log("HUD nao encontrado");
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
                        GameNetwork.Log("Player nao encontrado no World para posicionar");
                }
            }

            _gameNet?.ApplyPendingInventory();

            foreach (var s in _pendingSpawns)
                ProcessSpawn(s.EntityId, s.EntityType, s.Name, s.X, s.Y, s.Level, s.Health, s.MaxHealth, s.Extra1, s.Extra2, s.Extra3);
            _pendingSpawns.Clear();

            // Fechar tela de carregamento
            var loading = GetTree()?.Root.GetNodeOrNull("LoadingScreen");
            if (loading != null)
                loading.QueueFree();
        }
        catch (System.Exception ex)
        {
            GameNetwork.LogError($"Erro em FlushPendingSpawns", ex.ToString());
            CallDeferred(nameof(FlushPendingSpawns));
        }
    }

    private void OnEntitySpawned(ulong entityId, string entityType, string name, float x, float y, int level, int health, int maxHealth, string extraData1, string extraData2, string extraData3)
    {
        if (_networkNodes.ContainsKey(entityId))
        {
            GameNetwork.Log($"OnEntitySpawned: ignorando duplicado {entityType} '{name}' ({entityId})");
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
        GD.Print($"[EntityManager] ProcessSpawn: type={entityType} name={name} id={entityId}");

        Node2D? node = entityType switch
        {
            "player" => CreatePlayerEntity(entityId, name, x, y, level, health, maxHealth, extraData1, extraData2),
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

    private Node2D CreatePlayerEntity(ulong entityId, string name, float x, float y, int level, int health, int maxHealth, string characterClass, string race)
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
        root.SetMeta("network_id", entityId);

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

        var labelName = new Label
        {
            Text = name,
            Position = new Vector2(-30, -60),
            ZIndex = 2,
        };
        labelName.AddThemeFontSizeOverride("font_size", 14);
        labelName.AddThemeColorOverride("font_color", Colors.White);
        labelName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        labelName.AddThemeConstantOverride("outline_size", 2);
        root.AddChild(labelName);

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

    private void CarregarCenasMob()
    {
        _inimigoScene = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Inimigo.tscn");
        _mobScenes["goblin"] = _inimigoScene;
        _mobScenes["lobo"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Lobo.tscn");
        _mobScenes["porco"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Porco.tscn");
        _mobScenes["minotauro"] = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Minotauro.tscn");
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
            inimigo.NomeDoInimigo = name;
            inimigo.VidaMaxima = maxHealth;
            inimigo.SetVidaAtual(health, maxHealth);
            inimigo.IsBoss = isBoss;
            inimigo.MobType = mobType;
            inimigo.AnimPrefix = MobSpriteFramesBuilder.ObterPrefixo(mobType);
            inimigo.SetMeta("network_id", entityId);
            inimigo.SetMeta(MetaAnimPrefix, inimigo.AnimPrefix);
            { var _p = ObterMundo(); if (_p != null) _p.AddChild(inimigo); else AddChild(inimigo); }
            return inimigo;
        }

        var placeholder = new Node2D();
        placeholder.Position = new Vector2(x, y);
        placeholder.Name = $"Monster_{entityId}";

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
        Node2D root = null;

        if (existing != null)
        {
            var pos = new Vector2(x, y);
            foreach (Node node in existing)
            {
                if (node is Node2D n2d && IsInstanceValid(n2d))
                {
                    float d = n2d.GlobalPosition.DistanceTo(pos);
                    GD.Print($"[EntityManager]   Verificando '{n2d.Name}' em {n2d.GlobalPosition} dist={d:F1}");
                    if (d < 50f)
                    {
                        n2d.SetMeta("network_id", (long)entityId);
                        n2d.SetMeta("dialog_id", dialogId);
                        n2d.SetMeta(MetaAnimPrefix, animPrefix);
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
                Position = new Vector2(-30, -60),
                ZIndex = 2,
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

        // Trigger attack animation on the attacking mob
        if (attackerId != _gameNet?.LocalPlayerId &&
            _networkNodes.TryGetValue(attackerId, out var attackerNode) &&
            IsInstanceValid(attackerNode) &&
            attackerNode is Inimigo attackerMob &&
            _networkNodes.TryGetValue(targetId, out var animTargetNode) &&
            IsInstanceValid(animTargetNode))
        {
            Vector2 dirToTarget = (animTargetNode.Position - attackerNode.Position).Normalized();
            attackerMob.TriggerAttackAnimation(dirToTarget);
        }

        // Update target health (including local player)
        Node2D targetNode = null;
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

            var damageLabel = new Label
            {
                Text = damage.ToString(),
                Position = new Vector2(-10, -20),
                ZIndex = 10,
            };

            if (isCrit)
            {
                damageLabel.Text = $"{damage}!";
                damageLabel.AddThemeFontSizeOverride("font_size", 26);
                damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.1f));
                damageLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
                damageLabel.AddThemeConstantOverride("outline_size", 3);
            }
            else
            {
                damageLabel.AddThemeFontSizeOverride("font_size", 20);
                damageLabel.AddThemeColorOverride("font_color", Colors.White);
                damageLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
                damageLabel.AddThemeConstantOverride("outline_size", 2);
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

        var expLabel = new Label
        {
            Text = $"+{amount} XP",
            Position = new Vector2(0, -60),
            ZIndex = 10,
        };
        expLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1.0f));

        var playerNode = _gameNet?.GetEntity(entityId);
        if (playerNode != null)
        {
            playerNode.AddChild(expLabel);
            var tween = CreateTween();
            tween.TweenProperty(expLabel, "position", expLabel.Position + new Vector2(0, -30), 1.0f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(expLabel)) expLabel.QueueFree();
            }));
            tween.Play();
        }
    }

    private void OnLevelUp(ulong entityId, int newLevel)
    {
        if (entityId != _gameNet?.LocalPlayerId) return;

        var lvLabel = new Label
        {
            Text = $"LEVEL UP! ({newLevel})",
            Position = new Vector2(-50, -80),
            ZIndex = 10,
            Scale = new Vector2(1.5f, 1.5f),
        };
        lvLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.1f));

        var playerNode = _gameNet?.GetEntity(entityId);
        if (playerNode != null)
        {
            playerNode.AddChild(lvLabel);
            var tween = CreateTween();
            tween.TweenProperty(lvLabel, "position", lvLabel.Position + new Vector2(0, -40), 1.5f);
            tween.TweenProperty(lvLabel, "scale", new Vector2(0.5f, 0.5f), 1.5f);
            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(lvLabel)) lvLabel.QueueFree();
            }));
            tween.Play();
        }
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

    public static void UpdateRemoteAnimation(Node2D entity, Vector2 direction, bool moving)
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
        if (currentAnim.Contains("attack")) return;

        string dirName = DirectionUtil.VectorToCardinal(direction);

        string prefix = "";
        if (entity.HasMeta("anim_prefix"))
            prefix = entity.GetMeta("anim_prefix").AsString();

        string state = moving && direction.LengthSquared() > 0.01f ? "walk" : "idle";
        string targetAnim = $"{prefix}{state}_{dirName}";

        if (sprite.SpriteFrames.HasAnimation(targetAnim) && currentAnim != targetAnim)
            sprite.Play(targetAnim);
    }

    private void OnLootSpawn(ulong lootId, float x, float y, int itemId, int quantity)
    {
        if (_lootNodes.ContainsKey(lootId)) return;

        var root = new Area2D();
        root.Position = new Vector2(x, y);
        root.Name = $"Loot_{lootId}";
        root.SetMeta("loot_id", (long)lootId);
        root.SetMeta("item_id", itemId);
        root.SetMeta("quantity", quantity);

        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = 30f };
        root.AddChild(col);

        var icon = new Label();
        icon.Text = "\u2666";
        icon.Position = new Vector2(-8, -14);
        icon.AddThemeFontSizeOverride("font_size", 28);
        icon.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.0f));
        icon.AddThemeConstantOverride("shadow_offset_x", 1);
        icon.AddThemeConstantOverride("shadow_offset_y", 1);
        icon.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        root.AddChild(icon);

        if (itemId > 0 && _gameNet?.ItemDB != null)
        {
            var itemRes = _gameNet.ItemDB.GetItem(itemId);
            if (itemRes != null)
            {
                var labelName = new Label();
                labelName.Text = $"{itemRes.Nome} x{quantity}";
                labelName.Position = new Vector2(-40, 8);
                labelName.AddThemeFontSizeOverride("font_size", 14);
                labelName.AddThemeColorOverride("font_color", new Color(1, 1, 1));
                labelName.AddThemeConstantOverride("shadow_offset_x", 1);
                labelName.AddThemeConstantOverride("shadow_offset_y", 1);
                labelName.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
                root.AddChild(labelName);
            }
        }

        var prompt = new Label();
        prompt.Text = "[F]";
        prompt.Position = new Vector2(-14, -55);
        prompt.AddThemeFontSizeOverride("font_size", 20);
        prompt.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.3f));
        prompt.AddThemeConstantOverride("shadow_offset_x", 1);
        prompt.AddThemeConstantOverride("shadow_offset_y", 1);
        prompt.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        prompt.Visible = false;
        root.AddChild(prompt);

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

    private void OnLootDespawn(ulong lootId)
    {
        if (_lootNodes.TryGetValue(lootId, out var node) && IsInstanceValid(node))
        {
            node.QueueFree();
        }
        _lootNodes.Remove(lootId);
    }

    public void PushRemotePosition(ulong entityId, Vector2 position, Vector2 direction, bool moving)
    {
        _remoteStates[entityId] = new RemoteState
        {
            Position = position,
            Direction = direction,
            Moving = moving,
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

            // Nao sobrescrever posicao do proprio jogador local (colisao local e soberana)
            if (kvp.Key == _gameNet?.LocalPlayerId)
                continue;

            RemoteState cur = kvp.Value;

            // Computar direcao a partir do delta de posicao (fallback p/ servidores que nao enviam DirX/DirY)
            Vector2 computedDir = cur.Direction;
            if (_previousPositions.TryGetValue(kvp.Key, out var prevPos))
            {
                Vector2 posDelta = cur.Position - prevPos;
                if (posDelta.LengthSquared() > 1.0f)
                    computedDir = posDelta.Normalized();
            }
            _previousPositions[kvp.Key] = cur.Position;

            // Track last non-zero direction for idle facing
            if (computedDir.LengthSquared() > 0.001f)
                _lastDirections[kvp.Key] = computedDir;

            double elapsed = now - cur.Timestamp;

            float lerpWeight = cur.Moving ? (float)Mathf.Min(delta * 30.0, 1.0) : (float)Mathf.Min(delta * 50.0, 1.0);
            node.Position = node.Position.Lerp(cur.Position, lerpWeight);

            // Use last direction for idle facing when current direction is zero
            Vector2 animDir = computedDir;
            if (animDir.LengthSquared() < 0.001f && _lastDirections.TryGetValue(kvp.Key, out var lastDir))
                animDir = lastDir;

            UpdateRemoteAnimation(node, animDir, cur.Moving);
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
        _sceneReady = false;
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
        }
    }
}
