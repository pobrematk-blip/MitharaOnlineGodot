using Godot;

public partial class LoadingScreen : CanvasLayer
{
    private TextureRect _bg = null!;
    private Control _barHost = null!;
    private ColorRect _loadingTextCover = null!;
    private ProgressBar _progressBar = null!;
    private string _statusText = "";
    private bool _hasManualProgress;
    private double _animatedProgress;

    private static readonly Vector2 BarBaseSize = new(560, 22);
    private static readonly Vector2 NativeImageSize = new(1792, 1024);
    private static readonly Rect2 ProgressSlotRect = new(new Vector2(516, 858), new Vector2(760, 18));
    private static readonly Rect2 LoadingTextCoverRect = new(new Vector2(760, 804), new Vector2(272, 36));
    private static readonly Color Gold = new(0.88f, 0.58f, 0.18f);
    private static readonly Color GoldSoft = new(1.0f, 0.78f, 0.34f);

    public override void _Ready()
    {
        Layer = 128;

        var rootCtrl = new Control();
        rootCtrl.MouseFilter = Control.MouseFilterEnum.Ignore;
        rootCtrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(rootCtrl);

        _bg = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var bgTexture = ResourceLoader.Load<Texture2D>("res://ui/Tela de Carregamento/Tela de Carregamento.png");
        if (bgTexture != null)
            _bg.Texture = bgTexture;
        rootCtrl.AddChild(_bg);

        var overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.18f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rootCtrl.AddChild(overlay);

        _loadingTextCover = new ColorRect
        {
            Color = new Color(0.02f, 0.015f, 0.010f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rootCtrl.AddChild(_loadingTextCover);

        _barHost = CriarBarraCarregamento();
        rootCtrl.AddChild(_barHost);

        AjustarLayoutResponsivo();
        GetTree().Root.SizeChanged += AjustarLayoutResponsivo;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_hasManualProgress || _progressBar == null)
            return;

        _animatedProgress += delta * 34.0;
        _progressBar.Value = 10.0 + Mathf.PingPong((float)_animatedProgress, 82.0f);
    }

    private Control CriarBarraCarregamento()
    {
        var host = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var frame = new Panel
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", CriarStyleBox(new Color(0.015f, 0.013f, 0.010f, 0.88f), Gold, 2, 3));
        host.AddChild(frame);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 5);
        margin.AddThemeConstantOverride("margin_top", 5);
        margin.AddThemeConstantOverride("margin_right", 5);
        margin.AddThemeConstantOverride("margin_bottom", 5);
        host.AddChild(margin);

        _progressBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 12,
            ShowPercentage = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _progressBar.AddThemeStyleboxOverride("background", CriarStyleBox(new Color(0.02f, 0.018f, 0.014f, 0.95f), new Color(0.35f, 0.21f, 0.07f), 1, 2));
        _progressBar.AddThemeStyleboxOverride("fill", CriarStyleBox(new Color(0.55f, 0.06f, 0.02f, 0.96f), GoldSoft, 1, 2));
        margin.AddChild(_progressBar);

        return host;
    }

    private void AjustarLayoutResponsivo()
    {
        if (_barHost == null)
            return;

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float imageScale = Mathf.Max(viewport.X / NativeImageSize.X, viewport.Y / NativeImageSize.Y);
        Vector2 drawnImageSize = NativeImageSize * imageScale;
        Vector2 imageOffset = (viewport - drawnImageSize) * 0.5f;

        Vector2 slotPosition = imageOffset + ProgressSlotRect.Position * imageScale;
        Vector2 slotSize = ProgressSlotRect.Size * imageScale;
        slotSize.X = Mathf.Max(slotSize.X, 260f);
        slotSize.Y = Mathf.Clamp(slotSize.Y, 12f, BarBaseSize.Y);

        _barHost.Size = slotSize;
        _barHost.Position = slotPosition;

        if (_loadingTextCover != null)
        {
            _loadingTextCover.Position = imageOffset + LoadingTextCoverRect.Position * imageScale;
            _loadingTextCover.Size = LoadingTextCoverRect.Size * imageScale;
        }
    }

    private static StyleBoxFlat CriarStyleBox(Color bg, Color border, int borderSize, int radius)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderSize,
            BorderWidthTop = borderSize,
            BorderWidthRight = borderSize,
            BorderWidthBottom = borderSize,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
    }

    public void SetBackground(Texture2D texture)
    {
        if (_bg != null)
            _bg.Texture = texture;
    }

    public void SetStatus(string text)
    {
        _statusText = text;
    }

    public void SetProgress(float progress)
    {
        _hasManualProgress = true;
        if (_progressBar != null)
            _progressBar.Value = Mathf.Clamp(progress, 0f, 100f);
    }

    public void Fechar()
    {
        QueueFree();
    }

    public override void _ExitTree()
    {
        GetTree().Root.SizeChanged -= AjustarLayoutResponsivo;
    }
}
