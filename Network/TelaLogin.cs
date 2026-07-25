#nullable enable
using Godot;

public partial class TelaLogin : CanvasLayer
{
    private const string SiteBaseUrl = "https://mithara.online";
    private LineEdit _username = null!;
    private LineEdit _password = null!;
    private Button _loginBtn = null!;
    private Label _status = null!;
    private CheckBox _lembrarCheck = null!;

    private LineEdit _regUsername = null!;
    private LineEdit _regEmail = null!;
    private LineEdit _regPassword = null!;
    private LineEdit _regConfirm = null!;
    private Button _registerBtn = null!;
    private Label _regStatus = null!;
    private Label _connectionStatusLabel = null!;
    private Label _connectionDetailLabel = null!;
    private Panel _connectionDot = null!;

    private LineEdit _recEmail = null!;
    private Label _recPerguntaLabel = null!;
    private LineEdit _recResposta = null!;
    private LineEdit _recNewPass = null!;
    private Button _recEnviarBtn = null!;
    private Label _recStatus = null!;

    private Control _uiRoot = null!;
    private Control _loginView = null!;
    private Control _registerView = null!;
    private Control _recoverView = null!;
    private Control _statusRoot = null!;
    private Texture2D _atlas = null!;
    private Texture2D _accountAtlas = null!;

    private const string CredentialsPath = "user://login_data.cfg";
    private static readonly Vector2 AtlasSize = new(1378, 1142);
    private static readonly Color Gold = new(1.0f, 0.75f, 0.30f);
    private static readonly Color PanelDark = new(0.025f, 0.022f, 0.018f, 0.93f);
    private static readonly Color FieldDark = new(0.015f, 0.014f, 0.012f, 0.96f);
    private static readonly Color GreenButton = new(0.10f, 0.28f, 0.04f, 0.98f);
    private static readonly Color BlueButton = new(0.04f, 0.12f, 0.22f, 0.98f);

