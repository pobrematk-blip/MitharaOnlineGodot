using Godot;
using System.Collections.Generic;

public partial class PartyHUD : Control
{
    private VBoxContainer _container;
    private List<Godot.Collections.Dictionary> _members = new();

    public override void _Ready()
    {
        _container = new VBoxContainer();
        _container.Name = "PartyHUDContainer";
        _container.AddThemeConstantOverride("separation", 2);
        AddChild(_container);

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnPartyData += OnNetworkPartyData;
            net.OnPartyMemberUpdate += OnNetworkPartyMemberUpdate;
        }

        Refresh();
    }

    private void OnNetworkPartyData(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members)
    {
        _members.Clear();
        foreach (var m in members)
            _members.Add(m);
        Refresh();
    }

    private void OnNetworkPartyMemberUpdate(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined)
    {
        if (joined)
        {
            _members.Add(new Godot.Collections.Dictionary
            {
                ["name"] = name, ["health"] = health, ["max_health"] = maxHealth,
                ["mana"] = mana, ["max_mana"] = maxMana, ["level"] = level,
            });
        }
        else
        {
            _members.RemoveAll(m => (string)m["name"] == name);
        }
        Refresh();
    }

    public void Refresh()
    {
        foreach (var child in _container.GetChildren())
            child.QueueFree();

        if (_members.Count <= 1)
        {
            Visible = false;
            return;
        }

        Visible = true;

        foreach (var m in _members)
        {
            string nome = (string)m["name"];
            int hp = (int)m["health"];
            int maxHp = (int)m["max_health"];
            int mana = (int)m["mana"];
            int maxMana = (int)m["max_mana"];

            var memberPanel = new Panel();
            memberPanel.CustomMinimumSize = new Vector2(200, 44);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.4f);
            style.SetCornerRadiusAll(4);
            style.BorderColor = new Color(0.2f, 0.2f, 0.3f, 1);
            style.SetBorderWidthAll(1);
            memberPanel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 1);

            var nameLabel = new Label();
            nameLabel.Text = nome;
            nameLabel.AddThemeFontSizeOverride("font_size", 9);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 1, 0.85f));

            var hpBar = new ProgressBar();
            hpBar.MaxValue = maxHp > 0 ? maxHp : 1;
            hpBar.Value = hp;
            hpBar.ShowPercentage = false;
            hpBar.CustomMinimumSize = new Vector2(0, 8);
            var hpStyle = new StyleBoxFlat();
            hpStyle.BgColor = new Color(0.8f, 0.1f, 0.1f, 1);
            hpBar.AddThemeStyleboxOverride("fill", hpStyle);

            var mpBar = new ProgressBar();
            mpBar.MaxValue = maxMana > 0 ? maxMana : 1;
            mpBar.Value = mana;
            mpBar.ShowPercentage = false;
            mpBar.CustomMinimumSize = new Vector2(0, 6);
            var mpStyle = new StyleBoxFlat();
            mpStyle.BgColor = new Color(0.1f, 0.3f, 0.9f, 1);
            mpBar.AddThemeStyleboxOverride("fill", mpStyle);

            vbox.AddChild(nameLabel);
            vbox.AddChild(hpBar);
            vbox.AddChild(mpBar);
            memberPanel.AddChild(vbox);
            _container.AddChild(memberPanel);
        }
    }
}
