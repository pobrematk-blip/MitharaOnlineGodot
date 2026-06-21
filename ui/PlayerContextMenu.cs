#nullable enable
using Godot;

public partial class PlayerContextMenu : Panel
{
    private static PlayerContextMenu? _instance;
    private string _targetName = "";
    private ulong _targetId;
    private Label _targetLabel = null!;

    public override void _Ready()
    {
        _instance = this;
        Visible = false;

        CustomMinimumSize = new Vector2(200, 158);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        style.SetCornerRadiusAll(6);
        style.BorderColor = new Color(0.3f, 0.3f, 0.45f, 0.8f);
        style.SetBorderWidthAll(1);
        style.ShadowColor = new Color(0, 0, 0, 0.45f);
        style.ShadowSize = 5;
        AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        margin.AddChild(vbox);

        _targetLabel = new Label
        {
            Text = "Interagir com jogador",
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true,
            CustomMinimumSize = new Vector2(0, 24),
        };
        _targetLabel.AddThemeFontSizeOverride("font_size", 11);
        _targetLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.35f));
        vbox.AddChild(_targetLabel);

        AddOption(vbox, "Convidar para Grupo");
        AddOption(vbox, "Convidar para Guild");
        AddOption(vbox, "Desafiar para Duelo");
        AddOption(vbox, "Solicitar Troca");
    }

    private static void AddOption(VBoxContainer parent, string text)
    {
        var btn = new Button
        {
            Text = text,
            Flat = true,
            CustomMinimumSize = new Vector2(0, 28),
        };
        btn.Pressed += () =>
        {
            if (_instance == null) return;
            _instance.Visible = false;
            var net = _instance.GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (net == null || string.IsNullOrEmpty(_instance._targetName)) return;
            switch (text)
            {
                case "Convidar para Grupo":
                    net.SendPartyInvite(_instance._targetName);
                    break;
                case "Convidar para Guild":
                    net.SendGuildInvite(_instance._targetName);
                    break;
                case "Desafiar para Duelo":
                    net.SendDuelRequest(_instance._targetName);
                    break;
                case "Solicitar Troca":
                    net.SendTradeRequest(_instance._targetName);
                    break;
            }
        };
        parent.AddChild(btn);
    }

    public static void ShowAt(ulong targetId, string targetName, Vector2 screenPos)
    {
        if (_instance == null || !_instance.IsInsideTree()) return;
        _instance._targetId = targetId;
        _instance._targetName = targetName;
        _instance._targetLabel.Text = targetName;
        Vector2 viewportSize = _instance.GetViewportRect().Size;
        _instance.Position = new Vector2(
            Mathf.Clamp(screenPos.X, 8f, Mathf.Max(8f, viewportSize.X - _instance.Size.X - 8f)),
            Mathf.Clamp(screenPos.Y, 8f, Mathf.Max(8f, viewportSize.Y - _instance.Size.Y - 8f)));
        _instance.Visible = true;
        _instance.MoveToFront();
    }

    public static void HideMenu()
    {
        if (_instance == null) return;
        _instance.Visible = false;
    }
}
