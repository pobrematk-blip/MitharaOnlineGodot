using Godot;
using System.Collections.Generic;
#nullable enable annotations

public partial class PartyMemberHud : Control
{
    private VBoxContainer _container;
    private List<Godot.Collections.Dictionary> _members = new();
    private int _partyId;

    private string NomeJogador
    {
        get
        {
            var p = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
            return p?.NomePersonagem ?? "";
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(252, 0);

        var bg = new Panel();
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.75f);
        bgStyle.SetCornerRadiusAll(6);
        bgStyle.BorderColor = new Color(0.25f, 0.25f, 0.4f, 0.7f);
        bgStyle.SetBorderWidthAll(1);
        bg.AddThemeStyleboxOverride("panel", bgStyle);

        _container = new VBoxContainer();
        _container.Name = "PartyMemberContainer";
        _container.AddThemeConstantOverride("separation", 2);
        _container.OffsetLeft = 4;
        _container.OffsetTop = 4;
        _container.OffsetRight = -4;
        _container.OffsetBottom = -4;
        _container.MouseFilter = MouseFilterEnum.Ignore;
        bg.AddChild(_container);

        AddChild(bg);

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnPartyData += OnNetworkPartyData;
            net.OnPartyMemberUpdate += OnNetworkPartyMemberUpdate;
        }

