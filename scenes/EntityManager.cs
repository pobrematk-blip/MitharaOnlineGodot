#nullable enable
using Godot;
using System.Collections.Generic;
using Mithara.Network;

public partial class EntityManager : Node
{
    private GameNetwork? _gameNet;
    private PackedScene? _inimigoScene;
    private readonly Dictionary<ulong, Node2D> _networkNodes = new();
    private readonly Dictionary<ulong, Node2D> _lootNodes = new();

    private const string MetaAnimPrefix = "anim_prefix";
    private const string MetaSpritePath = "sprite_path";

    public override void _Ready()
    {
        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet == null) return;

        _gameNet.OnEntitySpawned += OnEntitySpawned;
        _gameNet.OnCombatResult += OnCombatResult;
        _gameNet.OnEntityDied += OnEntityDied;
        _gameNet.OnGainExp += OnGainExp;
        _gameNet.OnLevelUp += OnLevelUp;
        _gameNet.OnLootSpawn += OnLootSpawn;
        _gameNet.OnLootDespawn += OnLootDespawn;

        _inimigoScene = GD.Load<PackedScene>("res://characters/Inimigos/SpriteInimigo/Inimigo.tscn");
    }

    private void OnEntitySpawned(ulong entityId, string entityType, string name, float x, float y, int level, int health, int maxHealth, string extraData1, string extraData2, string extraData3)
    {
        if (_networkNodes.ContainsKey(entityId)) return;

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
        if (entityId == _gameNet?.LocalPlayerId) return null!;

        var root = new Node2D();
        root.Position = new Vector2(x, y);
        root.Name = $"Player_{entityId}";
        root.SetMeta("network_id", entityId);

        string animPrefix = ClasseRegistry.ObterPrefixoAtaqueRecomendado(characterClass);
        root.SetMeta(MetaAnimPrefix, animPrefix);

        var sprite = new AnimatedSprite2D();
        sprite.Name = "AnimatedSprite";
        sprite.ZIndex = 1;
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
                }
            }
        }

        root.AddChild(sprite);

        var labelName = new Label
        {
            Text = name,
            Position = new Vector2(-30, 10),
        };
        root.AddChild(labelName);

        AddChild(root);
        return root;
    }

    private Node2D CreateMonsterEntity(ulong entityId, string name, float x, float y, int level, int health, int maxHealth, string prefabId, bool isBoss)
    {
        if (_inimigoScene != null)
        {
            var inimigo = _inimigoScene.Instantiate<Inimigo>();
            inimigo.Position = new Vector2(x, y);
            inimigo.Name = $"Monster_{entityId}";
            inimigo.NomeDoInimigo = name;
            inimigo.VidaMaxima = maxHealth;
            inimigo.IsBoss = isBoss;
            inimigo.SetMeta("network_id", entityId);
            AddChild(inimigo);
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
            Position = new Vector2(-30, 10),
        };
        placeholder.AddChild(labelName);

        AddChild(placeholder);
        return placeholder;
    }

    private Node2D CreateNpcEntity(ulong entityId, string name, float x, float y, string dialogId, string race, string animPrefix)
    {
        var root = new Area2D();
        root.Position = new Vector2(x, y);
        root.Name = $"NPC_{entityId}";
        root.SetMeta("network_id", entityId);
        root.SetMeta("dialog_id", dialogId);
        root.AddToGroup("NPC");
        root.SetMeta(MetaAnimPrefix, animPrefix);

        var col = new CollisionShape2D();
        col.Shape = new CircleShape2D { Radius = 40f };
        root.AddChild(col);

        var sprite = new AnimatedSprite2D();
        sprite.Name = "AnimatedSprite";
        sprite.ZIndex = 1;

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

        var labelName = new Label
        {
            Text = name,
            Position = new Vector2(-30, 10),
        };
        root.AddChild(labelName);

        var prompt = new Label();
        prompt.Text = "[F] Falar";
        prompt.Name = "InteractPrompt";
        prompt.Position = new Vector2(-20, -55);
        prompt.AddThemeFontSizeOverride("font_size", 18);
        prompt.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.3f));
        prompt.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        prompt.AddThemeConstantOverride("outline_size", 2);
        prompt.Visible = false;
        root.AddChild(prompt);

        AddChild(root);
        return root;
    }

    private void OnCombatResult(ulong attackerId, ulong targetId, int damage, bool isCrit, int targetHealth, int targetMaxHealth)
    {
        if (_networkNodes.TryGetValue(targetId, out var targetNode))
        {
            var damageLabel = new Label
            {
                Text = isCrit ? $"{damage}! CRIT!" : damage.ToString(),
                Position = new Vector2(-10, -20),
                ZIndex = 10,
            };
            if (isCrit)
                damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.8f, 0f));
            else
                damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.1f));

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

    public static void UpdateRemoteAnimation(Node2D entity, Vector2 direction, bool moving)
    {
        if (!IsInstanceValid(entity)) return;

        var sprite = entity.FindChild("AnimatedSprite", true, false) as AnimatedSprite2D;
        if (sprite == null || sprite.SpriteFrames == null) return;

        string currentAnim = sprite.Animation.ToString();
        if (currentAnim.Contains("attack")) return;

        string dirName;
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            dirName = direction.X > 0 ? "right" : "left";
        else
            dirName = direction.Y > 0 ? "down" : "up";

        string targetAnim;
        if (moving && direction.LengthSquared() > 0.01f)
            targetAnim = $"walk_{dirName}";
        else
            targetAnim = $"idle_{dirName}";

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
    }

    public override void _ExitTree()
    {
        ClearAll();
        if (_gameNet != null)
        {
            _gameNet.OnEntitySpawned -= OnEntitySpawned;
            _gameNet.OnCombatResult -= OnCombatResult;
            _gameNet.OnEntityDied -= OnEntityDied;
            _gameNet.OnGainExp -= OnGainExp;
            _gameNet.OnLevelUp -= OnLevelUp;
            _gameNet.OnLootSpawn -= OnLootSpawn;
            _gameNet.OnLootDespawn -= OnLootDespawn;
        }
    }
}
