using Godot;
using System.Collections.Generic;

public partial class PartyUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private LineEdit _inviteInput;
    private Button _inviteButton;
    private VBoxContainer _membersList;
    private Label _emptyLabel;

    private int _partyId;
    private ulong _leaderId;
    private List<Godot.Collections.Dictionary> _members = new();
    private const int MaxParty = 5;
    private TextureButton _toggleButton;

    private string NomeJogador
    {
        get
        {
            var p = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
            return p?.NomePersonagem ?? "Aventureiro";
        }
    }

    public bool EstaAberto => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _inviteInput = _panel.GetNode<LineEdit>("VBox/InviteHBox/InviteInput");
        _inviteButton = _panel.GetNode<Button>("VBox/InviteHBox/InviteButton");
        _membersList = _panel.GetNode<VBoxContainer>("VBox/ScrollContainer/MembersList");
        _emptyLabel = _panel.GetNode<Label>("VBox/EmptyLabel");

        _closeButton.Pressed += OnClose;
        _titleBar.GuiInput += OnTitleBarGuiInput;
        _inviteButton.Pressed += OnInvite;
        _inviteInput.TextSubmitted += _ => OnInvite();

        _panel.Visible = false;

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

        AtualizarLista();
        CallDeferred(MethodName.Centralizar);
        GetTree().Root.SizeChanged += OnRootSizeChanged;
        CriarBotaoToggle();
    }

    private void CriarBotaoToggle()
    {
        _toggleButton = new TextureButton();
        _toggleButton.Name = "PartyToggleButton";
        _toggleButton.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Party.png");
        _toggleButton.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Party Selecionado.png");
        _toggleButton.CustomMinimumSize = new Vector2(36, 36);
        _toggleButton.StretchMode = TextureButton.StretchModeEnum.KeepCentered;
        _toggleButton.Pressed += () => AbrirFechar(null);
        AddChild(_toggleButton);
        AtualizarPosicaoBotao(_toggleButton);
        GetTree().Root.SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged()
    {
        if (_toggleButton != null)
            AtualizarPosicaoBotao(_toggleButton);
    }

    private void AtualizarPosicaoBotao(Control btn)
    {
        Vector2 tela = GetViewportRect().Size;
        btn.Position = new Vector2(tela.X - 44, tela.Y - 220);
    }

    private void Centralizar()
    {
        if (_panel == null) return;
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    private void OnClose() { AbrirFechar(false); }

    public void AbrirFechar(bool? forcedState = null)
    {
        bool newState = forcedState ?? !_panel.Visible;
        _panel.Visible = newState;
        if (newState)
        {
            Centralizar();
            TrazerParaFrente();
        }
        _arrastando = false;
    }

    private void TrazerParaFrente()
    {
        var parent = GetParent();
        if (parent != null)
            parent.MoveChild(this, parent.GetChildCount() - 1);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
    }

    private void OnInvite()
    {
        string nome = _inviteInput.Text.Trim();
        if (string.IsNullOrEmpty(nome)) return;

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.SendPartyInvite(nome);
        }

        _inviteInput.Text = "";
    }

    private void OnKick(string nome)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendPartyKick(nome);
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
        AtualizarLista();
        NotificarPartyHUD();
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
        AtualizarLista();
        NotificarPartyHUD();
    }

    private void OnNetworkPartyLeaderUpdate(ulong newLeaderId)
    {
        _leaderId = newLeaderId;
        foreach (var m in _members)
            m["is_leader"] = (ulong)(long)m["entity_id"] == newLeaderId;
        AtualizarLista();
    }

    private void OnEntityHealthUpdate(ulong entityId, int health, int maxHealth)
    {
        var member = FindMember(entityId);
        if (member == null) return;

        member["health"] = health;
        member["max_health"] = maxHealth;
        AtualizarLista();
        NotificarPartyHUD();
    }

    private void OnEntityManaUpdate(ulong entityId, int mana, int maxMana)
    {
        var member = FindMember(entityId);
        if (member == null) return;

        member["mana"] = mana;
        member["max_mana"] = maxMana;
        AtualizarLista();
        NotificarPartyHUD();
    }

    private Godot.Collections.Dictionary FindMember(ulong entityId)
    {
        return _members.Find(m => (ulong)(long)m["entity_id"] == entityId);
    }

    private void AtualizarLista()
    {
        foreach (var child in _membersList.GetChildren())
            child.QueueFree();

        if (_members.Count == 0)
        {
            _emptyLabel.Visible = true;
            return;
        }

        _emptyLabel.Visible = false;

        foreach (var m in _members)
        {
            string nome = (string)m["name"];
            int hp = (int)m["health"];
            int maxHp = (int)m["max_health"];
            int mp = m.ContainsKey("mana") ? (int)m["mana"] : 0;
            int maxMp = m.ContainsKey("max_mana") ? (int)m["max_mana"] : 0;
            int level = m.ContainsKey("level") ? (int)m["level"] : 1;
            bool isLeader = (bool)m["is_leader"];
            string charClass = m.ContainsKey("class") ? (string)m["class"] : "";

            var memberPanel = new Panel();
            memberPanel.CustomMinimumSize = new Vector2(280, 56);

            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0, 0, 0, 0.35f);
            panelStyle.SetCornerRadiusAll(4);
            panelStyle.BorderColor = isLeader ? new Color(0.9f, 0.7f, 0.1f, 0.5f) : new Color(0.2f, 0.2f, 0.3f, 0.4f);
            panelStyle.SetBorderWidthAll(1);
            if (isLeader)
                panelStyle.SetBorderWidthAll(2);
            memberPanel.AddThemeStyleboxOverride("panel", panelStyle);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 2);

            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 6);

            var nameLabel = new Label();
            nameLabel.Text = isLeader ? $"{nome} [L]" : nome;
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 1, 0.9f));

            var levelLabel = new Label();
            levelLabel.Text = $"Lv.{level}";
            levelLabel.AddThemeFontSizeOverride("font_size", 11);
            levelLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.3f, 0.85f));

            topRow.AddChild(nameLabel);
            topRow.AddChild(levelLabel);

            if (nome != NomeJogador)
            {
                var kickBtn = new Button();
                kickBtn.Text = "X";
                kickBtn.CustomMinimumSize = new Vector2(22, 22);
                kickBtn.AddThemeFontSizeOverride("font_size", 10);
                string captured = nome;
                kickBtn.Pressed += () => OnKick(captured);
                topRow.AddChild(kickBtn);
            }

            vbox.AddChild(topRow);

            var bars = new VBoxContainer();
            bars.AddThemeConstantOverride("separation", 1);
            bars.MouseFilter = Control.MouseFilterEnum.Ignore;

            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 10);
            hpBar.MaxValue = System.Math.Max(1, maxHp);
            hpBar.Value = System.Math.Clamp(hp, 0, System.Math.Max(1, maxHp));
            hpBar.ShowPercentage = false;
            var hpBg = new StyleBoxFlat { BgColor = new Color(0.15f, 0.04f, 0.04f, 0.85f) };
            hpBg.SetCornerRadiusAll(3);
            var hpFill = new StyleBoxFlat { BgColor = new Color(0.9f, 0.12f, 0.12f, 0.95f) };
            hpFill.SetCornerRadiusAll(3);
            hpBar.AddThemeStyleboxOverride("background", hpBg);
            hpBar.AddThemeStyleboxOverride("fill", hpFill);
            bars.AddChild(hpBar);

            var mpBar = new ProgressBar();
            mpBar.CustomMinimumSize = new Vector2(0, 10);
            mpBar.MaxValue = System.Math.Max(1, maxMp);
            mpBar.Value = System.Math.Clamp(mp, 0, System.Math.Max(1, maxMp));
            mpBar.ShowPercentage = false;
            var mpBg = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.15f, 0.85f) };
            mpBg.SetCornerRadiusAll(3);
            var mpFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.4f, 1f, 0.95f) };
            mpFill.SetCornerRadiusAll(3);
            mpBar.AddThemeStyleboxOverride("background", mpBg);
            mpBar.AddThemeStyleboxOverride("fill", mpFill);
            bars.AddChild(mpBar);

            vbox.AddChild(bars);
            memberPanel.AddChild(vbox);
            _membersList.AddChild(memberPanel);
        }
    }

    private void OnRootSizeChanged()
    {
        CallDeferred(MethodName.Centralizar);
    }

    public override void _ExitTree()
    {
        GetTree().Root.SizeChanged -= OnRootSizeChanged;
        if (_toggleButton != null)
            GetTree().Root.SizeChanged -= OnSizeChanged;

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnPartyData -= OnNetworkPartyData;
            net.OnPartyMemberUpdate -= OnNetworkPartyMemberUpdate;
            net.OnPartyLeaderUpdate -= OnNetworkPartyLeaderUpdate;
            net.OnEntityHealthUpdate -= OnEntityHealthUpdate;
            net.OnEntityManaUpdate -= OnEntityManaUpdate;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (@event.IsActionPressed("party"))
        {
            AbrirFechar(null);
            GetViewport().SetInputAsHandled();
        }
    }

    private void NotificarPartyHUD()
    {
        var hud = GetTree()?.CurrentScene?.FindChild("PartyHUD", true, false) as PartyHUD;
        hud?.Refresh();
    }
}
