using Godot;
using System;
using System.Collections.Generic;

public enum ChatChannel : byte
{
    Global = 0,
    Whisper = 1,
    Group = 2,
    Guild = 3,
    System = 4,
}

public partial class ChatUI : Control
{
    private static readonly string[] ChannelNames = { "Global", "Sussurro", "Grupo", "Guilda", "Sistema" };
    private static readonly Color[] ChannelColors =
    {
        new Color(0.9f, 0.9f, 1.0f),
        new Color(0.7f, 1.0f, 0.7f),
        new Color(1.0f, 0.85f, 0.5f),
        new Color(0.7f, 0.8f, 1.0f),
        new Color(1.0f, 1.0f, 0.53f),
    };

    private PanelContainer _mainContainer;
    private Panel _titleBar;
    private Button _lockButton;
    private bool _locked = true;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private Panel _resizeHandle;
    private bool _isResizing;
    private float _resizeDragStartY;
    private float _resizeStartOffsetTop;

    private HBoxContainer _tabBar;
    private Button[] _tabButtons;
    private ChatChannel _currentChannel;

    private RichTextLabel _messageLog;
    private bool _autoScroll = true;

    private HBoxContainer _langBar;
    private OptionButton _langSelector;

    private HBoxContainer _inputBar;
    private Label _channelLabel;
    private LineEdit _chatInput;

    private TranslatorService _translator;
    private GameNetwork _net;
    private string _playerName = "Jogador";

    private readonly List<(ChatChannel channel, string bbcode)> _allMessages = new();
    private readonly List<string> _pendingTranslations = new();

    public override void _Ready()
    {
        BuildUI();
        CallDeferred(nameof(ConnectToNetwork));
    }

