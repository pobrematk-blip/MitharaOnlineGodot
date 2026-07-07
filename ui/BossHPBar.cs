using Godot;

public partial class BossHPBar : Panel
{
    private Label _nameLabel;
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private PanelContainer _castPanel;
    private ProgressBar _castBar;
    private Label _castLabel;
    private HBoxContainer _buffBox;
    private VBoxContainer _contentBox;
    private readonly System.Collections.Generic.Dictionary<string, BossStatusIconUI> _statusIcons = new();
    private float _castRemaining;
    private float _castTotal;
    private const float BarHeight = 86f;

    public override void _Ready()
    {
        MouseFilter = Control.MouseFilterEnum.Ignore;
        ZIndex = 3500;
        ZAsRelative = false;
        CustomMinimumSize = new Vector2(560, BarHeight);

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
            Size = new Vector2(528, 66),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _contentBox.AddThemeConstantOverride("separation", 3);
        AddChild(_contentBox);

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 17);
        _nameLabel.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.1f));
        _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _nameLabel.AddThemeConstantOverride("outline_size", 3);
        _contentBox.AddChild(_nameLabel);

        _hpBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(528, 18),
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

        _castPanel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(528, 12),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _castPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.02f, 0.025f, 0.035f, 0.78f), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        _contentBox.AddChild(_castPanel);

        _castBar = new ProgressBar
        {
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _castBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.35f, 0.72f, 1f, 0.94f), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        _castBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.05f, 0.055f, 0.07f, 0.86f), CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        _castPanel.AddChild(_castBar);

        _castLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 4,
        };
        _castLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _castLabel.AddThemeFontSizeOverride("font_size", 9);
        _castLabel.AddThemeColorOverride("font_color", Colors.White);
        _castLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _castLabel.AddThemeConstantOverride("outline_size", 2);
        _castPanel.AddChild(_castLabel);

        _buffBox = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(528, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _buffBox.AddThemeConstantOverride("separation", 4);
        _contentBox.AddChild(_buffBox);

        Hide();
    }

    public override void _Process(double delta)
    {
        var vp = GetViewportRect();
        float width = Mathf.Clamp(vp.Size.X * 0.58f, 560f, 900f);
        Position = new Vector2((vp.Size.X - width) * 0.5f, 12);
        Size = new Vector2(width, BarHeight);

        if (_contentBox != null)
            _contentBox.Size = new Vector2(Mathf.Max(1f, width - 32f), 66);
        if (_hpBar != null)
            _hpBar.CustomMinimumSize = new Vector2(Mathf.Max(1f, width - 32f), 18);
        if (_castPanel != null)
            _castPanel.CustomMinimumSize = new Vector2(Mathf.Max(1f, width - 32f), 12);
        if (_buffBox != null)
            _buffBox.CustomMinimumSize = new Vector2(Mathf.Max(1f, width - 32f), 18);

        AtualizarCast((float)delta);

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
        float nearestDistSq = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("Bosses"))
        {
            if (node is Inimigo mob && mob.IsBoss && mob.IsInsideTree())
            {
                if (mob.VidaAtual <= 0)
                    continue;

                float distSq = player.GlobalPosition.DistanceSquaredTo(mob.GlobalPosition);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
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

    public void StartCast(string skillName, float castSeconds)
    {
        if (castSeconds <= 0f)
            return;

        _castTotal = castSeconds;
        _castRemaining = castSeconds;
        _castLabel.Text = skillName;
        _castBar.MaxValue = castSeconds;
        _castBar.Value = 0;
        _castPanel.Visible = true;
        Show();
    }

    public void AddBossStatus(string effectId, string displayName, bool isBuff, float duration)
    {
        if (string.IsNullOrWhiteSpace(effectId) || duration <= 0f || _buffBox == null)
            return;

        if (_statusIcons.TryGetValue(effectId, out var existing) && IsInstanceValid(existing))
        {
            existing.Restart(duration);
            return;
        }

        var icon = new BossStatusIconUI(effectId, displayName, isBuff, duration);
        icon.Expired += () =>
        {
            _statusIcons.Remove(effectId);
            if (IsInstanceValid(icon))
                icon.QueueFree();
        };
        _statusIcons[effectId] = icon;
        _buffBox.AddChild(icon);
    }

    private void AtualizarCast(float delta)
    {
        if (_castPanel == null || !_castPanel.Visible)
            return;

        _castRemaining = Mathf.Max(0f, _castRemaining - delta);
        _castBar.Value = Mathf.Clamp(_castTotal - _castRemaining, 0f, _castTotal);
        if (_castRemaining <= 0f)
            _castPanel.Visible = false;
    }
}

public partial class BossStatusIconUI : Panel
{
    public event System.Action Expired;

    private readonly string _effectId;
    private readonly string _displayName;
    private readonly bool _isBuff;
    private float _remaining;
    private Label _label;

    public BossStatusIconUI(string effectId, string displayName, bool isBuff, float duration)
    {
        _effectId = effectId;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? effectId : displayName;
        _isBuff = isBuff;
        _remaining = duration;
        CustomMinimumSize = new Vector2(22, 22);
        Size = new Vector2(22, 22);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        var border = _isBuff ? new Color(0.34f, 0.92f, 0.58f, 0.95f) : new Color(1f, 0.22f, 0.2f, 0.95f);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.015f, 0.018f, 0.025f, 0.9f),
            BorderColor = border,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        });

        _label = new Label
        {
            Text = Mathf.CeilToInt(_remaining).ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _label.SetAnchorsPreset(LayoutPreset.FullRect);
        _label.AddThemeFontSizeOverride("font_size", 9);
        _label.AddThemeColorOverride("font_color", border);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 2);
        AddChild(_label);
        TooltipText = $"{(_isBuff ? "Buff" : "Debuff")} do boss: {_displayName}";
        SetProcess(true);
    }

    public void Restart(float duration)
    {
        _remaining = duration;
        AtualizarLabel();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _remaining = Mathf.Max(0f, _remaining - (float)delta);
        AtualizarLabel();
        if (_remaining <= 0f)
        {
            SetProcess(false);
            Expired?.Invoke();
        }
    }

    private void AtualizarLabel()
    {
        if (_label != null)
            _label.Text = Mathf.CeilToInt(_remaining).ToString();
    }
}
