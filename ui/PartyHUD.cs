#nullable enable
using Godot;
using System.Collections.Generic;

public partial class PartyHUD : Control
{
    private VBoxContainer _container = null!;
    private List<Godot.Collections.Dictionary> _members = new();
    private int _partyId;
    private ulong _leaderId;
    private Panel? _activeMenu;
    private ulong _activeMenuTargetId;
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
        MouseFilter = MouseFilterEnum.Pass;
        AlignBelowPlayerHud();
        CallDeferred(nameof(AlignBelowPlayerHud));

        _container = new VBoxContainer();
        _container.Name = "PartyHUDContainer";
        _container.AddThemeConstantOverride("separation", 2);
        _container.MouseFilter = MouseFilterEnum.Ignore;
        _container.SetAnchorsPreset(LayoutPreset.FullRect);
        _container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_container);

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnPartyData += OnNetworkPartyData;
            net.OnPartyMemberUpdate += OnNetworkPartyMemberUpdate;
            net.OnPartyLeaderUpdate += OnNetworkPartyLeaderUpdate;
            net.OnEntityHealthUpdate += OnEntityHealthUpdate;
            net.OnEntityManaUpdate += OnEntityManaUpdate;

            if (net.HasPendingPartyData)
                OnNetworkPartyData(net.PendingPartyId, net.PendingPartyMembers);
        }

        Refresh();
    }

    private void AlignBelowPlayerHud()
    {
        var playerHud = GetTree()?.CurrentScene?.FindChild("PlayerHud", true, false) as Control;
        if (playerHud == null)
        {
            Position = new Vector2(8, 188);
            Size = new Vector2(170, 112);
            return;
        }

        Position = playerHud.Position + new Vector2(0, playerHud.Size.Y + 62);
        Size = new Vector2(Mathf.Max(160f, playerHud.Size.X - 42f), 112);
    }

    private void OnNetworkPartyData(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members)
    {
        _partyId = partyId;
        _leaderId = 0;
        _members.Clear();
        foreach (var m in members)
        {
            _members.Add(m);
            if ((bool)m["is_leader"])
                _leaderId = (ulong)(long)m["entity_id"];
        }
        Refresh();
    }

    private void OnNetworkPartyMemberUpdate(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined, string characterClass)
    {
        var existing = FindMember(entityId);
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
                existing["class"] = characterClass;
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
                    ["class"] = characterClass,
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

    private void OnEntityHealthUpdate(ulong entityId, int health, int maxHealth)
    {
        var member = FindMember(entityId);
        if (member == null) return;

        member["health"] = health;
        member["max_health"] = maxHealth;
        Refresh();
    }

    private void OnEntityManaUpdate(ulong entityId, int mana, int maxMana)
    {
        var member = FindMember(entityId);
        if (member == null) return;

        member["mana"] = mana;
        member["max_mana"] = maxMana;
        Refresh();
    }

    private Godot.Collections.Dictionary? FindMember(ulong entityId)
    {
        return _members.Find(m => (ulong)(long)m["entity_id"] == entityId);
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
        foreach (var child in _container.GetChildren())
            child.QueueFree();

        if (_members.Count <= 1)
        {
            FecharMenuContexto();
            Visible = false;
            return;
        }

        if (HiddenByUser)
        {
            FecharMenuContexto();
            Visible = false;
            return;
        }

        if (_activeMenu != null && IsInstanceValid(_activeMenu) && FindMember(_activeMenuTargetId) == null)
            FecharMenuContexto();

        Visible = true;
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");

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
            memberPanel.CustomMinimumSize = new Vector2(0, 28);
            memberPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

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
            vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vbox.OffsetLeft = 4;
            vbox.OffsetTop = 2;
            vbox.OffsetRight = -4;
            vbox.OffsetBottom = -2;

            var topRow = new HBoxContainer();
            topRow.MouseFilter = MouseFilterEnum.Ignore;

            string charClass = m.ContainsKey("class") ? (string)m["class"] : "";
            Texture2D? classIcon = CarregarIconeClasse(charClass);
            if (classIcon != null)
            {
                var iconRect = new TextureRect();
                iconRect.Texture = classIcon;
                iconRect.CustomMinimumSize = new Vector2(12, 12);
                iconRect.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                topRow.AddChild(iconRect);
            }

            var nameLabel = new Label();
            string prefix = isLeader ? "(L) " : "";
            nameLabel.Text = $"{prefix}{nome}";
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeFontSizeOverride("font_size", 8);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 1, 0.85f));

            var levelLabel = new Label();
            levelLabel.Text = $"Lv.{level}";
            levelLabel.AddThemeFontSizeOverride("font_size", 7);
            levelLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.3f, 0.8f));

            topRow.AddChild(nameLabel);
            topRow.AddChild(levelLabel);
            vbox.AddChild(topRow);

            var bars = new VBoxContainer();
            bars.AddThemeConstantOverride("separation", 1);
            bars.MouseFilter = MouseFilterEnum.Ignore;

            var hpBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0, 5),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MaxValue = System.Math.Max(1, maxHp),
                Value = System.Math.Clamp(hp, 0, System.Math.Max(1, maxHp)),
                ShowPercentage = false,
            };
            hpBar.AddThemeStyleboxOverride("background", CriarBarra(new Color(0.15f, 0.04f, 0.04f, 0.85f)));
            hpBar.AddThemeStyleboxOverride("fill", CriarBarra(new Color(0.9f, 0.12f, 0.12f, 0.95f)));
            bars.AddChild(hpBar);

            var mpBar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(0, 5),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MaxValue = System.Math.Max(1, maxMp),
                Value = System.Math.Clamp(mp, 0, System.Math.Max(1, maxMp)),
                ShowPercentage = false,
            };
            mpBar.AddThemeStyleboxOverride("background", CriarBarra(new Color(0.04f, 0.04f, 0.15f, 0.85f)));
            mpBar.AddThemeStyleboxOverride("fill", CriarBarra(new Color(0.2f, 0.4f, 1f, 0.95f)));
            bars.AddChild(mpBar);

            vbox.AddChild(bars);
            memberPanel.AddChild(vbox);

            memberPanel.MouseFilter = MouseFilterEnum.Pass;
            bool isSelf = net != null && eid == net.LocalPlayerId;
            memberPanel.GuiInput += (InputEvent @event) =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed &&
                    (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
                {
                    MostrarMenuContexto(eid, nome, isSelf, memberPanel.GetGlobalMousePosition());
                    GetViewport()?.SetInputAsHandled();
                }
            };

            _container.AddChild(memberPanel);
        }
    }

    private static Texture2D? CarregarIconeClasse(string classe)
    {
        string iconPath = classe.ToLowerInvariant() switch
        {
            "arqueiro" => "res://Itens/Incones/Arco do Atirador.png",
            "assassino" or "ladino" => "res://Itens/Incones/Adaga Sombria.png",
            "guerreiro" => "res://Itens/Incones/Machados Perdisos 1.png",
            "berserker" => "res://Itens/Incones/Machados Perdisos 2.png",
            "mago" => "res://Itens/Incones/1.png",
            "clerigo" => "res://Itens/Incones/Martelo quebrada.png",
            "guardiao" => "res://Itens/Incones/Escudo de Goglin.png",
            _ => "",
        };
        if (!string.IsNullOrEmpty(iconPath) && ResourceLoader.Exists(iconPath))
            return ResourceLoader.Load<Texture2D>(iconPath);
        return null;
    }

    private static StyleBoxFlat CriarBarra(Color cor)
    {
        var style = new StyleBoxFlat { BgColor = cor };
        style.SetCornerRadiusAll(2);
        return style;
    }

    private void MostrarMenuContexto(ulong targetId, string targetName, bool isSelf, Vector2 screenPos)
    {
        FecharMenuContexto();

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null) return;

        var menu = new Panel();
        menu.CustomMinimumSize = new Vector2(178, isSelf ? 38 : (_leaderId == net.LocalPlayerId ? 66 : 38));
        menu.Size = menu.CustomMinimumSize;
        menu.MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.75f);
        style.SetCornerRadiusAll(4);
        style.BorderColor = new Color(0.3f, 0.3f, 0.45f, 0.8f);
        style.SetBorderWidthAll(1);
        menu.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        menu.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        margin.AddChild(vbox);

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

        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 desiredGlobal = new(
            Mathf.Clamp(screenPos.X, 8f, Mathf.Max(8f, viewportSize.X - menu.Size.X - 8f)),
            Mathf.Clamp(screenPos.Y, 8f, Mathf.Max(8f, viewportSize.Y - menu.Size.Y - 8f)));
        menu.Position = desiredGlobal - GlobalPosition;
        AddChild(menu);
        menu.MoveToFront();
        _activeMenu = menu;
        _activeMenuTargetId = targetId;
    }

    private void FecharMenuContexto()
    {
        if (_activeMenu != null && IsInstanceValid(_activeMenu))
            _activeMenu.QueueFree();
        _activeMenu = null;
        _activeMenuTargetId = 0;
    }

    private static void AdicionarOpcao(VBoxContainer parent, string text, System.Action action)
    {
        var btn = new Button { Text = text, Flat = true, CustomMinimumSize = new Vector2(0, 26) };
        btn.Pressed += action;
        parent.AddChild(btn);
    }

    public override void _ExitTree()
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null) return;

        net.OnPartyData -= OnNetworkPartyData;
        net.OnPartyMemberUpdate -= OnNetworkPartyMemberUpdate;
        net.OnPartyLeaderUpdate -= OnNetworkPartyLeaderUpdate;
        net.OnEntityHealthUpdate -= OnEntityHealthUpdate;
        net.OnEntityManaUpdate -= OnEntityManaUpdate;
    }
}