    public override void _Ready()
    {
        var root = new Control();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var bgImg = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://Network/Tela de Login.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bgImg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bgImg);

        var overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.26f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(overlay);

        _atlas = ResourceLoader.Load<Texture2D>("res://Network/LoginUI/LoginAtlas.png");
        _accountAtlas = ResourceLoader.Load<Texture2D>("res://Network/LoginUI/AccountAtlas.png");
        _uiRoot = new Control
        {
            Size = AtlasSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(_uiRoot);

        _statusRoot = new Control
        {
            Size = new Vector2(340, 330),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(_statusRoot);

        CriarStatusServidor();
        CriarElementosFixos();
        _loginView = CriarLoginView();
        _registerView = CriarRegisterView();
        _recoverView = CriarRecoverView();

        _uiRoot.AddChild(_loginView);
        _uiRoot.AddChild(_registerView);
        _uiRoot.AddChild(_recoverView);

        _registerView.Visible = false;
        _recoverView.Visible = false;

        AddChild(root);
        AjustarLayoutResponsivo();
        GetTree().Root.SizeChanged += AjustarLayoutResponsivo;

        CarregarCredenciaisSalvas();
        ConectarSinais();
    }

    private void CriarElementosFixos()
    {
        AddAtlas(_uiRoot, new Rect2(430, 0, 522, 365), new Vector2(428, 0));

        AddAtlas(_uiRoot, new Rect2(46, 1050, 222, 76), new Vector2(46, 1050));
        var opcoes = AddInvisibleButton(_uiRoot, new Rect2(46, 1050, 222, 76));
        opcoes.Pressed += AbrirOpcoes;

        AddAtlas(_uiRoot, new Rect2(1048, 1054, 280, 72), new Vector2(1048, 1054));
        var sair = AddInvisibleButton(_uiRoot, new Rect2(1048, 1054, 280, 72));
        sair.Pressed += () => GetTree().Quit();

        AddAtlas(_uiRoot, new Rect2(410, 1064, 530, 76), new Vector2(424, 1064));
    }

    private void CriarStatusServidor()
    {
        AddAtlas(_statusRoot, new Rect2(30, 28, 340, 330), Vector2.Zero);
        _connectionDot = new Panel
        {
            Position = new Vector2(32, 76),
            Size = new Vector2(18, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _connectionDot.AddThemeStyleboxOverride("panel", CriarDotStyle(Colors.Red));
        _statusRoot.AddChild(_connectionDot);

        _connectionStatusLabel = new Label
        {
            Text = "Offline",
            Position = new Vector2(60, 66),
            Size = new Vector2(260, 30),
            VerticalAlignment = VerticalAlignment.Center,
        };
        PrepararLabel(_connectionStatusLabel, 18, Colors.White);
        _statusRoot.AddChild(_connectionStatusLabel);

        _connectionDetailLabel = new Label
        {
            Text = "Desconectado do servidor",
            Position = new Vector2(34, 110),
            Size = new Vector2(260, 70),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        PrepararLabel(_connectionDetailLabel, 15, new Color(0.88f, 0.84f, 0.74f));
        _statusRoot.AddChild(_connectionDetailLabel);
    }

    private Control CriarLoginView()
    {
        var view = new Control
        {
            Size = AtlasSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        AddAtlas(view, new Rect2(715, 350, 620, 670), new Vector2(379, 350));

        _username = CriarCampo(new Rect2(502, 548, 405, 38));
        _username.PlaceholderText = "Digite seu usuario";
        view.AddChild(_username);

        _password = CriarCampo(new Rect2(502, 658, 405, 38), true);
        _password.PlaceholderText = "Digite sua senha";
        view.AddChild(_password);

        _lembrarCheck = new CheckBox
        {
            Text = "",
            Position = new Vector2(446, 731),
            Size = new Vector2(34, 34),
            ButtonPressed = true,
        };
        view.AddChild(_lembrarCheck);

        var recuperar = new Button
        {
            Text = "",
            Position = new Vector2(749, 731),
            Size = new Vector2(188, 34),
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
        };
        recuperar.Pressed += AbrirRecuperacaoNoSite;
        view.AddChild(recuperar);

        _loginBtn = AddInvisibleButton(view, new Rect2(449, 792, 482, 70));
        _loginBtn.Pressed += OnLoginPressed;

        var criarConta = AddInvisibleButton(view, new Rect2(449, 925, 482, 70));
        criarConta.Pressed += AbrirCadastroNoSite;

        _status = CriarStatusLabel(new Rect2(434, 1005, 520, 34));
        view.AddChild(_status);

        return view;
    }

    private Control CriarRegisterView()
    {
        var view = new Control
        {
            Size = AtlasSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Vector2 panelPos = new(377, 344);
        Vector2 offset = panelPos - new Vector2(20, 318);
        AddAccountAtlas(view, new Rect2(20, 318, 623, 552), panelPos);

        var fechar = AddInvisibleButton(view, new Rect2(new Vector2(581, 342) + offset, new Vector2(33, 36)));
        fechar.Pressed += MostrarLogin;

        _regUsername = CriarCampo(new Rect2(new Vector2(164, 411) + offset, new Vector2(369, 35)));
        view.AddChild(_regUsername);

        _regEmail = CriarCampo(new Rect2(new Vector2(164, 482) + offset, new Vector2(369, 36)));
        view.AddChild(_regEmail);

        _regPassword = CriarCampo(new Rect2(new Vector2(164, 550) + offset, new Vector2(369, 36)), true);
        view.AddChild(_regPassword);

        _regConfirm = CriarCampo(new Rect2(new Vector2(164, 619) + offset, new Vector2(369, 36)), true);
        view.AddChild(_regConfirm);

        _registerBtn = AddInvisibleButton(view, new Rect2(new Vector2(148, 700) + offset, new Vector2(362, 52)));
        _registerBtn.Pressed += OnRegisterPressed;

        var voltar = AddInvisibleButton(view, new Rect2(new Vector2(148, 792) + offset, new Vector2(362, 49)));
        voltar.Pressed += MostrarLogin;

        _regStatus = CriarStatusLabel(new Rect2(new Vector2(125, 845) + offset, new Vector2(420, 36)));
        view.AddChild(_regStatus);

        return view;
    }

    private Control CriarRecoverView()
    {
        var view = new Control
        {
            Size = AtlasSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Vector2 panelPos = new(419, 344);
        Vector2 offset = panelPos - new Vector2(689, 317);
        AddAccountAtlas(view, new Rect2(689, 317, 539, 554), panelPos);

        var fechar = AddInvisibleButton(view, new Rect2(new Vector2(1162, 342) + offset, new Vector2(34, 36)));
        fechar.Pressed += MostrarLogin;

        _recEmail = CriarCampo(new Rect2(new Vector2(791, 547) + offset, new Vector2(374, 39)));
        view.AddChild(_recEmail);

        var buscar = AddInvisibleButton(view, new Rect2(new Vector2(784, 627) + offset, new Vector2(350, 51)));
        buscar.Pressed += OnBuscarPergunta;

        _recPerguntaLabel = CriarStatusLabel(new Rect2(new Vector2(795, 468) + offset, new Vector2(360, 48)));
        _recPerguntaLabel.AddThemeColorOverride("font_color", Gold);
        view.AddChild(_recPerguntaLabel);

        _recResposta = CriarCampo(new Rect2(new Vector2(791, 547) + offset, new Vector2(374, 39)));
        _recResposta.Visible = false;
        view.AddChild(_recResposta);

        _recNewPass = CriarCampo(new Rect2(new Vector2(791, 547) + offset, new Vector2(374, 39)), true);
        _recNewPass.Visible = false;
        view.AddChild(_recNewPass);

        _recEnviarBtn = buscar;

        var voltar = AddInvisibleButton(view, new Rect2(new Vector2(784, 738) + offset, new Vector2(350, 50)));
        voltar.Pressed += MostrarLogin;

        _recStatus = CriarStatusLabel(new Rect2(new Vector2(780, 795) + offset, new Vector2(370, 44)));
        view.AddChild(_recStatus);

        return view;
    }

    private void CriarJanelaBase(Control view, Rect2 rect)
    {
        var panel = new Panel
        {
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", CriarPanelStyle());
        view.AddChild(panel);
    }

    private void CriarRotulo(Control view, string text, Vector2 pos)
    {
        var label = new Label
        {
            Text = text,
            Position = pos,
            Size = new Vector2(220, 24),
        };
        PrepararLabel(label, 16, Gold);
        view.AddChild(label);
    }

    private void CriarCampoVisual(Control view, Rect2 rect, string iconText)
    {
        var panel = new Panel
        {
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", CriarFieldStyle());
        view.AddChild(panel);

        var icon = new Label
        {
            Text = iconText,
            Position = rect.Position + new Vector2(12, 9),
            Size = new Vector2(32, 32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        PrepararLabel(icon, 20, Gold);
        view.AddChild(icon);
    }

    private Control CriarSeparadorOu(Rect2 rect)
    {
        var row = new HBoxContainer
        {
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 12);

        var left = new ColorRect { Color = new Color(0.68f, 0.45f, 0.16f, 0.75f), CustomMinimumSize = new Vector2(180, 1) };
        var text = new Label { Text = "OU", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(48, 24) };
        var right = new ColorRect { Color = new Color(0.68f, 0.45f, 0.16f, 0.75f), CustomMinimumSize = new Vector2(180, 1) };
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        PrepararLabel(text, 20, new Color(0.92f, 0.86f, 0.72f));

        row.AddChild(left);
        row.AddChild(text);
        row.AddChild(right);
        return row;
    }

    private TextureRect AddAtlas(Control parent, Rect2 region, Vector2 position)
    {
        var atlasTexture = new AtlasTexture
        {
            Atlas = _atlas,
            Region = region,
        };
        var rect = new TextureRect
        {
            Texture = atlasTexture,
            Position = position,
            Size = region.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(rect);
        return rect;
    }

    private TextureRect AddAccountAtlas(Control parent, Rect2 region, Vector2 position)
    {
        var atlasTexture = new AtlasTexture
        {
            Atlas = _accountAtlas,
            Region = region,
        };
        var rect = new TextureRect
        {
            Texture = atlasTexture,
            Position = position,
            Size = region.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        parent.AddChild(rect);
        return rect;
    }

    private void CriarRodapeDireitos()
    {
        var footer = new Panel
        {
            Position = new Vector2(415, 1063),
            Size = new Vector2(520, 68),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        footer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.030f, 0.024f, 0.94f),
            BorderColor = new Color(0.72f, 0.50f, 0.20f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        });
        _uiRoot.AddChild(footer);

        var label = new Label
        {
            Text = "© 2024 MITHARA ONLINE: CONFLITO DE RAÇAS\nTODOS OS DIREITOS RESERVADOS",
            Position = new Vector2(12, 7),
            Size = new Vector2(496, 54),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        PrepararLabel(label, 17, new Color(0.95f, 0.84f, 0.58f));
        footer.AddChild(label);
    }

    private Button AddInvisibleButton(Control parent, Rect2 rect)
    {
        var button = new Button
        {
            Text = "",
            Position = rect.Position,
            Size = rect.Size,
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
        };
        var empty = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", empty);
        button.AddThemeStyleboxOverride("hover", empty);
        button.AddThemeStyleboxOverride("pressed", empty);
        button.AddThemeStyleboxOverride("focus", empty);
        parent.AddChild(button);
        return button;
    }

    private Button CriarBotaoTexto(string texto, Color color, Rect2 rect, int fontSize)
    {
        var button = new Button
        {
            Text = texto,
            Position = rect.Position,
            Size = rect.Size,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.AddThemeStyleboxOverride("normal", CriarButtonStyle(color, Gold));
        button.AddThemeStyleboxOverride("hover", CriarButtonStyle(color.Lightened(0.10f), Gold));
        button.AddThemeStyleboxOverride("pressed", CriarButtonStyle(color.Darkened(0.15f), Gold));
        button.AddThemeColorOverride("font_color", Gold);
        button.AddThemeColorOverride("font_outline_color", Colors.Black);
        button.AddThemeConstantOverride("outline_size", 2);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        return button;
    }

    private LineEdit CriarCampo(Rect2 rect, bool secret = false)
    {
        rect.Position += new Vector2(2, 0);
        rect.Size -= new Vector2(2, 0);

        var campo = new LineEdit
        {
            Position = rect.Position,
            Size = rect.Size,
            Secret = secret,
            PlaceholderText = "",
            FocusMode = Control.FocusModeEnum.Click,
        };
        var empty = new StyleBoxEmpty();
        campo.AddThemeStyleboxOverride("normal", empty);
        campo.AddThemeStyleboxOverride("focus", empty);
        campo.AddThemeStyleboxOverride("read_only", empty);
        campo.AddThemeColorOverride("font_color", Colors.White);
        campo.AddThemeColorOverride("font_placeholder_color", new Color(0.80f, 0.78f, 0.72f, 0.62f));
        campo.AddThemeColorOverride("caret_color", Gold);
        campo.AddThemeFontSizeOverride("font_size", 18);
        return campo;
    }

    private Label CriarStatusLabel(Rect2 rect)
    {
        var label = new Label
        {
            Text = "",
            Position = rect.Position,
            Size = rect.Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        PrepararLabel(label, 15, new Color(0.92f, 0.86f, 0.72f));
        return label;
    }

    private static void PrepararLabel(Label label, int fontSize, Color color)
    {
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static StyleBoxFlat CriarPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = PanelDark,
            BorderColor = Gold,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }

    private static StyleBoxFlat CriarFieldStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = FieldDark,
            BorderColor = new Color(0.52f, 0.33f, 0.12f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        };
    }

    private static StyleBoxFlat CriarButtonStyle(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
        };
    }

    private static StyleBoxFlat CriarDotStyle(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
        };
    }

    private void AjustarLayoutResponsivo()
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float scale = Mathf.Min(viewport.X / AtlasSize.X, viewport.Y / AtlasSize.Y);
        scale = Mathf.Min(scale, 1.15f);
        _uiRoot.Scale = new Vector2(scale, scale);
        _uiRoot.Position = (viewport - AtlasSize * scale) * 0.5f;

        _statusRoot.Scale = new Vector2(scale, scale);
        _statusRoot.Position = new Vector2(2, 2);
    }

    private void MostrarLogin()
    {
        _loginView.Visible = true;
        _registerView.Visible = false;
        _recoverView.Visible = false;
    }

    private void MostrarRegister()
    {
        _loginView.Visible = false;
        _registerView.Visible = true;
        _recoverView.Visible = false;
    }

    private void MostrarRecover()
    {
        _loginView.Visible = false;
        _registerView.Visible = false;
        _recoverView.Visible = true;
        _recPerguntaLabel.Text = "";
        _recStatus.Text = "";
    }

    private void AbrirCadastroNoSite()
    {
        _status.Text = "Abrindo cadastro no site...";
        OS.ShellOpen($"{SiteBaseUrl}/Account/Register");
    }

    private void AbrirRecuperacaoNoSite()
    {
        _status.Text = "Abrindo recuperacao de senha no site...";
        OS.ShellOpen($"{SiteBaseUrl}/Account/ForgotPassword");
    }

    private void AbrirOpcoes()
    {
        var root = GetTree().Root;
        var settings = GetNodeOrNull<SettingsUI>("LoginSettingsUI")
            ?? root.GetNodeOrNull<SettingsUI>("LoginSettingsUI");
        if (settings == null)
        {
            var scene = ResourceLoader.Load<PackedScene>("res://ui/SettingsUI.tscn");
            settings = scene.Instantiate<SettingsUI>();
            settings.Name = "LoginSettingsUI";
            AddChild(settings);
        }
        else if (settings.GetParent() != this)
        {
            settings.GetParent()?.RemoveChild(settings);
            AddChild(settings);
        }

        void AbrirSettings()
        {
            settings.GetNodeOrNull<Control>("SettingsToggleButton")?.Hide();
            settings.MoveToFront();
            settings.AbrirFechar(true);
        }

        if (settings.IsNodeReady())
            AbrirSettings();
        else
            settings.Ready += AbrirSettings;
    }

    private void CarregarCredenciaisSalvas()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(CredentialsPath) == Error.Ok)
        {
            _username.Text = cfg.GetValue("login", "username", "").AsString();
            _password.Text = "";
            _lembrarCheck.ButtonPressed = cfg.GetValue("login", "lembrar_usuario", false).AsBool()
                || cfg.GetValue("login", "lembrar", false).AsBool();
        }
    }

    private void SalvarCredenciais()
    {
        var cfg = new ConfigFile();
        if (_lembrarCheck.ButtonPressed)
        {
            cfg.SetValue("login", "username", _username.Text);
            cfg.SetValue("login", "password", "");
            cfg.SetValue("login", "lembrar", false);
            cfg.SetValue("login", "lembrar_usuario", true);
        }
        else
        {
            cfg.SetValue("login", "username", "");
            cfg.SetValue("login", "password", "");
            cfg.SetValue("login", "lembrar", false);
            cfg.SetValue("login", "lembrar_usuario", false);
        }
        cfg.Save(CredentialsPath);
    }

    private static void ResetarLoginState(GameNetwork net)
    {
        if (net.LoggedIn)
        {
            GD.Print("[TELA LOGIN] Resetando estado de login anterior.");
            net.LoggedIn = false;
            net.AccountId = 0;
            net.Characters.Clear();
        }
    }

    private void ConectarSinais()
    {
        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null)
        {
            _status.Text = "GameNetwork nao encontrado!";
            AtualizarStatusConexao(false);
            return;
        }

        ResetarLoginState(net);
        AtualizarStatusConexao(net.IsConnected);

        net.OnConnected += OnConnectedHandler;
        net.OnLoginResult += OnLoginResultHandler;
        net.OnRegisterResult += OnRegisterResultHandler;
        net.OnSecurityQuestion += OnSecurityQuestionHandler;
        net.OnRecoverResult += OnRecoverResultHandler;
        net.OnDisconnected += OnDisconnectedHandler;

        if (!net.IsConnected)
        {
            _status.Text = "Conectando ao servidor...";
            _regStatus.Text = "Conectando ao servidor...";
            _recStatus.Text = "Conectando ao servidor...";
            AtualizarStatusConexao(false);
            net.ConnectToServer();
        }
    }

    private void AtualizarStatusConexao(bool online)
    {
        if (_connectionStatusLabel == null || _connectionDot == null || _connectionDetailLabel == null)
            return;

        if (online)
        {
            _connectionStatusLabel.Text = "Online";
            _connectionDetailLabel.Text = "Conectado ao servidor";
            _connectionDot.AddThemeStyleboxOverride("panel", CriarDotStyle(Colors.LimeGreen));
        }
        else
        {
            _connectionStatusLabel.Text = "Offline";
            _connectionDetailLabel.Text = "Desconectado do servidor";
            _connectionDot.AddThemeStyleboxOverride("panel", CriarDotStyle(Colors.Red));
        }
    }

    private void OnLoginPressed()
    {
        _loginBtn.Disabled = true;
        _status.Text = "Enviando login...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
        {
            net.SendLogin(_username.Text.Trim(), _password.Text);
            var timer = GetTree().CreateTimer(10.0f);
            timer.Timeout += () =>
            {
                if (_loginBtn != null && _loginBtn.Disabled)
                {
                    _loginBtn.Disabled = false;
                    _status.Text = "Timeout: tente novamente.";
                }
            };
        }
        else
        {
            _status.Text = "Nao conectado ao servidor.";
            _loginBtn.Disabled = false;
        }
    }

    private void OnRegisterPressed()
    {
        string user = _regUsername.Text.Trim();
        string email = _regEmail.Text.Trim();
        string pass = _regPassword.Text;
        string confirm = _regConfirm.Text;

        if (user.Length < 3)
        {
            _regStatus.Text = "Usuario deve ter pelo menos 3 caracteres.";
            return;
        }
        if (pass.Length < 3)
        {
            _regStatus.Text = "Senha deve ter pelo menos 3 caracteres.";
            return;
        }
        if (!email.Contains('@') || email.Length < 6)
        {
            _regStatus.Text = "Digite um e-mail valido.";
            return;
        }
        if (pass != confirm)
        {
            _regStatus.Text = "Senhas nao conferem.";
            return;
        }

        _registerBtn.Disabled = true;
        _regStatus.Text = "Criando conta...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
        {
            net.SendRegister(user, email, pass);
            var timer = GetTree().CreateTimer(10.0f);
            timer.Timeout += () =>
            {
                if (_registerBtn != null && _registerBtn.Disabled)
                {
                    _registerBtn.Disabled = false;
                    _regStatus.Text = "Timeout: tente novamente.";
                }
            };
        }
        else
        {
            _regStatus.Text = "Nao conectado ao servidor.";
            _registerBtn.Disabled = false;
        }
    }

    private void OnBuscarPergunta()
    {
        _recStatus.Text = "Enviando solicitaÃ§Ã£o...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
            net.SendGetSecurityQuestion(_recEmail.Text.Trim());
        else
            _recStatus.Text = "Nao conectado ao servidor.";
    }

    private void OnRecoverPressed()
    {
        string answer = _recResposta.Text.Trim();
        string newPass = _recNewPass.Text;

        if (string.IsNullOrEmpty(answer))
        {
            _recStatus.Text = "Digite a resposta secreta.";
            return;
        }
        if (newPass.Length < 3)
        {
            _recStatus.Text = "Nova senha deve ter pelo menos 3 caracteres.";
            return;
        }

        _recEnviarBtn.Disabled = true;
        _recStatus.Text = "Redefinindo senha...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
            net.SendRecoverPassword(_recEmail.Text.Trim(), answer, newPass);
        else
        {
            _recStatus.Text = "Nao conectado ao servidor.";
            _recEnviarBtn.Disabled = false;
        }
    }

    private void OnConnectedHandler()
    {
        _status.Text = "Conectado ao servidor.";
        _regStatus.Text = "Conectado ao servidor.";
        _recStatus.Text = "Conectado ao servidor.";
        AtualizarStatusConexao(true);
    }

    private void OnLoginResultHandler(bool success, string message)
    {
        if (success)
        {
            _status.Text = "Login OK!";
            SalvarCredenciais();
            OnLoginSuccess();
        }
        else
        {
            _status.Text = $"Erro: {message}";
            _loginBtn.Disabled = false;
        }
    }

    private void OnRegisterResultHandler(bool success, string message)
    {
        if (success)
        {
            _regStatus.Text = "Conta criada! Faca login.";
            _regStatus.AddThemeColorOverride("font_color", Colors.Green);
            _registerBtn.Disabled = false;
        }
        else
        {
            _regStatus.Text = $"Erro: {message}";
            _regStatus.AddThemeColorOverride("font_color", Colors.Red);
            _registerBtn.Disabled = false;
        }
    }

    private void OnSecurityQuestionHandler(bool found, string questionOrError)
    {
        if (found)
        {
            _recPerguntaLabel.Text = questionOrError;
            _recPerguntaLabel.AddThemeColorOverride("font_color", Gold);
            _recStatus.Text = "";
        }
        else
        {
            _recPerguntaLabel.Text = "";
            _recStatus.Text = $"Erro: {questionOrError}";
            _recStatus.AddThemeColorOverride("font_color", Colors.Red);
        }
    }

    private void OnRecoverResultHandler(bool success, string message)
    {
        if (success)
        {
            _recStatus.Text = message;
            _recStatus.AddThemeColorOverride("font_color", Colors.Green);
            _recEnviarBtn.Disabled = false;
        }
        else
        {
            _recStatus.Text = $"Erro: {message}";
            _recStatus.AddThemeColorOverride("font_color", Colors.Red);
            _recEnviarBtn.Disabled = false;
        }
    }

    private void OnDisconnectedHandler()
    {
        _status.Text = "Desconectado do servidor.";
        _loginBtn.Disabled = false;
        _regStatus.Text = "Desconectado do servidor.";
        _registerBtn.Disabled = false;
        AtualizarStatusConexao(false);
    }

    public override void _ExitTree()
    {
        GetTree().Root.SizeChanged -= AjustarLayoutResponsivo;

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null)
        {
            net.OnConnected -= OnConnectedHandler;
            net.OnDisconnected -= OnDisconnectedHandler;
            net.OnLoginResult -= OnLoginResultHandler;
            net.OnRegisterResult -= OnRegisterResultHandler;
            net.OnSecurityQuestion -= OnSecurityQuestionHandler;
            net.OnRecoverResult -= OnRecoverResultHandler;
        }
    }

    private void OnLoginSuccess()
    {
        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += () =>
        {
            QueueFree();
            var menu = GetNodeOrNull<MenuInicial>(GetParent().GetPath());
            menu?.CallDeferred(nameof(MenuInicial.IrParaProximaCena));
        };
    }
}