    private void BuildUI()
    {
        _mainContainer = new PanelContainer();
        _mainContainer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.5f),
            BorderColor = new Color(0.3f, 0.3f, 0.4f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginBottom = 0,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
        });
        _mainContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_mainContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 1);
        _mainContainer.AddChild(vbox);

        BuildResizeHandle(vbox);
        BuildTitleBar(vbox);
        BuildTabBar(vbox);
        BuildLanguageBar(vbox);
        BuildDisplay(vbox);
        BuildInput(vbox);
        SetChannel(ChatChannel.Global);
    }

    private void BuildResizeHandle(VBoxContainer parent)
    {
        _resizeHandle = new Panel();
        _resizeHandle.CustomMinimumSize = new Vector2(0, 6);
        _resizeHandle.MouseDefaultCursorShape = CursorShape.Vsize;
        _resizeHandle.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.25f, 0.25f, 0.35f, 0.6f),
            BorderColor = new Color(0.4f, 0.4f, 0.5f),
            BorderWidthBottom = 1,
        });
        _resizeHandle.GuiInput += OnResizeHandleInput;
        parent.AddChild(_resizeHandle);
    }

    private void BuildTitleBar(VBoxContainer parent)
    {
        _titleBar = new Panel();
        _titleBar.CustomMinimumSize = new Vector2(0, 22);
        _titleBar.MouseDefaultCursorShape = CursorShape.Arrow;
        _titleBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.2f, 0.28f, 0.8f),
            BorderColor = new Color(0.4f, 0.4f, 0.5f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        });
        _titleBar.GuiInput += OnTitleBarGuiInput;
        parent.AddChild(_titleBar);

        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = SizeFlags.Fill;
        hbox.SizeFlagsVertical = SizeFlags.Fill;
        hbox.AddThemeConstantOverride("separation", 0);
        _titleBar.AddChild(hbox);

        var label = new Label();
        label.Text = "\U0001f4ac  Chat";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SizeFlagsHorizontal = SizeFlags.Expand;
        label.SizeFlagsVertical = SizeFlags.Fill;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.7f));
        hbox.AddChild(label);

        _lockButton = new Button();
        _lockButton.CustomMinimumSize = new Vector2(24, 22);
        _lockButton.Flat = true;
        _lockButton.Text = "\U0001f512";
        _lockButton.AddThemeFontSizeOverride("font_size", 12);
        _lockButton.Pressed += ToggleLock;
        hbox.AddChild(_lockButton);
    }

    private void BuildTabBar(VBoxContainer parent)
    {
        _tabBar = new HBoxContainer();
        _tabBar.AddThemeConstantOverride("separation", 2);
        _tabBar.SizeFlagsHorizontal = SizeFlags.Fill;

        var channels = (ChatChannel[])Enum.GetValues(typeof(ChatChannel));
        _tabButtons = new Button[channels.Length];

        for (int i = 0; i < channels.Length; i++)
        {
            int idx = i;
            var btn = new Button();
            btn.Text = ChannelNames[i];
            btn.Flat = true;
            btn.SizeFlagsHorizontal = SizeFlags.Expand;
            btn.CustomMinimumSize = new Vector2(0, 20);
            btn.AddThemeFontSizeOverride("font_size", 9);
            btn.AddThemeColorOverride("font_color", ChannelColors[i]);
            btn.Pressed += () => SetChannel((ChatChannel)idx);
            _tabBar.AddChild(btn);
            _tabButtons[i] = btn;
        }

        var translateBtn = new Button();
        translateBtn.Text = "\U0001f310";
        translateBtn.Flat = true;
        translateBtn.CustomMinimumSize = new Vector2(22, 20);
        translateBtn.AddThemeFontSizeOverride("font_size", 11);
        translateBtn.ToggleMode = true;
        translateBtn.Pressed += () => _langBar.Visible = translateBtn.ButtonPressed;
        _tabBar.AddChild(translateBtn);

        parent.AddChild(_tabBar);
    }

    private void BuildLanguageBar(VBoxContainer parent)
    {
        _langBar = new HBoxContainer();
        _langBar.Visible = false;
        _langBar.AddThemeConstantOverride("separation", 4);

        var langLabel = new Label();
        langLabel.Text = "Traduzir para:";
        langLabel.AddThemeFontSizeOverride("font_size", 9);
        langLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.5f));
        _langBar.AddChild(langLabel);

        _langSelector = new OptionButton();
        _langSelector.AddThemeFontSizeOverride("font_size", 9);
        _langBar.AddChild(_langSelector);

        parent.AddChild(_langBar);
    }

    private void BuildDisplay(VBoxContainer parent)
    {
        _messageLog = new RichTextLabel();
        _messageLog.SizeFlagsHorizontal = SizeFlags.Expand;
        _messageLog.SizeFlagsVertical = SizeFlags.Expand;
        _messageLog.CustomMinimumSize = new Vector2(300, 100);
        _messageLog.BbcodeEnabled = true;
        _messageLog.FitContent = false;
        _messageLog.ScrollActive = true;
        _messageLog.AddThemeFontSizeOverride("normal_font_size", 10);
        _messageLog.AddThemeFontSizeOverride("bold_font_size", 10);
        _messageLog.AddThemeColorOverride("default_color", new Color(0.9f, 0.9f, 0.95f, 1.0f));
        _messageLog.AddThemeColorOverride("background_color", new Color(0, 0.3f, 0, 0.6f));
        _messageLog.Show();
        _messageLog.Visible = true;
        parent.AddChild(_messageLog);
    }

    private void BuildInput(VBoxContainer parent)
    {
        _inputBar = new HBoxContainer();
        _inputBar.AddThemeConstantOverride("separation", 4);
        _inputBar.SizeFlagsHorizontal = SizeFlags.Fill;
        _inputBar.CustomMinimumSize = new Vector2(0, 28);

        _channelLabel = new Label();
        _channelLabel.Text = "[Global]";
        _channelLabel.CustomMinimumSize = new Vector2(60, 0);
        _channelLabel.AddThemeFontSizeOverride("font_size", 9);
        _channelLabel.AddThemeColorOverride("font_color", ChannelColors[0]);
        _channelLabel.VerticalAlignment = VerticalAlignment.Center;
        _inputBar.AddChild(_channelLabel);

        _chatInput = new LineEdit();
        _chatInput.CustomMinimumSize = new Vector2(220, 20);
        _chatInput.KeepEditingOnTextSubmit = false;
        _chatInput.PlaceholderText = "Digite sua mensagem...";
        _chatInput.AddThemeFontSizeOverride("font_size", 15);
        _chatInput.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        _chatInput.AddThemeColorOverride("placeholder_color", new Color(0.6f, 0.6f, 0.7f, 0.5f));
        _chatInput.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.2f, 0.5f),
            BorderColor = new Color(0.3f, 0.3f, 0.4f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        });
        _chatInput.TextSubmitted += OnTextSubmitted;
        _inputBar.AddChild(_chatInput);

        var sendBtn = new Button();
        sendBtn.Text = "Enviar";
        sendBtn.CustomMinimumSize = new Vector2(50, 0);
        sendBtn.AddThemeFontSizeOverride("font_size", 9);
        sendBtn.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1.0f, 0.9f));
        sendBtn.Pressed += () => OnTextSubmitted(_chatInput.Text);
        _inputBar.AddChild(sendBtn);

        parent.AddChild(_inputBar);
    }

    private void ConnectToNetwork()
    {
        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_net == null)
        {
            GD.Print("[CHAT] GameNetwork não encontrado, chat offline.");
            return;
        }

        _net.OnChatMessage += OnChatReceived;

        var personagem = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        if (personagem != null && personagem.TemPersonagem)
            _playerName = personagem.NomePersonagem;

        _translator = new TranslatorService();
        AddChild(_translator);

        SetupLanguageSelector();
    }

    private void SetupLanguageSelector()
    {
        if (_translator == null) return;
        _langSelector.Clear();
        var codes = _translator.GetLanguageCodes();
        var names = _translator.GetLanguageNames();

        for (int i = 0; i < codes.Length; i++)
            _langSelector.AddItem(names[i]);

        _langSelector.Selected = 0;
        _langSelector.ItemSelected += (index) =>
        {
            _translator.SelectedLanguage = codes[(int)index];
        };
    }

    public void AddSystemMessage(string message)
    {
        string bbcode = $"[color=#ffff88]{message}[/color]\n";
        _allMessages.Add((ChatChannel.System, bbcode));
        if (_currentChannel == ChatChannel.System || _currentChannel == ChatChannel.Global)
            AppendToLog(bbcode);
        TrimMessages();
    }

    private void OnChatReceived(byte channel, string senderName, string message, string language)
    {
        if (string.Equals(senderName, "!Sistema", StringComparison.OrdinalIgnoreCase))
        {
            AddSystemMessage(message);
            return;
        }

        var ch = (ChatChannel)channel;
        int chIdx = Mathf.Clamp((int)channel, 0, ChannelColors.Length - 1);
        string hex = ChannelColors[chIdx].ToHtml();
        string color = "#" + hex.Substring(0, 6);
        string tag = ChannelNames[chIdx];

        string displayName = ch == ChatChannel.Whisper && senderName.StartsWith("-> ")
            ? $"[b][color=#88ff88]({senderName})[/color][/b]"
            : $"[b][color={color}]{senderName}[/color][/b]";

        string formatted = $"[color={color}][{tag}][/color] {displayName}: {message}";
        AddMessage(formatted + "\n", ch);

        if (_translator != null && _translator.Enabled && ch != ChatChannel.Whisper)
        {
            TryTranslate(message, language, senderName, ch);
        }
    }

    private void TryTranslate(string message, string language, string senderName, ChatChannel ch)
    {
        if (string.IsNullOrEmpty(language) || language == _translator.SelectedLanguage) return;

        string color = ChannelColors[(int)ch].ToHtml();

        _translator.DetectLanguage(message, (detected) =>
        {
            if (string.IsNullOrEmpty(detected) || detected == _translator.SelectedLanguage)
                return;

            _translator.Translate(message, detected, (translated) =>
            {
                if (string.IsNullOrEmpty(translated)) return;
                string transMsg = $"     [color=#888888][i](\u2192 {translated})[/i][/color]\n";
                AddMessage(transMsg);
            });
        });
    }

    private void AddMessage(string bbcode, ChatChannel channel)
    {
        if (_messageLog == null) return;

        _allMessages.Add((channel, bbcode));
        if (channel == _currentChannel || _currentChannel == ChatChannel.Global)
            AppendToLog(bbcode);
        TrimMessages();

        if (_autoScroll)
            CallDeferred(nameof(ScrollToBottom));
    }

    private void AddMessage(string bbcode)
    {
        AddMessage(bbcode, _currentChannel);
    }

    private void AppendToLog(string bbcode)
    {
        if (_messageLog == null) return;
        try { _messageLog.AppendText(bbcode); }
        catch (System.Exception ex) { GD.PrintErr($"[CHAT] ERROR: {ex.Message}"); }
    }

    private void TrimMessages()
    {
        while (_allMessages.Count > 200)
        {
            _allMessages.RemoveAt(0);
        }
    }

    private void RebuildLogForCurrentChannel()
    {
        if (_messageLog == null) return;
        _messageLog.Clear();
        foreach (var (channel, bbcode) in _allMessages)
        {
            if (channel == _currentChannel || _currentChannel == ChatChannel.Global)
                AppendToLog(bbcode);
        }
        if (_autoScroll)
            CallDeferred(nameof(ScrollToBottom));
    }

    private void ScrollToBottom()
    {
        int lines = _messageLog.GetLineCount();
        if (lines > 0)
            _messageLog.ScrollToLine(lines - 1);
    }

    private void OnTextSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _chatInput.Text = "";
        _chatInput.ReleaseFocus();

        if (text.StartsWith("/"))
        {
            HandleCommand(text);
            return;
        }

        if (_net == null || !_net.IsConnected)
        {
            GD.PrintErr("[CHAT] Chat local bloqueado. Conecte ao servidor para enviar mensagens.");
            return;
        }

        string target = "";
        byte ch = (byte)_currentChannel;

        _net.SendChat(ch, target, text, "pt");
        OnChatReceived(ch, _playerName, text, "pt");
    }

    private void HandleCommand(string cmd)
    {
        var parts = cmd.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        switch (parts[0].ToLower())
        {
            case "/w":
            case "/whisper":
                if (parts.Length < 3) { AddMessage("[color=#ff8888]Use: /w <nome> <mensagem>[/color]\n"); return; }
                string target = parts[1];
                string msg = string.Join(" ", parts, 2, parts.Length - 2);
                _net?.SendChat((byte)ChatChannel.Whisper, target, msg, "pt");
                OnChatReceived((byte)ChatChannel.Whisper, $"-> {target}", msg, "pt");
                break;

            case "/g":
            case "/global":
                _currentChannel = ChatChannel.Global;
                UpdateChannelUI();
                break;

            case "/p":
            case "/party":
            case "/group":
                if (parts.Length >= 2)
                {
                    string partyMsg = string.Join(" ", parts, 1, parts.Length - 1);
                    _net?.SendChat((byte)ChatChannel.Group, "", partyMsg, "pt");
                }
                else
                {
                    _currentChannel = ChatChannel.Group;
                    UpdateChannelUI();
                }
                break;

            case "/gu":
            case "/guild":
                if (parts.Length >= 2)
                {
                    string guildMsg = string.Join(" ", parts, 1, parts.Length - 1);
                    _net?.SendChat((byte)ChatChannel.Guild, "", guildMsg, "pt");
                }
                else
                {
                    _currentChannel = ChatChannel.Guild;
                    UpdateChannelUI();
                }
                break;

            case "/invite":
            case "/aceitar":
            case "/accept":
            case "/sair":
            case "/leave":
            case "/gcreate":
            case "/ginvite":
            case "/gaceitar":
            case "/gaccept":
            case "/gsair":
            case "/gleave":
                string fullCmd = string.Join(" ", parts);
                _net?.SendChat((byte)ChatChannel.Global, "", fullCmd, "pt");
                break;

            case "/ajuda":
            case "/help":
                AddMessage("[color=#ffff88]Comandos: /w <nome> <msg>, /g (global), /p (grupo), /gu (guilda), /invite <nome>, /gcreate <nome>[/color]\n");
                break;

            default:
                AddMessage($"[color=#ff8888]Comando desconhecido: {parts[0]}[/color]\n");
                break;
        }
    }

    private void SetChannel(ChatChannel channel)
    {
        _currentChannel = channel;
        UpdateChannelUI();
        RebuildLogForCurrentChannel();
    }

    private void UpdateChannelUI()
    {
        int idx = (int)_currentChannel;
        _channelLabel.Text = $"[{ChannelNames[idx]}]";
        _channelLabel.AddThemeColorOverride("font_color", ChannelColors[idx]);
        _chatInput.PlaceholderText = $"Digite para [{ChannelNames[idx]}]...";

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            var style = new StyleBoxFlat
            {
                BgColor = i == idx ? new Color(0.3f, 0.3f, 0.4f, 0.5f) : new Color(0, 0, 0, 0),
                BorderWidthBottom = i == idx ? 1 : 0,
                BorderColor = ChannelColors[i],
            };
            _tabButtons[i].AddThemeStyleboxOverride("normal", style);
            _tabButtons[i].AddThemeStyleboxOverride("hover", style);
            _tabButtons[i].AddThemeStyleboxOverride("pressed", style);
        }
    }

    private void CenterBar()
    {
    }

    private void OnResizeHandleInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
        {
            if (btn.Pressed)
            {
                _isResizing = true;
                _resizeDragStartY = GetGlobalMousePosition().Y;
                _resizeStartOffsetTop = OffsetTop;
                AcceptEvent();
            }
            else if (_isResizing)
            {
                _isResizing = false;
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion && _isResizing)
        {
            float currentY = GetGlobalMousePosition().Y;
            float delta = currentY - _resizeDragStartY;
            float newOffset = _resizeStartOffsetTop + delta;

            float viewH = GetViewportRect().Size.Y;
            newOffset = Mathf.Clamp(newOffset, -(viewH - 80), -100);
            OffsetTop = newOffset;
            AcceptEvent();
        }
    }

    private void ToggleLock()
    {
        _locked = !_locked;
        _lockButton.Text = _locked ? "\U0001f512" : "\U0001f513";
        _titleBar.MouseDefaultCursorShape = _locked ? CursorShape.Arrow : CursorShape.Move;
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (_locked) return;

        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                _arrastando = true;
                _pontoCliqueOriginal = mouseEvent.Position;
            }
            else
            {
                _arrastando = false;
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _mainContainer.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        if (_net != null)
            _net.OnChatMessage -= OnChatReceived;
    }
}
