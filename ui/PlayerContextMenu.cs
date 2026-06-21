#nullable enable
using Godot;

public partial class PlayerContextMenu : Panel
{
    private static PlayerContextMenu? _instance;
    private string _targetName = "";
    private ulong _targetId;

    public override void _Ready()
    {
        _instance = this;
        Visible = false;

        CustomMinimumSize = new Vector2(160, 0);
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.7f);
        style.SetCornerRadiusAll(4);
        AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        AddChild(vbox);

        AddOption(vbox, "Convidar para Grupo");
        AddOption(vbox, "Convidar para Guild");
        AddOption(vbox, "Desafiar para Duelo");
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
            }
        };
        parent.AddChild(btn);
    }

    public static void ShowAt(ulong targetId, string targetName, Vector2 screenPos)
    {
        if (_instance == null || !_instance.IsInsideTree()) return;
        _instance._targetId = targetId;
        _instance._targetName = targetName;
        _instance.Position = screenPos;
        _instance.Visible = true;
    }

    public static void HideMenu()
    {
        if (_instance == null) return;
        _instance.Visible = false;
    }
}
