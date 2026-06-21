#nullable enable
using Godot;
using System.Collections.Generic;

public partial class PartyHUD : Control
{
    private VBoxContainer _container;
    private List<Godot.Collections.Dictionary> _members = new();
    private int _partyId;
    private ulong _leaderId;
    private Panel? _activeMenu;
    public bool HiddenByUser { get; set; }

    private string NomeJogador
    {
        get
        {
            var p = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
            return p?.NomePersonagem ?? "Aventureiro";
        }
    }

    public override void _Ready()
    {
        _container = new VBoxContainer();
        _container.Name = "PartyHUDContainer";
        _container.AddThemeConstantOverride("separation", 2);
        _container.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_container);

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnPartyData += OnNetworkPartyData;
            net.OnPartyMemberUpdate += OnNetworkPartyMemberUpdate;
            net.OnPartyLeaderUpdate += OnNetworkPartyLeaderUpdate;
        }

        Refresh();
    }

    private void OnNetworkPartyData(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members)
    {
        _partyId = partyId;
        _members.Clear();
        foreach (var m in members)
        {
            _members.Add(m);
            if ((bool)m["is_leader"])
                _leaderId = (ulong)(long)m["entity_id"];
        }
        Refresh();
    }

    private void OnNetworkPartyMemberUpdate(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined)
    {
        var existing = _members.Find(m => (ulong)(long)m["entity_id"] == entityId);
        if (joined)
        {
            if (existing != null)
            {
                existing["name"] = name;
                existing["health"] = health;
                existing["max_health"] = maxHealth;
                existing["mana"] = mana;
                existing["max_mana"] = maxMana;
                existing["level"] = level;
            }
            else
            {
                _members.Add(new Godot.Collections.Dictionary
                {
                    ["entity_id"] = (long)entityId,
                    ["name"] = name,
                    ["is_leader"] = false,
                    ["health"] = health,
                    ["max_health"] = maxHealth,
                    ["mana"] = mana,
                    ["max_mana"] = maxMana,
                    ["level"] = level,
                });
            }
        }
        else
        {
            _members.RemoveAll(m => (ulong)(long)m["entity_id"] == entityId);
            if (_members.Count == 0)
            {
                _partyId = 0;
                _leaderId = 0;
            }
        }
        Refresh();
    }

    private void OnNetworkPartyLeaderUpdate(ulong newLeaderId)
    {
        _leaderId = newLeaderId;
        foreach (var m in _members)
            m["is_leader"] = (ulong)(long)m["entity_id"] == newLeaderId;
        Refresh();
    }

    public void Refresh()
    {
        if (_activeMenu != null && IsInstanceValid(_activeMenu))
            _activeMenu.QueueFree();
        _activeMenu = null;

        foreach (var child in _container.GetChildren())
            child.QueueFree();

        if (_members.Count <= 1)
        {
            Visible = false;
            return;
        }

        if (HiddenByUser) return;

        Visible = true;

        foreach (var m in _members)
        {
            ulong eid = (ulong)(long)m["entity_id"];
            string nome = (string)m["name"];
            bool isLeader = (bool)m["is_leader"];
            int hp = (int)m["health"];
            int maxHp = (int)m["max_health"];
            int mp = m.ContainsKey("mana") ? (int)m["mana"] : 0;
            int maxMp = m.ContainsKey("max_mana") ? (int)m["max_mana"] : 0;
            int level = m.ContainsKey("level") ? (int)m["level"] : 1;

            var memberPanel = new Panel();
            memberPanel.CustomMinimumSize = new Vector2(200, 40);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.45f);
            style.SetCornerRadiusAll(4);
            style.BorderColor = isLeader ? new Color(0.9f, 0.7f, 0.1f, 0.6f) : new Color(0.2f, 0.2f, 0.3f, 0.5f);
            style.SetBorderWidthAll(1);
            memberPanel.AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 1);
            vbox.MouseFilter = MouseFilterEnum.Ignore;

            var topRow = new HBoxContainer();
            topRow.MouseFilter = MouseFilterEnum.Ignore;

            var nameLabel = new Label();
            string prefix = isLeader ? "[color=yellow](L)[/color] " : "";
            nameLabel.Text = $"{prefix}{nome}";
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeFontSizeOverride("font_size", 10);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 1, 0.85f));

            var levelLabel = new Label();
            levelLabel.Text = $"Lv.{level}";
            levelLabel.AddThemeFontSizeOverride("font_size", 9);
            levelLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.3f, 0.8f));

            topRow.AddChild(nameLabel);
            topRow.AddChild(levelLabel);
            vbox.AddChild(topRow);

            var bars = new VBoxContainer();
            bars.AddThemeConstantOverride("separation", 1);
            bars.MouseFilter = MouseFilterEnum.Ignore;

            float hpPct = maxHp > 0 ? Mathf.Clamp((float)hp / maxHp, 0, 1) : 1f;
            var hpBar = new ColorRect();
            hpBar.CustomMinimumSize = new Vector2(0, 4);
            hpBar.Size = new Vector2(200 * hpPct, 4);
            hpBar.Color = new Color(1f - hpPct, hpPct, 0, 0.9f);
            bars.AddChild(hpBar);

            float mpPct = maxMp > 0 ? Mathf.Clamp((float)mp / maxMp, 0, 1) : 1f;
            var mpBar = new ColorRect();
            mpBar.CustomMinimumSize = new Vector2(0, 4);
            mpBar.Size = new Vector2(200 * mpPct, 4);
            mpBar.Color = new Color(0.2f, 0.4f, 1f, 0.9f);
            bars.AddChild(mpBar);

            vbox.AddChild(bars);

            memberPanel.MouseFilter = MouseFilterEnum.Pass;
            bool isSelf = nome == NomeJogador;
            memberPanel.GuiInput += (InputEvent @event) =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                {
                    MostrarMenuContexto(eid, nome, isSelf, memberPanel.GetGlobalMousePosition());
                    GetViewport()?.SetInputAsHandled();
                }
            };

            _container.AddChild(memberPanel);
        }
    }

    private void MostrarMenuContexto(ulong targetId, string targetName, bool isSelf, Vector2 screenPos)
    {
        if (_activeMenu != null && IsInstanceValid(_activeMenu))
            _activeMenu.QueueFree();

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null) return;

        var menu = new Panel();
        menu.CustomMinimumSize = new Vector2(160, 0);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.75f);
        style.SetCornerRadiusAll(4);
        menu.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        menu.AddChild(vbox);

        if (isSelf)
        {
            AdicionarOpcao(vbox, "Sair da Party", () =>
            {
                menu.QueueFree();
                _activeMenu = null;
                net.SendPartyLeave();
            });
        }

        if (!isSelf && _leaderId == net.LocalPlayerId)
        {
            AdicionarOpcao(vbox, $"Remover {targetName}", () =>
            {
                menu.QueueFree();
                _activeMenu = null;
                net.SendPartyKick(targetName);
            });
            AdicionarOpcao(vbox, "Passar Liderança", () =>
            {
                menu.QueueFree();
                _activeMenu = null;
                net.SendPartyPromote(targetName);
            });
        }

        if (vbox.GetChildCount() == 0)
        {
            AdicionarOpcao(vbox, "Fechar", () =>
            {
                menu.QueueFree();
                _activeMenu = null;
            });
        }

        menu.Position = screenPos;
        AddChild(menu);
        _activeMenu = menu;

        // Close menu on left-click
        var catcher = new Control();
        catcher.MouseFilter = MouseFilterEnum.Pass;
        catcher.SetAnchorsPreset(LayoutPreset.FullRect);
        catcher.GuiInput += (InputEvent ev) =>
        {
            if (ev is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
            {
                if (IsInstanceValid(menu)) menu.QueueFree();
                if (IsInstanceValid(catcher)) catcher.QueueFree();
                _activeMenu = null;
            }
        };
        GetTree().CurrentScene?.AddChild(catcher);
    }

    private static void AdicionarOpcao(VBoxContainer parent, string text, System.Action action)
    {
        var btn = new Button { Text = text, Flat = true, CustomMinimumSize = new Vector2(0, 26) };
        btn.Pressed += action;
        parent.AddChild(btn);
    }
}
