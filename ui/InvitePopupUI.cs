#nullable enable
using Godot;

public partial class InvitePopupUI : Panel
{
    private static InvitePopupUI? _instance;

    private Label _titleLabel;
    private string _inviteType = "";

    public override void _Ready()
    {
        _instance = this;
        Visible = false;

        CustomMinimumSize = new Vector2(300, 80);
        var margin = new MarginContainer { ThemeTypeVariation = "MarginContainer" };
        AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        _titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        vbox.AddChild(_titleLabel);

        var hbox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hbox.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(hbox);

        var acceptBtn = new Button { Text = "Aceitar" };
        acceptBtn.Pressed += OnAccept;
        hbox.AddChild(acceptBtn);

        var declineBtn = new Button { Text = "Recusar" };
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
            "duel" => $"{senderName} desafiou você para um duelo!",
            _ => $"{senderName} convidou você!",
        };
        var vp = _instance.GetViewportRect();
        _instance.Position = new Vector2(vp.Size.X / 2 - 150, vp.Size.Y / 2 - 40);
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
            case "duel": net.SendDuelAccept(); break;
        }
    }

    private void OnDecline()
    {
        Visible = false;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null) return;
        if (_inviteType == "duel")
            net.SendDuelDecline();
    }
}
