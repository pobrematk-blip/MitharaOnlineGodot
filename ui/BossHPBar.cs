using Godot;

public partial class BossHPBar : Panel
{
    private Label _nameLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private float _detectRange = 700f;

    public override void _Ready()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(432, 64);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.6f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        };
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer
        {
            Position = new Vector2(16, 10),
            Size = new Vector2(400, 44),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(vbox);

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 20);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.1f));
        _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _nameLabel.AddThemeConstantOverride("outline_size", 3);
        vbox.AddChild(_nameLabel);

        _hpBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(400, 22),
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
        vbox.AddChild(_hpBar);

        _hpLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _hpLabel.AddThemeFontSizeOverride("font_size", 15);
        _hpLabel.AddThemeColorOverride("font_color", Colors.White);
        _hpLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _hpLabel.AddThemeConstantOverride("outline_size", 2);
        vbox.AddChild(_hpLabel);

        Hide();
    }

    public override void _Process(double delta)
    {
        var vp = GetViewportRect();
        Position = new Vector2((vp.Size.X - 432) / 2, 10);
        Size = new Vector2(432, 64);

        FindNearestBoss();
    }

    private void FindNearestBoss()
    {
        var player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
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