        Visible = false;
    }

    private void OnNetworkPartyData(int partyId, Godot.Collections.Array<Godot.Collections.Dictionary> members)
    {
        _partyId = partyId;
        _members.Clear();
        foreach (var m in members)
        {
            if ((string)m["name"] != NomeJogador)
                _members.Add(m);
        }
        Refresh();
    }

    private void OnNetworkPartyMemberUpdate(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined, string characterClass)
    {
        var existing = _members.Find(m => (ulong)(long)m["entity_id"] == entityId);
        if (joined)
        {
            if (existing != null)
            {
                existing["health"] = health;
                existing["max_health"] = maxHealth;
                existing["mana"] = mana;
                existing["max_mana"] = maxMana;
                existing["level"] = level;
                existing["class"] = characterClass;
            }
            else if (name != NomeJogador)
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
        }
        Refresh();
    }

    private void Refresh()
    {
        foreach (var child in _container.GetChildren())
            child.QueueFree();

        if (_members.Count == 0)
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
            int mp = m.ContainsKey("mana") ? (int)m["mana"] : 0;
            int maxMp = m.ContainsKey("max_mana") ? (int)m["max_mana"] : 0;
            int level = m.ContainsKey("level") ? (int)m["level"] : 1;
            string charClass = m.ContainsKey("class") ? (string)m["class"] : "";
            bool isLeader = m.ContainsKey("is_leader") && (bool)m["is_leader"];

            var card = new Panel();
            card.CustomMinimumSize = new Vector2(244, 44);

            var cardStyle = new StyleBoxFlat();
            cardStyle.BgColor = new Color(0, 0, 0, 0.3f);
            cardStyle.SetCornerRadiusAll(4);
            cardStyle.BorderColor = isLeader ? new Color(0.9f, 0.7f, 0.1f, 0.4f) : new Color(0.2f, 0.2f, 0.3f, 0.3f);
            cardStyle.SetBorderWidthAll(1);
            card.AddThemeStyleboxOverride("panel", cardStyle);

            var hbox = new HBoxContainer();
            hbox.MouseFilter = MouseFilterEnum.Ignore;
            hbox.AddThemeConstantOverride("separation", 4);
            hbox.OffsetLeft = 4;
            hbox.OffsetTop = 2;
            hbox.OffsetRight = -4;
            hbox.OffsetBottom = -2;
            card.AddChild(hbox);

            Texture2D? classIcon = CarregarIconeClasse(charClass);
            if (classIcon != null)
            {
                var iconRect = new TextureRect();
                iconRect.Texture = classIcon;
                iconRect.CustomMinimumSize = new Vector2(22, 22);
                iconRect.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                hbox.AddChild(iconRect);
            }

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 1);
            vbox.MouseFilter = MouseFilterEnum.Ignore;

            var nameLevelLabel = new Label();
            string prefix = isLeader ? "[color=yellow](L)[/color] " : "";
            nameLevelLabel.Text = $"{prefix}{nome} | Nv. {level}";
            nameLevelLabel.AddThemeFontSizeOverride("font_size", 9);
            nameLevelLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 1, 0.9f));
            vbox.AddChild(nameLevelLabel);

            var bars = new VBoxContainer();
            bars.AddThemeConstantOverride("separation", 1);
            bars.MouseFilter = MouseFilterEnum.Ignore;

            var hpRow = new HBoxContainer();
            hpRow.AddThemeConstantOverride("separation", 3);
            hpRow.MouseFilter = MouseFilterEnum.Ignore;

            float hpPct = maxHp > 0 ? Mathf.Clamp((float)hp / maxHp, 0, 1) : 1f;
            var hpBarBg = new ColorRect();
            hpBarBg.CustomMinimumSize = new Vector2(0, 7);
            hpBarBg.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpBarBg.Color = new Color(0.15f, 0.05f, 0.05f, 0.6f);

            var hpBar = new ColorRect();
            hpBar.CustomMinimumSize = new Vector2(0, 7);
            hpBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpBar.Size = new Vector2(100 * hpPct, 7);
            hpBar.Color = new Color(0.9f, 0.12f, 0.12f, 0.92f);

            var hpContainer = new Control();
            hpContainer.CustomMinimumSize = new Vector2(0, 7);
            hpContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpContainer.AddChild(hpBarBg);
            hpContainer.AddChild(hpBar);

            var hpLabel = new Label();
            hpLabel.Text = $"{hp}/{maxHp}";
            hpLabel.AddThemeFontSizeOverride("font_size", 7);
            hpLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            hpLabel.CustomMinimumSize = new Vector2(52, 0);

            hpRow.AddChild(hpContainer);
            hpRow.AddChild(hpLabel);
            bars.AddChild(hpRow);

            var mpRow = new HBoxContainer();
            mpRow.AddThemeConstantOverride("separation", 3);
            mpRow.MouseFilter = MouseFilterEnum.Ignore;

            float mpPct = maxMp > 0 ? Mathf.Clamp((float)mp / maxMp, 0, 1) : 1f;
            var mpBarBg = new ColorRect();
            mpBarBg.CustomMinimumSize = new Vector2(0, 7);
            mpBarBg.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            mpBarBg.Color = new Color(0.05f, 0.05f, 0.2f, 0.6f);

            var mpBar = new ColorRect();
            mpBar.CustomMinimumSize = new Vector2(0, 7);
            mpBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            mpBar.Size = new Vector2(100 * mpPct, 7);
            mpBar.Color = new Color(0.2f, 0.4f, 1f, 0.85f);

            var mpContainer = new Control();
            mpContainer.CustomMinimumSize = new Vector2(0, 7);
            mpContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            mpContainer.AddChild(mpBarBg);
            mpContainer.AddChild(mpBar);

            var mpLabel = new Label();
            mpLabel.Text = $"{mp}/{maxMp}";
            mpLabel.AddThemeFontSizeOverride("font_size", 7);
            mpLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
            mpLabel.CustomMinimumSize = new Vector2(52, 0);

            mpRow.AddChild(mpContainer);
            mpRow.AddChild(mpLabel);
            bars.AddChild(mpRow);

            vbox.AddChild(bars);
            hbox.AddChild(vbox);
            _container.AddChild(card);
        }

        CallDeferred(nameof(AjustarAltura));
    }

    private void AjustarAltura()
    {
        float totalH = 8;
        foreach (var child in _container.GetChildren())
        {
            if (child is Control control)
                totalH += control.GetCombinedMinimumSize().Y + 2;
        }
        if (totalH < 8) totalH = 8;
        CustomMinimumSize = new Vector2(252, totalH);
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
}
