#nullable enable
using Godot;

public partial class PlayerContextMenu : Panel
{
    private static PlayerContextMenu? _instance;
    private string _targetName = "";
    private ulong _targetId;
    private Label _targetLabel = null!;
    private Panel? _duelWagerPanel;

    public override void _Ready()
    {
        _instance = this;
        Visible = false;

        CustomMinimumSize = new Vector2(200, 158);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Stop;

        var style = MitharaUiTheme.Panel(0.95f);
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
        _targetLabel.AddThemeColorOverride("font_color", MitharaUiTheme.Accent);
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
                    _instance.ShowDuelWagerDialog();
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

    private void ShowDuelWagerDialog()
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null || string.IsNullOrEmpty(_targetName))
            return;

        _duelWagerPanel?.QueueFree();

        _duelWagerPanel = new Panel
        {
            TopLevel = true,
            ZIndex = 4095,
            CustomMinimumSize = new Vector2(390, 218),
            Size = new Vector2(390, 218),
            MouseFilter = MouseFilterEnum.Stop,
        };
        var style = MitharaUiTheme.Panel(0.96f);
        style.ShadowColor = new Color(0, 0, 0, 0.58f);
        style.ShadowSize = 10;
        _duelWagerPanel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _duelWagerPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        var titleRow = new HBoxContainer();
        vbox.AddChild(titleRow);

        var title = new Label
        {
            Text = "Aposta do Duelo",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", MitharaUiTheme.Accent);
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        title.AddThemeConstantOverride("outline_size", 3);
        titleRow.AddChild(title);

        var closeBtn = new Button
        {
            Text = "X",
            Flat = true,
            CustomMinimumSize = new Vector2(28, 26),
        };
        closeBtn.Pressed += CloseDuelWagerDialog;
        titleRow.AddChild(closeBtn);

        var desc = new Label
        {
            Text = $"Quanto ouro quer apostar contra {_targetName}?",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 34),
        };
        desc.AddThemeFontSizeOverride("font_size", 13);
        desc.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        vbox.AddChild(desc);

        var goldBox = new Panel();
        goldBox.AddThemeStyleboxOverride("panel", MitharaUiTheme.Inner(0.72f));
        goldBox.CustomMinimumSize = new Vector2(0, 72);
        vbox.AddChild(goldBox);

        var goldMargin = new MarginContainer();
        goldMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        goldMargin.AddThemeConstantOverride("margin_left", 18);
        goldMargin.AddThemeConstantOverride("margin_top", 8);
        goldMargin.AddThemeConstantOverride("margin_right", 18);
        goldMargin.AddThemeConstantOverride("margin_bottom", 8);
        goldBox.AddChild(goldMargin);

        var goldVBox = new VBoxContainer();
        goldVBox.AddThemeConstantOverride("separation", 5);
        goldMargin.AddChild(goldVBox);

        var goldLabel = new Label
        {
            Text = $"Seu ouro: {net.Gold:N0}",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        goldLabel.AddThemeFontSizeOverride("font_size", 11);
        goldLabel.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
        goldVBox.AddChild(goldLabel);

        var spinCenter = new CenterContainer();
        goldVBox.AddChild(spinCenter);

        var spin = new SpinBox
        {
            MinValue = 0,
            MaxValue = Mathf.Max(0, net.Gold),
            Step = 1,
            Rounded = true,
            Value = 0,
            CustomMinimumSize = new Vector2(190, 30),
        };
        spin.GetLineEdit().Alignment = HorizontalAlignment.Center;
        spinCenter.AddChild(spin);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        buttons.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(buttons);

        string targetName = _targetName;
        var confirmBtn = new Button
        {
            Text = "Desafiar",
            CustomMinimumSize = new Vector2(116, 32),
        };
        confirmBtn.Pressed += () =>
        {
            int wager = Mathf.Max(0, (int)spin.Value);
            net.SendDuelRequest(targetName, wager);
            CloseDuelWagerDialog();
        };
        buttons.AddChild(confirmBtn);

        var cancelBtn = new Button
        {
            Text = "Cancelar",
            CustomMinimumSize = new Vector2(104, 32),
        };
        cancelBtn.Pressed += CloseDuelWagerDialog;
        buttons.AddChild(cancelBtn);

        var dialogParent = GetParent() ?? this;
        dialogParent.AddChild(_duelWagerPanel);
        CenterDuelWagerDialog();
        _duelWagerPanel.Visible = true;
        _duelWagerPanel.MoveToFront();
    }

    private void CloseDuelWagerDialog()
    {
        if (_duelWagerPanel == null)
            return;

        _duelWagerPanel.QueueFree();
        _duelWagerPanel = null;
    }

    private void CenterDuelWagerDialog()
    {
        if (_duelWagerPanel == null)
            return;

        var vp = GetViewportRect().Size;
        _duelWagerPanel.Position = new Vector2(
            vp.X * 0.5f - _duelWagerPanel.Size.X * 0.5f,
            vp.Y * 0.5f - _duelWagerPanel.Size.Y * 0.5f);
    }
}
