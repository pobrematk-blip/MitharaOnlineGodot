using Godot;

public partial class TalentNodeSlotUI : PanelContainer
{
    public TalentNodeResource NodeData { get; set; }
    public bool IsUnlocked { get; set; }
    public Texture2D IconTexture
    {
        get => _iconTexture;
        set
        {
            _iconTexture = value;
            AtualizarIconeVisual();
            QueueRedraw();
        }
    }
    public string FallbackText
    {
        get => _fallbackText;
        set
        {
            _fallbackText = value;
            AtualizarIconeVisual();
            QueueRedraw();
        }
    }
    public float IconPadding { get; set; } = 5f;

    private Texture2D _iconTexture;
    private string _fallbackText = "?";
    private TextureRect _iconView;
    private Label _fallbackLabel;
    private PanelContainer _hoverTooltip;
    private CanvasLayer _hoverTooltipLayer;

    public override void _Ready()
    {
        MouseEntered += MostrarTooltipCustom;
        MouseExited += EsconderTooltipCustom;
        AtualizarIconeVisual();
        SetProcess(false);
    }

    public override void _Draw()
    {
        if (_iconView != null || _fallbackLabel != null)
            return;

        if (_iconTexture != null)
        {
            var padding = new Vector2(IconPadding, IconPadding);
            var iconRect = new Rect2(padding, Size - padding * 2f);
            if (iconRect.Size.X > 0 && iconRect.Size.Y > 0)
                DrawTextureRect(_iconTexture, iconRect, false);
            return;
        }

        if (string.IsNullOrWhiteSpace(_fallbackText))
            return;

        var font = GetThemeDefaultFont();
        if (font == null)
            return;

        int fontSize = Mathf.RoundToInt(Mathf.Clamp(Size.Y * 0.34f, 9f, 22f));
        Vector2 textSize = font.GetStringSize(_fallbackText, HorizontalAlignment.Center, -1, fontSize);
        Vector2 pos = (Size - textSize) * 0.5f + new Vector2(0, textSize.Y * 0.78f);
        DrawString(font, pos, _fallbackText, HorizontalAlignment.Center, Size.X, fontSize, new Color(0.88f, 0.9f, 0.94f));
    }

    public override void _ExitTree()
    {
        EsconderTooltipCustom();
    }

