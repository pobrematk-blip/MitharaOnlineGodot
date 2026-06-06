using Godot;

public partial class ItemColetavel : Area2D
{
    [Export] public ItemResource ItemContido { get; set; }
    [Export] public Vector2 EscalaSprite { get; set; } = Vector2.One * 2;

    private Label _nameLabel;
    private Label _prompt;
    private bool _playerInRange;
    private bool _wasFPressed;
    private ItemTooltip _tooltip;

    public override void _Ready()
    {
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
        }

        _nameLabel = new Label();
        _nameLabel.Text = ItemContido?.Nome ?? "Item";
        _nameLabel.Position = new Vector2(-40, -38);
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _nameLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _nameLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _nameLabel.AddThemeColorOverride("shadow_color", new Color(0, 0, 0, 0.8f));
        AddChild(_nameLabel);

        _prompt = new Label();
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
        if (!_playerInRange) return;

        if (!_prompt.Visible)
            _prompt.Visible = true;

        if (Input.IsKeyPressed(Key.F) && !_wasFPressed)
        {
            _wasFPressed = true;
            TryCollect();
        }
        if (!Input.IsKeyPressed(Key.F))
            _wasFPressed = false;
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

    private void TryCollect()
    {
        if (ItemContido == null) return;

        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        var inventario = player.GetNodeOrNull<InventarioComponent>("InventarioComponent");
        if (inventario == null) return;

        if (inventario.AdicionarItem(ItemContido, 1))
        {
            GD.Print($"[MUNDO] Player coletou: {ItemContido.Nome}!");
            _tooltip?.Esconder();
            QueueFree();
        }
    }
}
