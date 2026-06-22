#nullable enable
using Godot;

public partial class TelaLogin : CanvasLayer
{
    private LineEdit _username = null!;
    private LineEdit _password = null!;
    private Button _loginBtn = null!;
    private Label _status = null!;
    private CheckBox _lembrarCheck = null!;

    private LineEdit _regUsername = null!;
    private LineEdit _regPassword = null!;
    private LineEdit _regConfirm = null!;
    private Button _registerBtn = null!;
    private Label _regStatus = null!;
    private Label _connectionStatusLabel = null!;
    private Label _connectionDetailLabel = null!;
    private Panel _connectionDot = null!;

    private LineEdit _recUsername = null!;
    private Label _recPerguntaLabel = null!;
    private LineEdit _recResposta = null!;
    private LineEdit _recNewPass = null!;
    private Button _recEnviarBtn = null!;
    private Label _recStatus = null!;

    private Panel _loginPanel = null!;
    private Panel _registerPanel = null!;
    private Panel _recoverPanel = null!;
    private CenterContainer _centerLogin = null!;
    private CenterContainer _centerRegister = null!;
    private CenterContainer _centerRecover = null!;

    private const string CredentialsPath = "user://login_data.cfg";

    public override void _Ready()
    {
        var root = new Panel();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var bgImg = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>("res://Network/Tela de Login.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bgImg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bgImg);

        var overlay = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(overlay);

        _loginPanel = CriarLoginPanel();
        _registerPanel = CriarRegisterPanel();
        _recoverPanel = CriarRecoverPanel();

        _centerLogin = new CenterContainer();
        _centerLogin.SetAnchorsPreset(Control.LayoutPreset.Center);
        _centerLogin.Position = new Vector2(-92, 81); // Moved additional 1.5cm left from previous position
        _centerLogin.AddChild(_loginPanel);
        root.AddChild(_centerLogin);

        _centerRegister = new CenterContainer();
        _centerRegister.SetAnchorsPreset(Control.LayoutPreset.Center);
        _centerRegister.Position = new Vector2(-92, 81); // Moved additional 1.5cm left from previous position
        _registerPanel.CustomMinimumSize = new Vector2(360, 0);
        _centerRegister.AddChild(_registerPanel);
        root.AddChild(_centerRegister);

        _centerRecover = new CenterContainer();
        _centerRecover.SetAnchorsPreset(Control.LayoutPreset.Center);
        _centerRecover.Position = new Vector2(-92, 81); // Moved additional 1.5cm left from previous position
        var recoverVBox = new VBoxContainer();
        recoverVBox.SetAnchorsPreset(Control.LayoutPreset.Center);
        var spacerRec = new Control();
        spacerRec.SizeFlagsVertical = Control.SizeFlags.Expand;
        spacerRec.CustomMinimumSize = new Vector2(0, 200);
        recoverVBox.AddChild(spacerRec);
        
        var hboxRec = new HBoxContainer();
        var hspacerRecLeft = new Control();
        hspacerRecLeft.SizeFlagsHorizontal = Control.SizeFlags.Expand;
        hspacerRecLeft.CustomMinimumSize = new Vector2(120, 0);
        hboxRec.AddChild(hspacerRecLeft);
        hboxRec.AddChild(_recoverPanel);
        var hspacerRecRight = new Control();
        hspacerRecRight.SizeFlagsHorizontal = Control.SizeFlags.Expand;
        hboxRec.AddChild(hspacerRecRight);
        
        recoverVBox.AddChild(hboxRec);
        _centerRecover.AddChild(recoverVBox);
        root.AddChild(_centerRecover);

        var connectionPanel = new Panel();
        connectionPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        connectionPanel.Size = new Vector2(220, 50);  // Reasonable size
        connectionPanel.Position = new Vector2(-20, -20);  // Standard position
        connectionPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.6f),  // Semi-transparent dark
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });

        var connectionVBox = new VBoxContainer();
        connectionVBox.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        connectionVBox.SizeFlagsVertical = Control.SizeFlags.Fill;
        connectionVBox.CustomMinimumSize = new Vector2(220, 50);

        var statusRow = new HBoxContainer();
        statusRow.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        statusRow.SizeFlagsVertical = Control.SizeFlags.Fill;

        _connectionDot = new Panel();
        _connectionDot.CustomMinimumSize = new Vector2(14, 14);  // Good size dot
        _connectionDot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Red,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
        });
        statusRow.AddChild(_connectionDot);
        
        // Add spacer to push status label to the right
        statusRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.Expand });

        _connectionStatusLabel = new Label
        {
            Text = "Offline",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _connectionStatusLabel.AddThemeColorOverride("font_color", Colors.White);  // White text
        statusRow.AddChild(_connectionStatusLabel);

        connectionVBox.AddChild(statusRow);

        _connectionDetailLabel = new Label
        {
            Text = "Desconectado do servidor",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _connectionDetailLabel.AddThemeColorOverride("font_color", Colors.White);  // White text
        _connectionDetailLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        connectionVBox.AddChild(_connectionDetailLabel);

        connectionPanel.AddChild(connectionVBox);
        root.AddChild(connectionPanel);

        _centerRegister.Visible = false;
        _centerRecover.Visible = false;

        AddChild(root);

        CarregarCredenciaisSalvas();
        ConectarSinais();
    }

    private Panel CriarPanelBase()
    {
        var panel = new Panel();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.6f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        return panel;
    }

    private Panel CriarLoginPanel()
    {
        var panel = CriarPanelBase();
        panel.CustomMinimumSize = new Vector2(320, 0);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var vbox = new VBoxContainer();

        vbox.AddChild(new Label { Text = "Usuário:" });
        _username = new LineEdit { PlaceholderText = "Digite seu usuário" };
        vbox.AddChild(_username);

        vbox.AddChild(new Label { Text = "Senha:" });
        _password = new LineEdit { PlaceholderText = "Digite sua senha", Secret = true };
        vbox.AddChild(_password);

        _lembrarCheck = new CheckBox { Text = "Lembrar senha", ButtonPressed = true };
        vbox.AddChild(_lembrarCheck);

        vbox.AddChild(new Control { Size = new Vector2(0, 10) });

        _loginBtn = new Button { Text = "Entrar" };
        _loginBtn.Pressed += OnLoginPressed;
        vbox.AddChild(_loginBtn);

        _status = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        vbox.AddChild(_status);

        var botoesExtras = new HBoxContainer();
        var btnCriarConta = new Button { Text = "Criar Conta", Flat = true };
        btnCriarConta.Pressed += MostrarRegister;
        botoesExtras.AddChild(btnCriarConta);

        var btnEsqueci = new Button { Text = "Esqueci a senha", Flat = true };
        btnEsqueci.Pressed += MostrarRecover;
        botoesExtras.AddChild(btnEsqueci);

        vbox.AddChild(botoesExtras);

        panel.AddChild(vbox);
        return panel;
    }

    private Panel CriarRegisterPanel()
    {
        var panel = CriarPanelBase();
        panel.CustomMinimumSize = new Vector2(360, 0);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var vbox = new VBoxContainer();

        var title = new Label
        {
            Text = "Criar Nova Conta",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(title);

        vbox.AddChild(new Control { Size = new Vector2(0, 10) });

        vbox.AddChild(new Label { Text = "Usuário:" });
        _regUsername = new LineEdit { PlaceholderText = "Mínimo 3 caracteres" };
        vbox.AddChild(_regUsername);

        vbox.AddChild(new Label { Text = "Senha:" });
        _regPassword = new LineEdit { PlaceholderText = "Mínimo 3 caracteres", Secret = true };
        vbox.AddChild(_regPassword);

        vbox.AddChild(new Label { Text = "Confirmar Senha:" });
        _regConfirm = new LineEdit { PlaceholderText = "Digite a senha novamente", Secret = true };
        vbox.AddChild(_regConfirm);

        vbox.AddChild(new Control { Size = new Vector2(0, 20) });

        var buttons = new HBoxContainer();
        buttons.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        _registerBtn = new Button { Text = "Criar Conta" };
        _registerBtn.Pressed += OnRegisterPressed;
        buttons.AddChild(_registerBtn);
        buttons.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });

        var btnVoltar = new Button { Text = "Voltar", Flat = true };
        btnVoltar.Pressed += MostrarLogin;
        buttons.AddChild(btnVoltar);

        vbox.AddChild(buttons);

        _regStatus = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        vbox.AddChild(_regStatus);

        panel.AddChild(vbox);
        return panel;
    }

    private Panel CriarRecoverPanel()
    {
        var panel = CriarPanelBase();
        panel.CustomMinimumSize = new Vector2(320, 0);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var vbox = new VBoxContainer();

        var title = new Label
        {
            Text = "Recuperar Senha",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        vbox.AddChild(title);

        vbox.AddChild(new Control { Size = new Vector2(0, 10) });

        vbox.AddChild(new Label { Text = "Usuário:" });
        _recUsername = new LineEdit { PlaceholderText = "Digite seu usuário" };
        vbox.AddChild(_recUsername);

        var btnBuscar = new Button { Text = "Buscar Pergunta" };
        btnBuscar.Pressed += OnBuscarPergunta;
        vbox.AddChild(btnBuscar);

        _recPerguntaLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        vbox.AddChild(_recPerguntaLabel);

        vbox.AddChild(new Label { Text = "Resposta:" });
        _recResposta = new LineEdit { PlaceholderText = "Digite sua resposta secreta" };
        vbox.AddChild(_recResposta);

        vbox.AddChild(new Label { Text = "Nova Senha:" });
        _recNewPass = new LineEdit { PlaceholderText = "Mínimo 3 caracteres", Secret = true };
        vbox.AddChild(_recNewPass);

        vbox.AddChild(new Control { Size = new Vector2(0, 20) });

        var buttons = new HBoxContainer();
        buttons.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        _recEnviarBtn = new Button { Text = "Redefinir Senha" };
        _recEnviarBtn.Pressed += OnRecoverPressed;
        buttons.AddChild(_recEnviarBtn);
        buttons.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });

        var btnVoltar = new Button { Text = "Voltar", Flat = true };
        btnVoltar.Pressed += MostrarLogin;
        buttons.AddChild(btnVoltar);

        vbox.AddChild(buttons);

        _recStatus = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        vbox.AddChild(_recStatus);

        panel.AddChild(vbox);
        return panel;
    }

    private void MostrarLogin()
    {
        _centerLogin.Visible = true;
        _centerRegister.Visible = false;
        _centerRecover.Visible = false;
    }

    private void MostrarRegister()
    {
        _centerLogin.Visible = false;
        _centerRegister.Visible = true;
        _centerRecover.Visible = false;
    }

    private void MostrarRecover()
    {
        _centerLogin.Visible = false;
        _centerRegister.Visible = false;
        _centerRecover.Visible = true;
        _recPerguntaLabel.Text = "";
        _recStatus.Text = "";
    }

    private void CarregarCredenciaisSalvas()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(CredentialsPath) == Error.Ok)
        {
            _username.Text = cfg.GetValue("login", "username", "").AsString();
            _password.Text = cfg.GetValue("login", "password", "").AsString();
            _lembrarCheck.ButtonPressed = cfg.GetValue("login", "lembrar", false).AsBool();
        }
    }

    private void SalvarCredenciais()
    {
        var cfg = new ConfigFile();
        if (_lembrarCheck.ButtonPressed)
        {
            cfg.SetValue("login", "username", _username.Text);
            cfg.SetValue("login", "password", _password.Text);
            cfg.SetValue("login", "lembrar", true);
        }
        else
        {
            cfg.SetValue("login", "username", "");
            cfg.SetValue("login", "password", "");
            cfg.SetValue("login", "lembrar", false);
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
            _status!.Text = "GameNetwork não encontrado!";
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
            _connectionDot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.LimeGreen,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
            });
        }
        else
        {
            _connectionStatusLabel.Text = "Offline";
            _connectionDetailLabel.Text = "Desconectado do servidor";
            _connectionDot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = Colors.Red,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
            });
        }
    }

    private void OnLoginPressed()
    {
        _loginBtn!.Disabled = true;
        _status!.Text = "Enviando login...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
        {
            net.SendLogin(_username.Text.Trim(), _password.Text);
            
            // Timeout de 10 segundos para re-habilitar o botão se não receber resposta
            var timer = GetTree().CreateTimer(10.0f);
            timer.Timeout += () =>
            {
                if (_loginBtn != null && _loginBtn.Disabled)
                {
                    _loginBtn.Disabled = false;
                    _status.Text = "Timeout: Tente novamente.";
                }
            };
        }
        else
        {
            _status.Text = "Não conectado ao servidor.";
            _loginBtn.Disabled = false;
        }
    }

    private void OnRegisterPressed()
    {
        string user = _regUsername.Text.Trim();
        string pass = _regPassword.Text;
        string confirm = _regConfirm.Text;

        if (user.Length < 3)
        {
            _regStatus!.Text = "Usuário deve ter pelo menos 3 caracteres.";
            return;
        }
        if (pass.Length < 3)
        {
            _regStatus!.Text = "Senha deve ter pelo menos 3 caracteres.";
            return;
        }
        if (pass != confirm)
        {
            _regStatus!.Text = "Senhas não conferem.";
            return;
        }

        _registerBtn!.Disabled = true;
        _regStatus!.Text = "Criando conta...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
        {
            net.SendRegister(user, pass);
            
            // Timeout de 10 segundos para re-habilitar o botão
            var timer = GetTree().CreateTimer(10.0f);
            timer.Timeout += () =>
            {
                if (_registerBtn != null && _registerBtn.Disabled)
                {
                    _registerBtn.Disabled = false;
                    _regStatus.Text = "Timeout: Tente novamente.";
                }
            };
        }
        else
        {
            _regStatus.Text = "Não conectado ao servidor.";
            _registerBtn.Disabled = false;
        }
    }

    private void OnBuscarPergunta()
    {
        _recStatus!.Text = "Buscando...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
            net.SendGetSecurityQuestion(_recUsername.Text.Trim());
        else
            _recStatus.Text = "Não conectado ao servidor.";
    }

    private void OnRecoverPressed()
    {
        string answer = _recResposta.Text.Trim();
        string newPass = _recNewPass.Text;

        if (string.IsNullOrEmpty(answer))
        {
            _recStatus!.Text = "Digite a resposta secreta.";
            return;
        }
        if (newPass.Length < 3)
        {
            _recStatus!.Text = "Nova senha deve ter pelo menos 3 caracteres.";
            return;
        }

        _recEnviarBtn!.Disabled = true;
        _recStatus!.Text = "Redefinindo senha...";

        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected)
            net.SendRecoverPassword(_recUsername.Text.Trim(), answer, newPass);
        else
        {
            _recStatus.Text = "Não conectado ao servidor.";
            _recEnviarBtn.Disabled = false;
        }
    }

    private void OnConnectedHandler()
    {
        _status!.Text = "Conectado ao servidor.";
        _regStatus!.Text = "Conectado ao servidor.";
        _recStatus!.Text = "Conectado ao servidor.";
        AtualizarStatusConexao(true);
    }

    private void OnLoginResultHandler(bool success, string message)
    {
        if (success)
        {
            _status!.Text = "Login OK!";
            SalvarCredenciais();
            OnLoginSuccess();
        }
        else
        {
            _status!.Text = $"Erro: {message}";
            _loginBtn!.Disabled = false;
        }
    }

    private void OnRegisterResultHandler(bool success, string message)
    {
        if (success)
        {
            _regStatus!.Text = "Conta criada! Faça login.";
            _regStatus.AddThemeColorOverride("font_color", Colors.Green);
            _registerBtn!.Disabled = false;
        }
        else
        {
            _regStatus!.Text = $"Erro: {message}";
            _regStatus.AddThemeColorOverride("font_color", Colors.Red);
            _registerBtn!.Disabled = false;
        }
    }

    private void OnSecurityQuestionHandler(bool found, string questionOrError)
    {
        if (found)
        {
            _recPerguntaLabel!.Text = questionOrError;
            _recPerguntaLabel.AddThemeColorOverride("font_color", Colors.White);
            _recStatus!.Text = "";
        }
        else
        {
            _recPerguntaLabel!.Text = "";
            _recStatus!.Text = $"Erro: {questionOrError}";
            _recStatus.AddThemeColorOverride("font_color", Colors.Red);
        }
    }

    private void OnRecoverResultHandler(bool success, string message)
    {
        if (success)
        {
            _recStatus!.Text = message;
            _recStatus.AddThemeColorOverride("font_color", Colors.Green);
            _recEnviarBtn!.Disabled = false;
        }
        else
        {
            _recStatus!.Text = $"Erro: {message}";
            _recStatus.AddThemeColorOverride("font_color", Colors.Red);
            _recEnviarBtn!.Disabled = false;
        }
    }

    private void OnDisconnectedHandler()
    {
        _status!.Text = "Desconectado do servidor.";
        _loginBtn!.Disabled = false;
        _regStatus!.Text = "Desconectado do servidor.";
        _registerBtn!.Disabled = false;
        AtualizarStatusConexao(false);
    }

    public override void _ExitTree()
    {
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
