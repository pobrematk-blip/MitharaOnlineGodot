using Godot;
using System.Collections.Generic;

public partial class ItemColetavel : Area2D
{
    [Export] public ItemResource ItemContido { get; set; }
    [Export] public Vector2 EscalaSprite { get; set; } = Vector2.One * 2;

    private Label _nameLabel;
    private Label _prompt;
    private bool _playerInRange;
    private bool _wasFPressed;
    private ItemTooltip _tooltip;

    private static readonly Dictionary<Raridade, Color> RarityColors = new()
    {
        [Raridade.Comum] = Color.FromHtml("#ffffff"),
        [Raridade.Incomum] = Color.FromHtml("#1eff00"),
        [Raridade.Raro] = Color.FromHtml("#0070dd"),
        [Raridade.Epico] = Color.FromHtml("#a335ee"),
        [Raridade.Lendario] = Color.FromHtml("#ffcc00"),
        [Raridade.Mistico] = Color.FromHtml("#ff4444"),
    };

    public override void _Ready()
    {
        AddToGroup("Loot");

        Color rarityColor = Color.FromHtml("#ffffff");
        if (ItemContido != null)
            rarityColor = RarityColors.GetValueOrDefault(ItemContido.Raridade, Color.FromHtml("#ffffff"));

        // Loot fica acima do chao, mas abaixo de personagens/mobs/NPCs.
        ZIndex = 0;
        ZAsRelative = true;
        YSortEnabled = false;
        Scale = Vector2.Zero;

        var spawnTween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        spawnTween.TweenProperty(this, "scale", Vector2.One * 1.15f, 0.25f);
        spawnTween.TweenProperty(this, "scale", Vector2.One, 0.1f);

        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null)
        {
            if (ItemContido?.Icone != null)
                sprite.Texture = ItemContido.Icone;

            if (sprite.Texture != null)
            {
                float texW = sprite.Texture.GetWidth();
                float texH = sprite.Texture.GetHeight();
                float scale = 40f / Mathf.Max(texW, texH);
                sprite.Scale = Vector2.One * scale;
            }
            else
            {
                sprite.Scale = EscalaSprite;
            }

            sprite.SelfModulate = rarityColor;

            var floatTween = CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            floatTween.TweenProperty(sprite, "position:y", -6f, 1.2f);
            floatTween.TweenProperty(sprite, "position:y", 0f, 1.2f);
        }

        _nameLabel = new Label();
        _nameLabel.Text = ItemContido?.Nome ?? "Item";
        _nameLabel.Position = new Vector2(-40, -38);
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", rarityColor);
        _nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _nameLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _nameLabel.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        AddChild(_nameLabel);

        _prompt = new Label();
        _prompt.Name = "LootPrompt";
        _prompt.Text = "[F]";
        _prompt.Position = new Vector2(-14, -65);
        _prompt.AddThemeFontSizeOverride("font_size", 20);
        _prompt.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 0.3f));
        _prompt.AddThemeConstantOverride("shadow_offset_x", 1);
        _prompt.AddThemeConstantOverride("shadow_offset_y", 1);
        _prompt.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        _prompt.Visible = false;
        AddChild(_prompt);

        _tooltip = GetNodeOrNull<ItemTooltip>("/root/main/UI/ItemTooltip");

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public override void _Process(double delta)
    {
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
        {
            _playerInRange = true;
            _prompt.Visible = true;

            var tween = CreateTween().SetLoops();
            tween.TweenProperty(_prompt, "position:y", _prompt.Position.Y - 5, 0.6f);
            tween.TweenProperty(_prompt, "position:y", _prompt.Position.Y, 0.6f);
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
        {
            _playerInRange = false;
            _prompt.Visible = false;
        }
    }

    private void OnMouseEntered()
    {
        if (_tooltip != null && ItemContido != null)
            _tooltip.Mostrar(ItemContido, GetGlobalMousePosition());
    }

    private void OnMouseExited()
    {
        _tooltip?.Esconder();
    }

    public void Coletar()
    {
        TryCollect();
    }

    private void TryCollect()
    {
        if (ItemContido == null) return;

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
        {
            net.SendCollectLocalItem(ItemContido.ItemID, 1, GlobalPosition);
            GD.Print($"[ITEM] Pedido de coleta enviado ao servidor: {ItemContido.Nome}");
            _tooltip?.Esconder();
            QueueFree();
            return;
        }

        GD.PrintErr("[ITEM] Coleta local bloqueada. Conecte ao servidor para coletar itens.");
    }
}