    private void AtualizarIconeVisual()
    {
        if (!IsInsideTree())
            return;

        if (_iconTexture != null)
        {
            if (_fallbackLabel != null && IsInstanceValid(_fallbackLabel))
                _fallbackLabel.QueueFree();
            _fallbackLabel = null;

            if (_iconView == null || !IsInstanceValid(_iconView))
            {
                _iconView = new TextureRect
                {
                    MouseFilter = MouseFilterEnum.Ignore,
                    ZIndex = 1,
                    ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                };
                _iconView.SetAnchorsPreset(LayoutPreset.FullRect);
                _iconView.OffsetLeft = IconPadding;
                _iconView.OffsetTop = IconPadding;
                _iconView.OffsetRight = -IconPadding;
                _iconView.OffsetBottom = -IconPadding;
                AddChild(_iconView);
            }

            _iconView.Texture = _iconTexture;
            _iconView.TooltipText = TooltipText;
            AtualizarRetanguloIcone();
            QueueRedraw();
            return;
        }

        if (_iconView != null && IsInstanceValid(_iconView))
            _iconView.QueueFree();
        _iconView = null;

        if (string.IsNullOrWhiteSpace(_fallbackText))
        {
            if (_fallbackLabel != null && IsInstanceValid(_fallbackLabel))
                _fallbackLabel.QueueFree();
            _fallbackLabel = null;
            QueueRedraw();
            return;
        }

        if (_fallbackLabel == null || !IsInstanceValid(_fallbackLabel))
        {
            _fallbackLabel = new Label
            {
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 1,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _fallbackLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_fallbackLabel);
        }

        _fallbackLabel.Text = _fallbackText;
        _fallbackLabel.TooltipText = TooltipText;
        _fallbackLabel.AddThemeFontSizeOverride("font_size", NodeData?.NodeType == TalentNodeType.Skill ? 22 : 13);
        _fallbackLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.9f, 0.94f));
        AtualizarRetanguloIcone();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion)
            MostrarTooltipCustom();
    }

    public override Variant _GetDragData(Vector2 position)
    {
        if (!IsUnlocked)
        {
            GD.Print($"[TALENT UI] Drag bloqueado: talento ainda nao liberado ({NodeData?.NodeId ?? "sem-node"}).");
            return default;
        }

        if (NodeData == null || NodeData.HabilidadeAtiva == null)
        {
            GD.Print($"[TALENT UI] Drag bloqueado: talento sem skill ativa ({NodeData?.NodeId ?? "sem-node"}).");
            return default;
        }

        var preview = new TextureRect();
        preview.Texture = NodeData.HabilidadeAtiva.Icone;
        preview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        preview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        preview.CustomMinimumSize = new Vector2(40, 40);
        preview.Size = new Vector2(40, 40);
        SetDragPreview(preview);
        GD.Print($"[TALENT UI] Arrastando skill: {NodeData.HabilidadeAtiva.Nome} (ID {NodeData.HabilidadeAtiva.SkillId})");
        return NodeData.HabilidadeAtiva;
    }

    private void MostrarTooltipCustom()
    {
        if (string.IsNullOrWhiteSpace(TooltipText) || _hoverTooltip != null)
            return;

        _hoverTooltip = new PanelContainer
        {
            ZIndex = 4095,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _hoverTooltip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.025f, 0.035f, 0.96f),
            BorderColor = new Color(0.45f, 0.6f, 0.85f, 0.9f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            ContentMarginBottom = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
        });

        var label = new Label
        {
            Text = TooltipText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(320, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.92f, 0.94f, 1f));
        _hoverTooltip.AddChild(label);

        _hoverTooltipLayer = new CanvasLayer
        {
            Name = "TalentTooltipLayer",
            Layer = 128,
        };
        GetTree()?.Root.AddChild(_hoverTooltipLayer);
        _hoverTooltipLayer.AddChild(_hoverTooltip);
        _hoverTooltip.ResetSize();
        AtualizarPosicaoTooltip();
        SetProcess(true);
    }

    private void EsconderTooltipCustom()
    {
        if (_hoverTooltipLayer != null && IsInstanceValid(_hoverTooltipLayer))
            _hoverTooltipLayer.QueueFree();
        else if (_hoverTooltip != null && IsInstanceValid(_hoverTooltip))
            _hoverTooltip.QueueFree();
        _hoverTooltipLayer = null;
        _hoverTooltip = null;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        AtualizarRetanguloIcone();
        if (_hoverTooltip != null && IsInstanceValid(_hoverTooltip))
            AtualizarPosicaoTooltip();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            AtualizarRetanguloIcone();
    }

    private void AtualizarPosicaoTooltip()
    {
        if (_hoverTooltip == null)
            return;

        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 pos = GetViewport().GetMousePosition() + new Vector2(18, 18);
        Vector2 size = _hoverTooltip.Size;
        if (size.X <= 1 || size.Y <= 1)
            size = _hoverTooltip.GetCombinedMinimumSize();
        if (pos.X + size.X > viewportSize.X - 8)
            pos.X = Mathf.Max(8, viewportSize.X - size.X - 8);
        if (pos.Y + size.Y > viewportSize.Y - 8)
            pos.Y = Mathf.Max(8, viewportSize.Y - size.Y - 8);
        _hoverTooltip.Position = pos;
    }

    private void AtualizarRetanguloIcone()
    {
        if (_iconView != null && IsInstanceValid(_iconView))
        {
            _iconView.Position = new Vector2(IconPadding, IconPadding);
            _iconView.Size = new Vector2(
                Mathf.Max(1, Size.X - IconPadding * 2f),
                Mathf.Max(1, Size.Y - IconPadding * 2f));
        }

        if (_fallbackLabel != null && IsInstanceValid(_fallbackLabel))
        {
            _fallbackLabel.Position = Vector2.Zero;
            _fallbackLabel.Size = Size;
        }
    }
}
