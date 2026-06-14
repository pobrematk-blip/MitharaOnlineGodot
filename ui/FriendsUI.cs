using Godot;
using System;
using System.Collections.Generic;

public partial class FriendsUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private LineEdit _addFriendInput;
    private Button _addFriendButton;
    private VBoxContainer _friendsList;
    private Label _emptyLabel;

    private List<FriendData> _friends = new();
    private const string FriendsPath = "user://amigos.cfg";
    private const string SectionFriends = "Amigos";

    public bool EstaAberto => _panel != null && _panel.Visible;

    private struct FriendData
    {
        public string Nome;
        public bool Online;
    }

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _addFriendInput = _panel.GetNode<LineEdit>("VBox/AddFriendHBox/AddFriendInput");
        _addFriendButton = _panel.GetNode<Button>("VBox/AddFriendHBox/AddFriendButton");
        _friendsList = _panel.GetNode<VBoxContainer>("VBox/ScrollContainer/FriendsList");
        _emptyLabel = _panel.GetNode<Label>("VBox/EmptyLabel");

        _closeButton.Pressed += OnClose;
        _titleBar.GuiInput += OnTitleBarGuiInput;
        _addFriendButton.Pressed += OnAddFriend;
        _addFriendInput.TextSubmitted += _ => OnAddFriend();

        _panel.Visible = false;

        CarregarAmigos();
        AtualizarLista();

        CallDeferred(MethodName.Centralizar);
        GetTree().Root.SizeChanged += () => CallDeferred(MethodName.Centralizar);

        CriarBotaoToggle();
    }

    private TextureButton? _toggleBtn;

    private void CriarBotaoToggle()
    {
        var btn = new TextureButton();
        btn.Name = "FriendsToggleButton";
        btn.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Amigos.png");
        btn.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Amigos Selecionado.png");
        btn.CustomMinimumSize = new Vector2(36, 36);
        btn.StretchMode = TextureButton.StretchModeEnum.KeepCentered;
        btn.Pressed += () => AbrirFechar(null);
        AddChild(btn);

        _toggleBtn = btn;
        AtualizarPosicaoBotao(btn);
        GetTree().Root.SizeChanged += () => AtualizarPosicaoBotao(btn);
    }

    private void AtualizarPosicaoBotao(Control btn)
    {
        Vector2 tela = GetViewportRect().Size;
        btn.Position = new Vector2(tela.X - 44, tela.Y - 88);
    }

    private void AtualizarPosicaoBotao(Button btn)
    {
        Vector2 tela = GetViewportRect().Size;
        btn.Position = new Vector2(tela.X - 44, tela.Y - 88);
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

    private void OnAddFriend()
    {
        string nome = _addFriendInput.Text.Trim();
        if (string.IsNullOrEmpty(nome)) return;

        foreach (var f in _friends)
        {
            if (string.Equals(f.Nome, nome, StringComparison.OrdinalIgnoreCase))
            {
                GD.Print($"[AMIGOS] Amigo '{nome}' ja existe!");
                _addFriendInput.Text = "";
                return;
            }
        }

        _friends.Add(new FriendData { Nome = nome, Online = false });
        _addFriendInput.Text = "";
        SalvarAmigos();
        AtualizarLista();
    }

    private void RemoverAmigo(string nome)
    {
        _friends.RemoveAll(f => string.Equals(f.Nome, nome, StringComparison.OrdinalIgnoreCase));
        SalvarAmigos();
        AtualizarLista();
    }

    private void AtualizarLista()
    {
        foreach (var child in _friendsList.GetChildren())
            child.QueueFree();

        if (_friends.Count == 0)
        {
            _emptyLabel.Visible = true;
            return;
        }

        _emptyLabel.Visible = false;

        foreach (var friend in _friends)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            var statusIcon = new TextureRect();
            statusIcon.CustomMinimumSize = new Vector2(16, 16);
            statusIcon.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
            statusIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            statusIcon.Texture = GD.Load<Texture2D>(friend.Online
                ? "res://ui/Incone de Menu/Amigos Online.png"
                : "res://ui/Incone de Menu/Amigos Offline.png");

            var nameLabel = new Label();
            nameLabel.Text = friend.Nome;
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));

            var statusLabel = new Label();
            statusLabel.Text = friend.Online ? "Online" : "Offline";
            Color statusColor = friend.Online
                ? new Color(0, 1, 0, 0.8f)
                : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            statusLabel.AddThemeColorOverride("font_color", statusColor);
            statusLabel.CustomMinimumSize = new Vector2(50, 0);

            var removeBtn = new Button();
            removeBtn.Text = "X";
            removeBtn.CustomMinimumSize = new Vector2(24, 24);
            string captured = friend.Nome;
            removeBtn.Pressed += () => RemoverAmigo(captured);

            hbox.AddChild(statusIcon);
            hbox.AddChild(nameLabel);
            hbox.AddChild(statusLabel);
            hbox.AddChild(removeBtn);

            _friendsList.AddChild(hbox);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (@event.IsActionPressed("amigos"))
        {
            AbrirFechar(null);
            GetViewport().SetInputAsHandled();
        }
    }

    private void SalvarAmigos()
    {
        var cfg = new ConfigFile();
        string[] nomes = new string[_friends.Count];
        for (int i = 0; i < _friends.Count; i++)
            nomes[i] = _friends[i].Nome;
        cfg.SetValue(SectionFriends, "lista", string.Join(",", nomes));
        cfg.Save(FriendsPath);
    }

    private void CarregarAmigos()
    {
        _friends.Clear();
        var cfg = new ConfigFile();
        if (cfg.Load(FriendsPath) != Error.Ok) return;

        string raw = cfg.GetValue(SectionFriends, "lista", "").AsString();
        if (string.IsNullOrEmpty(raw)) return;

        string[] nomes = raw.Split(",", StringSplitOptions.RemoveEmptyEntries);
        foreach (var nome in nomes)
            _friends.Add(new FriendData { Nome = nome.Trim(), Online = false });
    }
}
