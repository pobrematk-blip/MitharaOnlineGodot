using Godot;
using System.Collections.Generic;

public partial class GuildUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private TabContainer _tabContainer;

    private LineEdit _inviteInput;
    private Button _inviteButton;
    private Button _baseTeleportButton;
    private Label _guildNameLabel;
    private Label _guildLevelLabel;
    private Label _memberCountLabel;
    private Label _xpLabel;
    private ProgressBar _xpBar;
    private VBoxContainer _membersList;
    private Label _emptyLabel;

    private Label _skillPointsLabel;
    private VBoxContainer _skillsList;

    [Signal] public delegate void BaseTeleportRequestedEventHandler();

    private int _guildId;
    private string _guildName = "";
    private List<Godot.Collections.Dictionary> _membros = new();
    private int _guildLevel = 1;
    private int _guildXp;
    private int _skillPoints;
    private Dictionary<string, int> _skillLevels = new();

    private string NomeJogador
    {
        get
        {
            var p = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
            return p?.NomePersonagem ?? "Aventureiro";
        }
    }

    private static readonly int[] XpLimites = { 100, 250, 500, 1000 };
    private static readonly int[] SlotsPorLevel = { 0, 10, 15, 20, 25, 30 };

    private static readonly string[] Cargos = { "Lider", "Capitao", "Oficial", "Membro", "Novato" };

    private struct SkillDef
    {
        public string Id;
        public string Nome;
        public string DescFormat;
        public int MaxLevel;
        public int ValorPorNivel;
        public string Unidade;
        public SkillDef(string id, string nome, string desc, int max, int val, string unid)
        { Id = id; Nome = nome; DescFormat = desc; MaxLevel = max; ValorPorNivel = val; Unidade = unid; }
    }

    private static readonly SkillDef[] Skills =
    {
        new("hp", "Vitalidade", "HP maximo +{0} para todos", 5, 50, ""),
        new("xp", "Sabedoria", "XP ganho +{0}% para todos", 5, 5, "%"),
        new("defesa", "Armadura", "Defesa +{0} para todos", 5, 10, ""),
        new("dano", "Forca", "Dano +{0}% para todos", 5, 3, "%"),
        new("velocidade", "Agilidade", "Velocidade +{0}% para todos", 5, 2, "%"),
        new("drop", "Fortuna", "Drop rate +{0}% para todos", 5, 5, "%"),
    };

    public bool EstaAberto => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _tabContainer = _panel.GetNode<TabContainer>("TabContainer");

        _inviteInput = _panel.GetNode<LineEdit>("TabContainer/Membros/InviteHBox/InviteInput");
        _inviteButton = _panel.GetNode<Button>("TabContainer/Membros/InviteHBox/InviteButton");
        _baseTeleportButton = _panel.GetNode<Button>("TabContainer/Membros/BaseTeleportButton");
        _guildNameLabel = _panel.GetNode<Label>("TabContainer/Membros/GuildNameLabel");
        _guildLevelLabel = _panel.GetNode<Label>("TabContainer/Membros/GuildLevelLabel");
        _memberCountLabel = _panel.GetNode<Label>("TabContainer/Membros/MemberCountLabel");
        _xpLabel = _panel.GetNode<Label>("TabContainer/Membros/XpLabel");
        _xpBar = _panel.GetNode<ProgressBar>("TabContainer/Membros/XpBar");
        _membersList = _panel.GetNode<VBoxContainer>("TabContainer/Membros/ScrollContainer/MembersList");
        _emptyLabel = _panel.GetNode<Label>("TabContainer/Membros/EmptyLabel");

        _skillPointsLabel = _panel.GetNode<Label>("TabContainer/Habilidades/SkillPointsLabel");
        _skillsList = _panel.GetNode<VBoxContainer>("TabContainer/Habilidades/ScrollContainer/SkillsList");

        _closeButton.Pressed += OnClose;
        _titleBar.GuiInput += OnTitleBarGuiInput;
        _inviteButton.Pressed += OnInvite;
        _inviteInput.TextSubmitted += _ => OnInvite();
        _baseTeleportButton.Pressed += () => EmitSignal(SignalName.BaseTeleportRequested);

        _panel.Visible = false;

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnGuildData += OnNetworkGuildData;
            net.OnGuildMemberUpdate += OnNetworkGuildMemberUpdate;
            net.OnGuildRankUpdate += OnNetworkGuildRankUpdate;
            net.OnGuildSkillUpdate += OnNetworkGuildSkillUpdate;
        }

        foreach (var skill in Skills)
            _skillLevels.TryAdd(skill.Id, 0);

        AtualizarLista();
        AtualizarSkills();

        CallDeferred(MethodName.Centralizar);
        GetTree().Root.SizeChanged += () => CallDeferred(MethodName.Centralizar);
        CriarBotaoToggle();
    }

    private void CriarBotaoToggle()
    {
        var btn = new TextureButton();
        btn.Name = "GuildToggleButton";
        btn.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Guild.png");
        btn.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Guild Selecionado.png");
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
        btn.Position = new Vector2(tela.X - 44, tela.Y - 132);
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (@event.IsActionPressed("guild"))
        {
            AbrirFechar(null);
            GetViewport().SetInputAsHandled();
        }
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
        if (newState) Centralizar();
        _arrastando = false;
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

    private int MeuCargo()
    {
        foreach (var m in _membros)
        {
            string name = (string)m["name"];
            if (name == NomeJogador) return (int)m["rank"];
        }
        return 4;
    }

    private int MaxSlots() => _guildLevel >= 1 && _guildLevel <= 5 ? SlotsPorLevel[_guildLevel] : 10;

    private int XpParaProximo()
    {
        if (_guildLevel >= 5) return 0;
        return XpLimites[_guildLevel - 1];
    }

    // ================= NETWORK CALLBACKS =================

    private void OnNetworkGuildData(int guildId, string guildName, Godot.Collections.Array<Godot.Collections.Dictionary> members, int level, int xp, int skillPoints, Godot.Collections.Array<Godot.Collections.Dictionary> skills)
    {
        _guildId = guildId;
        _guildName = guildName;
        _guildLevel = level;
        _guildXp = xp;
        _skillPoints = skillPoints;

        _membros.Clear();
        foreach (var m in members)
            _membros.Add(m);

        _skillLevels.Clear();
        foreach (var s in skills)
            _skillLevels[(string)s["id"]] = (int)s["level"];

        AtualizarLista();
        AtualizarSkills();
    }

    private void OnNetworkGuildMemberUpdate(ulong entityId, string name, int rank, bool joined)
    {
        if (joined)
        {
            _membros.Add(new Godot.Collections.Dictionary
            {
                ["entity_id"] = (long)entityId, ["name"] = name, ["rank"] = rank,
            });
        }
        else
        {
            _membros.RemoveAll(m => (string)m["name"] == name);
            if (_membros.Count == 0)
            {
                _guildId = 0;
                _guildName = "";
            }
        }
        AtualizarLista();
    }

    private void OnNetworkGuildRankUpdate(ulong entityId, int newRank)
    {
        foreach (var m in _membros)
        {
            if ((ulong)(long)m["entity_id"] == entityId)
            {
                m["rank"] = newRank;
                break;
            }
        }
        AtualizarLista();
    }

    private void OnNetworkGuildSkillUpdate(string skillId, int newLevel)
    {
        _skillLevels[skillId] = newLevel;
        AtualizarSkills();
    }

    // ================= ACTIONS =================

    private void OnInvite()
    {
        string nome = _inviteInput.Text.Trim();
        if (string.IsNullOrEmpty(nome)) return;

        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendGuildInvite(nome);
        _inviteInput.Text = "";
    }

    private void OnKick(string nome)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendGuildKick(nome);
    }

    private void OnPromover(string nome)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendGuildPromote(nome);
    }

    private void OnRebaixar(string nome)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendGuildDemote(nome);
    }

    private void OnComprarSkill(string skillId)
    {
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendGuildBuySkill(skillId);
    }

    // ================= UI =================

    private void AtualizarLista()
    {
        foreach (var child in _membersList.GetChildren())
            child.QueueFree();

        int maxSlots = MaxSlots();
        _guildNameLabel.Text = _guildId > 0 ? _guildName : "Sem Guilda";
        _guildLevelLabel.Text = $"Nivel {_guildLevel}";
        _memberCountLabel.Text = $"Membros: {_membros.Count}/{maxSlots}";

        if (_guildLevel >= 5)
        {
            _xpBar.Visible = false;
            _xpLabel.Text = "Nivel Maximo!";
        }
        else
        {
            _xpBar.Visible = true;
            int needed = XpParaProximo();
            _xpBar.MaxValue = needed;
            _xpBar.Value = _guildXp;
            _xpLabel.Text = $"XP: {_guildXp}/{needed}";
        }

        if (_membros.Count == 0) { _emptyLabel.Visible = true; return; }
        _emptyLabel.Visible = false;
        int meuCargo = MeuCargo();

        foreach (var m in _membros)
        {
            string nome = (string)m["name"];
            int cargoIdx = (int)m["rank"];

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);

            var statusRect = new ColorRect();
            statusRect.CustomMinimumSize = new Vector2(10, 10);
            statusRect.Size = new Vector2(10, 10);
            statusRect.Color = cargoIdx == 0 ? new Color(1, 0.8f, 0, 0.9f) : new Color(0, 1, 0, 0.9f);

            var nameLabel = new Label();
            nameLabel.Text = nome;
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));

            Color cargoColor = cargoIdx switch
            {
                0 => new Color(1, 0.8f, 0, 0.9f),
                1 => new Color(0, 0.7f, 1, 0.9f),
                2 => new Color(0.6f, 0.4f, 1, 0.9f),
                3 => new Color(0.6f, 0.6f, 0.8f, 0.7f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.7f)
            };

            var cargoLabel = new Label();
            cargoLabel.Text = Cargos[cargoIdx];
            cargoLabel.AddThemeColorOverride("font_color", cargoColor);
            cargoLabel.CustomMinimumSize = new Vector2(70, 0);

            hbox.AddChild(statusRect);
            hbox.AddChild(nameLabel);
            hbox.AddChild(cargoLabel);

            if (nome != NomeJogador && meuCargo == 0)
            {
                bool podeSubir = cargoIdx > 0;
                bool podeDescer = cargoIdx < 4 && cargoIdx > 0;

                if (podeSubir)
                {
                    var upBtn = new Button();
                    upBtn.Text = "\u25B2";
                    upBtn.CustomMinimumSize = new Vector2(22, 22);
                    string captured = nome;
                    upBtn.Pressed += () => OnPromover(captured);
                    hbox.AddChild(upBtn);
                }
                if (podeDescer)
                {
                    var downBtn = new Button();
                    downBtn.Text = "\u25BC";
                    downBtn.CustomMinimumSize = new Vector2(22, 22);
                    string captured = nome;
                    downBtn.Pressed += () => OnRebaixar(captured);
                    hbox.AddChild(downBtn);
                }
                var kickBtn = new Button();
                kickBtn.Text = "X";
                kickBtn.CustomMinimumSize = new Vector2(22, 22);
                string capturedKick = nome;
                kickBtn.Pressed += () => OnKick(capturedKick);
                hbox.AddChild(kickBtn);
            }
            else if (nome != NomeJogador && meuCargo == 1 && cargoIdx > 2)
            {
                var kickBtn = new Button();
                kickBtn.Text = "X";
                kickBtn.CustomMinimumSize = new Vector2(22, 22);
                string capturedKick = nome;
                kickBtn.Pressed += () => OnKick(capturedKick);
                hbox.AddChild(kickBtn);
            }
            else if (nome != NomeJogador && meuCargo == 2 && cargoIdx > 3)
            {
                var kickBtn = new Button();
                kickBtn.Text = "X";
                kickBtn.CustomMinimumSize = new Vector2(22, 22);
                string capturedKick = nome;
                kickBtn.Pressed += () => OnKick(capturedKick);
                hbox.AddChild(kickBtn);
            }

            _membersList.AddChild(hbox);
        }
    }

    private void AtualizarSkills()
    {
        foreach (var child in _skillsList.GetChildren())
            child.QueueFree();

        _skillPointsLabel.Text = $"Pontos de Habilidade: {_skillPoints}";
        bool ehLider = MeuCargo() == 0;

        foreach (var def in Skills)
        {
            int nivel = _skillLevels.GetValueOrDefault(def.Id, 0);
            int valorTotal = nivel * def.ValorPorNivel;
            bool maxed = nivel >= def.MaxLevel;
            bool podeComprar = ehLider && _skillPoints > 0 && !maxed;

            var panel = new Panel();
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.CustomMinimumSize = new Vector2(0, 48);
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            bgStyle.SetCornerRadiusAll(4);
            bgStyle.ContentMarginLeft = 8;
            bgStyle.ContentMarginTop = 6;
            bgStyle.ContentMarginRight = 8;
            bgStyle.ContentMarginBottom = 6;
            panel.AddThemeStyleboxOverride("panel", bgStyle);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 4);

            var topHbox = new HBoxContainer();
            topHbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            topHbox.AddThemeConstantOverride("separation", 6);

            var nomeLabel = new Label();
            nomeLabel.Text = def.Nome;
            nomeLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nomeLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
            nomeLabel.AddThemeFontSizeOverride("font_size", 12);

            var levelLabel = new Label();
            levelLabel.Text = maxed ? "MAX" : $"Lv.{nivel}/{def.MaxLevel}";
            levelLabel.AddThemeColorOverride("font_color", maxed ? new Color(0.2f, 1, 0.2f, 0.9f) : new Color(0.6f, 0.6f, 1, 0.8f));
            levelLabel.AddThemeFontSizeOverride("font_size", 10);
            levelLabel.CustomMinimumSize = new Vector2(50, 0);

            topHbox.AddChild(nomeLabel);
            topHbox.AddChild(levelLabel);

            var descLabel = new Label();
            descLabel.Text = string.Format(def.DescFormat, valorTotal);
            descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.9f, 0.7f));
            descLabel.AddThemeFontSizeOverride("font_size", 9);

            vbox.AddChild(topHbox);
            vbox.AddChild(descLabel);

            if (!maxed)
            {
                var barHbox = new HBoxContainer();
                barHbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                barHbox.AddThemeConstantOverride("separation", 4);

                var bar = new ProgressBar();
                bar.MaxValue = def.MaxLevel;
                bar.Value = nivel;
                bar.ShowPercentage = false;
                bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                bar.CustomMinimumSize = new Vector2(0, 10);
                bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.2f, 0.6f, 0.9f, 1) });

                barHbox.AddChild(bar);

                if (podeComprar)
                {
                    var comprarBtn = new Button();
                    comprarBtn.Text = "+";
                    comprarBtn.CustomMinimumSize = new Vector2(24, 20);
                    string captured = def.Id;
                    comprarBtn.Pressed += () => OnComprarSkill(captured);
                    barHbox.AddChild(comprarBtn);
                }

                vbox.AddChild(barHbox);
            }

            panel.AddChild(vbox);
            _skillsList.AddChild(panel);
        }
    }
}
