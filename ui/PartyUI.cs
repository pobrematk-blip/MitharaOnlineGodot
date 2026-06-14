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
        }

        AtualizarLista();
        CallDeferred(MethodName.Centralizar);
        GetTree().Root.SizeChanged += () => CallDeferred(MethodName.Centralizar);
        CriarBotaoToggle();
    }

    private void CriarBotaoToggle()
    {
        var btn = new TextureButton();
        btn.Name = "PartyToggleButton";
        btn.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Party.png");
        btn.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Party Selecionado.png");
        btn.CustomMinimumSize = new Vector2(36, 36);
        btn.StretchMode = TextureButton.StretchModeEnum.KeepCentered;
        btn.Pressed += () => AbrirFechar(null);
        AddChild(btn);
        AtualizarPosicaoBotao(btn);
        GetTree().Root.SizeChanged += () => AtualizarPosicaoBotao(btn);
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

    private void OnNetworkPartyMemberUpdate(ulong entityId, string name, int health, int maxHealth, int mana, int maxMana, int level, bool joined)
    {
        if (joined)
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
        AtualizarLista();
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
            bool isLeader = (bool)m["is_leader"];

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            float pct = maxHp > 0 ? (float)hp / maxHp : 1f;
            var hpBar = new ColorRect();
            hpBar.CustomMinimumSize = new Vector2(60, 8);
            hpBar.Size = new Vector2(60, 8);
            hpBar.Color = new Color(1f - pct, pct, 0, 0.8f);

            var nameLabel = new Label();
            nameLabel.Text = isLeader ? $"{nome} (L)" : nome;
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));

            var hpLabel = new Label();
            hpLabel.Text = $"{hp}/{maxHp}";
            hpLabel.AddThemeColorOverride("font_color", new Color(0, 1, 0, 0.7f));
            hpLabel.CustomMinimumSize = new Vector2(60, 0);

            if (nome != NomeJogador)
            {
                var kickBtn = new Button();
                kickBtn.Text = "X";
                kickBtn.CustomMinimumSize = new Vector2(22, 22);
                string captured = nome;
                kickBtn.Pressed += () => OnKick(captured);
                hbox.AddChild(kickBtn);
            }

            hbox.AddChild(hpBar);
            hbox.AddChild(nameLabel);
            hbox.AddChild(hpLabel);
            _membersList.AddChild(hbox);
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
