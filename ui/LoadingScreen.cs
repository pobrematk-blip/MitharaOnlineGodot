using Godot;

public partial class LoadingScreen : CanvasLayer
{
    private TextureRect _bg;
    private Label _status;

    public override void _Ready()
    {
        Layer = 128;

        var rootCtrl = new Control();
        rootCtrl.MouseFilter = Control.MouseFilterEnum.Ignore;
        rootCtrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(rootCtrl);

        _bg = new TextureRect();
        _bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var bgTexture = ResourceLoader.Load<Texture2D>("res://ui/Tela de Carregamento/Tela de Carregamento.png");
        if (bgTexture != null)
            _bg.Texture = bgTexture;
        rootCtrl.AddChild(_bg);

        var overlay = new ColorRect();
        overlay.Color = new Color(0, 0, 0, 0.4f);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rootCtrl.AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        rootCtrl.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        center.AddChild(vbox);

        _status = new Label();
        _status.Text = "Carregando...";
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AddThemeFontSizeOverride("font_size", 24);
        _status.AddThemeColorOverride("font_color", Colors.White);
        _status.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _status.AddThemeConstantOverride("outline_size", 3);
        vbox.AddChild(_status);

        SetProcess(false);
    }

    public void SetBackground(Texture2D texture)
    {
        if (_bg != null)
            _bg.Texture = texture;
    }

    public void SetStatus(string text)
    {
        if (_status != null)
            _status.Text = text;
    }

    public void Fechar()
    {
        QueueFree();
    }
}
