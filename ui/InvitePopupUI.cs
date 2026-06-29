#nullable enable
using Godot;

public partial class InvitePopupUI : Panel
{
    private static InvitePopupUI? _instance;

    private Label _titleLabel = null!;
    private string _inviteType = "";

    public InvitePopupUI()
    {
        _instance = this;
    }

    public override void _Ready()
    {
        _instance = this;
        Visible = false;

        CustomMinimumSize = new Vector2(420, 150);
        Size = CustomMinimumSize;

        AddThemeStyleboxOverride("panel", MitharaUiTheme.Panel(0.95f));

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        _titleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 52),
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 14);
        _titleLabel.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        vbox.AddChild(_titleLabel);

        var hbox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hbox.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(hbox);

        var acceptBtn = new Button { Text = "Aceitar", CustomMinimumSize = new Vector2(120, 34) };
        acceptBtn.Pressed += OnAccept;
        hbox.AddChild(acceptBtn);

        var declineBtn = new Button { Text = "Recusar", CustomMinimumSize = new Vector2(120, 34) };
        declineBtn.Pressed += OnDecline;
        hbox.AddChild(declineBtn);
    }

    public static void ShowInvite(string inviteType, string senderName)
    {
        if (_instance == null || !_instance.IsInsideTree()) return;
        _instance._inviteType = inviteType;
        _instance._titleLabel.Text = inviteType switch
        {
            "party" => $"{senderName} convidou você para um grupo!",
            "guild" => $"{senderName} convidou você para a guild!",
            "guild_promote" => $"{senderName} quer passar a liderança da guild para você!",
            "duel" => $"{senderName} desafiou você para um duelo!",
            "trade" => $"{senderName} quer trocar itens com você!",
            _ => $"{senderName} convidou você!",
        };
        var vp = _instance.GetViewportRect();
        _instance.Size = _instance.CustomMinimumSize;
        _instance.Position = new Vector2(vp.Size.X / 2 - _instance.Size.X / 2, vp.Size.Y / 2 - _instance.Size.Y / 2);
        _instance.Visible = true;
    }

    private void OnAccept()
    {
        Visible = false;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null) return;
        switch (_inviteType)
        {
            case "party": net.SendPartyAccept(); break;
            case "guild": net.SendGuildAccept(); break;
            case "guild_promote": net.SendGuildPromoteLeaderAccept(); break;
            case "duel": net.SendDuelAccept(); break;
            case "trade": net.SendTradeAccept(); break;
        }
    }

    private void OnDecline()
    {
        Visible = false;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null) return;
        switch (_inviteType)
        {
            case "duel": net.SendDuelDecline(); break;
            case "trade": net.SendTradeDecline(); break;
            case "guild_promote": net.SendGuildPromoteLeaderDecline(); break;
        }
    }
}
