using Godot;

public partial class BossHPBar : Panel
{
    private Label _nameLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private VBoxContainer _contentBox;
    private float _detectRange = 1400f;
    private const float BarHeight = 74f;

    public override void _Ready()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore;
        ZIndex = 3500;
        ZAsRelative = false;
        CustomMinimumSize = new Vector2(520, BarHeight);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.6f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        };
        AddThemeStyleboxOverride("panel", style);

        _contentBox = new VBoxContainer
        {
            Position = new Vector2(16, 10),
            Size = new Vector2(488, 54),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_contentBox);

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 20);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.1f));
        _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _nameLabel.AddThemeConstantOverride("outline_size", 3);
        _contentBox.AddChild(_nameLabel);

        _hpBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(488, 24),
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var fillStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.55f, 0.06f, 0.06f, 0.9f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        var bgBarStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        _hpBar.AddThemeStyleboxOverride("fill", fillStyle);
        _hpBar.AddThemeStyleboxOverride("background", bgBarStyle);
        _contentBox.AddChild(_hpBar);

        _hpLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _hpLabel.AddThemeFontSizeOverride("font_size", 15);
        _hpLabel.AddThemeColorOverride("font_color", Colors.White);
        _hpLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _hpLabel.AddThemeConstantOverride("outline_size", 2);
        _contentBox.AddChild(_hpLabel);

        Hide();
    }

    public override void _Process(double delta)
    {
        var vp = GetViewportRect();
        float width = Mathf.Clamp(vp.Size.X * 0.56f, 520f, 760f);
        Position = new Vector2((vp.Size.X - width) * 0.5f, 12);
        Size = new Vector2(width, BarHeight);

        if (_contentBox != null)
            _contentBox.Size = new Vector2(Mathf.Max(1f, width - 32f), 54);
        if (_hpBar != null)
            _hpBar.CustomMinimumSize = new Vector2(Mathf.Max(1f, width - 32f), 24);

        FindNearestBoss();
    }

    private void FindNearestBoss()
    {
        var player = ObterPlayerLocal();
        if (player == null || !player.IsInsideTree())
        {
            Hide();
            return;
        }

        Inimigo nearestBoss = null;
        float nearestDist = _detectRange;

        foreach (var node in GetTree().GetNodesInGroup("Inimigos"))
        {
            if (node is Inimigo mob && mob.IsBoss && mob.IsInsideTree())
            {
                float dist = player.GlobalPosition.DistanceSquaredTo(mob.GlobalPosition);
                if (dist < nearestDist * nearestDist)
                {
                    nearestDist = Mathf.Sqrt(dist);
                    nearestBoss = mob;
                }
            }
        }

        if (nearestBoss != null)
        {
            UpdateBar(nearestBoss);
            Show();
        }
        else
        {
            Hide();
        }
    }

    private Node2D ObterPlayerLocal()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player != null && player.IsInsideTree())
            return player;

        player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
        if (player != null && player.IsInsideTree())
            return player;

        return GetTree()?.CurrentScene?.FindChild("Player", true, false) as Node2D;
    }

    private void UpdateBar(Inimigo boss)
    {
        _nameLabel.Text = $"[Lv.{boss.Level}] {boss.NomeDoInimigo}";

        int hp = boss.VidaAtual;
        int maxHp = boss.VidaMax;
        _hpBar.MaxValue = System.Math.Max(1, maxHp);
        _hpBar.Value = System.Math.Clamp(hp, 0, System.Math.Max(1, maxHp));
        _hpLabel.Text = $"{hp} / {maxHp}";
    }
}
