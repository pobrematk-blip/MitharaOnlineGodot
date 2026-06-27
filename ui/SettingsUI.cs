using Godot;
using System;
using System.Collections.Generic;

public partial class SettingsUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private TabContainer _tabContainer;
    private OptionButton _resolutionOption;
    private CheckBox _fullscreenCheck;
    private HSlider _brightnessSlider;
    private HSlider _masterVolumeSlider;
    private HSlider _sfxVolumeSlider;
    private HSlider _musicVolumeSlider;
    private HSlider _cameraZoomSlider;
    private Label _cameraZoomValueLabel;
    private Player _player;
    private ColorRect _brightnessOverlay;
    private VBoxContainer _teclasList;
    private string _acaoEsperandoTecla;
    private Button _botaoEsperandoTecla;
    private CheckBox _mostrarNomeCheck;
    private CheckBox _mostrarVidaCheck;
    private CheckBox _mostrarManaCheck;
    private CheckBox _mostrarTagCheck;
    private CheckBox _mostrarEmblemaCheck;
    private CheckBox _mostrarPartyHUDCheck;

    private static readonly Vector2I[] Resolutions = {
        new Vector2I(1280, 720),
        new Vector2I(1366, 768),
        new Vector2I(1600, 900),
        new Vector2I(1920, 1080),
        new Vector2I(2560, 1440),
    };

    private Button _btnVoltarSelecao;
    private Button _applyVideoButton;
    private TextureButton _toggleButton;

    private const string SettingsPath = "user://settings.cfg";
    private const string SectionVideo = "Video";
    private const string SectionAudio = "Audio";
    private const string SectionCamera = "Camera";
    private const string SectionTeclas = "Teclas";
    private const string SectionUI = "UI";

    public bool EstaAberto => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _tabContainer = _panel.GetNode<TabContainer>("TabContainer");

        _resolutionOption = _tabContainer.GetNode<OptionButton>("Video/ResolutionOption");
        _fullscreenCheck = _tabContainer.GetNode<CheckBox>("Video/FullscreenCheck");
        _brightnessSlider = _tabContainer.GetNode<HSlider>("Video/BrightnessSlider");

        _masterVolumeSlider = _tabContainer.GetNode<HSlider>("Audio/MasterVolumeSlider");
        _sfxVolumeSlider = _tabContainer.GetNode<HSlider>("Audio/SfxVolumeSlider");
        _musicVolumeSlider = _tabContainer.GetNode<HSlider>("Audio/MusicVolumeSlider");

        _cameraZoomSlider = _tabContainer.GetNode<HSlider>("Camera/ZoomSlider");
        _cameraZoomValueLabel = _tabContainer.GetNode<Label>("Camera/ZoomValueLabel");

        _brightnessOverlay = GetNode<ColorRect>("BrightnessOverlay");
        _teclasList = _tabContainer.GetNode<VBoxContainer>("Teclas/ScrollContainer/TeclasList");

        _mostrarNomeCheck = _tabContainer.GetNode<CheckBox>("UI/MostrarNomeCheck");
        _mostrarVidaCheck = _tabContainer.GetNode<CheckBox>("UI/MostrarVidaCheck");
        _mostrarManaCheck = _tabContainer.GetNode<CheckBox>("UI/MostrarManaCheck");
        _mostrarTagCheck = _tabContainer.GetNode<CheckBox>("UI/MostrarTagCheck");
        _mostrarEmblemaCheck = _tabContainer.GetNode<CheckBox>("UI/MostrarEmblemaCheck");
        _mostrarPartyHUDCheck = _tabContainer.GetNode<CheckBox>("UI/MostrarPartyHUDCheck");
        PrepararAbaUIScroll();

        PopulateResolutions();
        SelecionarResolucaoAtual();
        LoadSettings();

        _mostrarNomeCheck.Toggled += OnMostrarNomeToggled;
        _mostrarVidaCheck.Toggled += OnMostrarVidaToggled;
        _mostrarManaCheck.Toggled += OnMostrarManaToggled;
        _mostrarTagCheck.Toggled += OnMostrarTagToggled;
        _mostrarEmblemaCheck.Toggled += OnMostrarEmblemaToggled;
        _mostrarPartyHUDCheck.Toggled += OnMostrarPartyHUDToggled;

        AplicarOverheadUIDoCheckbox();

        PopularTeclas();

        _closeButton.Pressed += OnClose;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _btnVoltarSelecao = _panel.GetNode<Button>("BtnVoltarSelecao");
        _btnVoltarSelecao.Pressed += OnVoltarSelecao;

        _applyVideoButton = _tabContainer.GetNode<Button>("Video/ApplyButton");
        _applyVideoButton.Pressed += OnApplyVideoPressed;
        _brightnessSlider.ValueChanged += OnBrightnessChanged;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _sfxVolumeSlider.ValueChanged += OnSfxVolumeChanged;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
        _cameraZoomSlider.ValueChanged += OnCameraZoomChanged;

        _panel.Visible = false;

        CallDeferred(MethodName.Centralizar);
        CallDeferred(MethodName.FindPlayer);

        GetTree().Root.SizeChanged += OnRootSizeChanged;

        CriarBotaoToggle();
    }

    private void PrepararAbaUIScroll()
    {
        var uiTab = _tabContainer.GetNodeOrNull<VBoxContainer>("UI");
        if (uiTab == null || uiTab.GetNodeOrNull<ScrollContainer>("UIScrollRuntime") != null)
            return;

        var children = new List<Node>();
        foreach (var child in uiTab.GetChildren())
            children.Add(child);

        var scroll = new ScrollContainer
        {
            Name = "UIScrollRuntime",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var inner = new VBoxContainer
        {
            Name = "UIRuntimeList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        inner.AddThemeConstantOverride("separation", 8);

        uiTab.AddChild(scroll);
        scroll.AddChild(inner);

        foreach (var child in children)
        {
            uiTab.RemoveChild(child);
            inner.AddChild(child);
            if (child is Control control)
            {
                control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                if (control is CheckBox cb)
                {
                    cb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    cb.ClipText = true;
                }
                else if (control is Label label)
                {
                    label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    label.ClipText = true;
                }
            }
        }
    }

    private void CriarBotaoToggle()
    {
        _toggleButton = new TextureButton();
        _toggleButton.Name = "SettingsToggleButton";
        _toggleButton.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Menu.png");
        _toggleButton.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Menu Selecionado.png");
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
        btn.Position = new Vector2(tela.X - 44, tela.Y - 44);
    }

    private void PopulateResolutions()
    {
        _resolutionOption.Clear();
        for (int i = 0; i < Resolutions.Length; i++)
            _resolutionOption.AddItem($"{Resolutions[i].X} x {Resolutions[i].Y}", i);
    }

    private void SelecionarResolucaoAtual()
    {
        Vector2I tela = DisplayServer.WindowGetSize();
        int closest = 0;
        int closestDist = int.MaxValue;
        for (int i = 0; i < Resolutions.Length; i++)
        {
            int dist = Mathf.Abs(Resolutions[i].X - tela.X) + Mathf.Abs(Resolutions[i].Y - tela.Y);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = i;
            }
        }
        _resolutionOption.Selected = closest;
    }

    private void FindPlayer()
    {
        _player = GetTree().CurrentScene.FindChild("Player", true, false) as Player;
        AplicarOverheadUIDoCheckbox();
    }

    private void AplicarOverheadUIDoCheckbox()
    {
        var overhead = GetTree().CurrentScene?.FindChild("OverheadUI", true, false) as OverheadUI;
        if (overhead == null) return;
        overhead.MostrarNome = _mostrarNomeCheck.ButtonPressed;
        overhead.MostrarBarraVida = _mostrarVidaCheck.ButtonPressed;
        overhead.MostrarBarraMana = _mostrarManaCheck.ButtonPressed;
        overhead.MostrarTagGuild = _mostrarTagCheck.ButtonPressed;
        overhead.MostrarEmblemaGuild = _mostrarEmblemaCheck.ButtonPressed;
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
            FindPlayer();
            Centralizar();
            SelecionarResolucaoAtual();
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

    private void OnVoltarSelecao()
    {
        GD.Print("[SETTINGS] OnVoltarSelecao chamado!");
        SaveSettings();
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null || !net.IsConnected)
        {
            GD.PrintErr("[SETTINGS] Não foi possível sair: sem conexão com o servidor.");
            return;
        }

        _btnVoltarSelecao.Disabled = true;
        _btnVoltarSelecao.Text = "Saindo do mundo...";
        net.SendLeaveWorld();
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

    private void OnApplyVideo()
    {
        bool fullscreen = _fullscreenCheck.ButtonPressed;
        DisplayServer.WindowSetMode(fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

        int idx = _resolutionOption.Selected;
        if (idx >= 0 && idx < Resolutions.Length && !fullscreen)
            DisplayServer.WindowSetSize(Resolutions[idx]);
    }

    private void OnApplyVideoPressed()
    {
        OnApplyVideo();
        SaveSettings();
        CallDeferred(MethodName.Centralizar);
    }

    private void OnBrightnessChanged(double value)
    {
        float alpha = Mathf.Clamp(1f - (float)value, 0f, 1f);
        _brightnessOverlay.Color = new Color(0, 0, 0, alpha * 0.6f);
    }

    private void OnMasterVolumeChanged(double value)
    {
        float db = Mathf.LinearToDb(Mathf.Clamp((float)value, 0.001f, 1f));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
    }

    private void OnSfxVolumeChanged(double value)
    {
        float db = Mathf.LinearToDb(Mathf.Clamp((float)value, 0.001f, 1f));
        int sfxIdx = AudioServer.GetBusIndex("SFX");
        if (sfxIdx >= 0) AudioServer.SetBusVolumeDb(sfxIdx, db);
    }

    private void OnMusicVolumeChanged(double value)
    {
        float db = Mathf.LinearToDb(Mathf.Clamp((float)value, 0.001f, 1f));
        int musicIdx = AudioServer.GetBusIndex("Music");
        if (musicIdx >= 0) AudioServer.SetBusVolumeDb(musicIdx, db);
    }

    private void OnCameraZoomChanged(double value)
    {
        float zoom = Mathf.Clamp((float)value, 0.2f, 2f);
        _cameraZoomValueLabel.Text = $"{zoom:F2}x";
        if (_player != null)
        {
            var camera = _player.GetNodeOrNull<Camera2D>("Camera");
            if (camera != null)
                camera.Zoom = Vector2.One * zoom;
        }
    }

    // ========== UI OVERHEAD ==========

    private void OnMostrarNomeToggled(bool pressed)
    {
        AplicarOverheadUI("MostrarNome", pressed);
    }

    private void OnMostrarVidaToggled(bool pressed)
    {
        AplicarOverheadUI("MostrarBarraVida", pressed);
    }

    private void OnMostrarManaToggled(bool pressed)
    {
        AplicarOverheadUI("MostrarBarraMana", pressed);
    }

    private void OnMostrarTagToggled(bool pressed)
    {
        AplicarOverheadUI("MostrarTagGuild", pressed);
    }

    private void OnMostrarEmblemaToggled(bool pressed)
    {
        AplicarOverheadUI("MostrarEmblemaGuild", pressed);
    }

    private void OnMostrarPartyHUDToggled(bool pressed)
    {
        var partyHud = GetTree().CurrentScene?.FindChild("PartyHUD", true, false) as PartyHUD;
        if (partyHud != null)
        {
            partyHud.HiddenByUser = !pressed;
            if (pressed)
                partyHud.Refresh();
            else
                partyHud.Visible = false;
        }
    }

    private static void AplicarOverheadUI(string propriedade, bool value)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        var overhead = tree.CurrentScene?.FindChild("OverheadUI", true, false) as OverheadUI;
        if (overhead == null) return;

        switch (propriedade)
        {
            case "MostrarNome": overhead.MostrarNome = value; break;
            case "MostrarBarraVida": overhead.MostrarBarraVida = value; break;
            case "MostrarBarraMana": overhead.MostrarBarraMana = value; break;
            case "MostrarTagGuild": overhead.MostrarTagGuild = value; break;
            case "MostrarEmblemaGuild": overhead.MostrarEmblemaGuild = value; break;
        }
    }

    // ========== KEY BINDING ==========

    private void PopularTeclas()
    {
        foreach (var child in _teclasList.GetChildren())
            child.QueueFree();

        string[] ignorar = { "ui_focus_next", "ui_focus_prev", "ui_left", "ui_right", "ui_up", "ui_down" };

        foreach (var nome in InputMap.GetActions())
        {
            string nomeStr = nome;
            if (System.Array.IndexOf(ignorar, nomeStr) >= 0) continue;
            if (nomeStr.StartsWith("ui_")) continue;

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.CustomMinimumSize = new Vector2(0, 32);
            hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var label = new Label();
            label.Text = NomeAmigavel(nomeStr);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.ClipText = true;
            label.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));
            label.CustomMinimumSize = new Vector2(110, 0);

            var keyBtn = new Button();
            keyBtn.CustomMinimumSize = new Vector2(96, 28);
            keyBtn.ClipText = true;
            keyBtn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
            string acao = nomeStr;
            keyBtn.Pressed += () => IniciarRebinding(acao, keyBtn);
            AtualizarTextoTecla(keyBtn, acao);

            hbox.AddChild(label);
            hbox.AddChild(keyBtn);
            _teclasList.AddChild(hbox);
        }
    }

    private string NomeAmigavel(string nome)
    {
        return nome switch
        {
            "mover_cima" => "Mover Cima",
            "mover_baixo" => "Mover Baixo",
            "mover_esquerda" => "Mover Esquerda",
            "mover_direita" => "Mover Direita",
            "atacar" => "Atacar",
            "pular" => "Pular",
            "correr" => "Correr",
            "inventario" => "Inventário",
            "equipamento" => "Equipamento",
            "banco" => "Banco",
            "talent_tree" => "Árvore de Talentos",
            "editor_classe" => "Editor de Classe",
            "settings" => "Configurações",
            "amigos" => "Amigos",
            "guild" => "Guilda",
            "party" => "Grupo",
            "quest" => "Missões",
            "mapa" => "Mapa",
            _ => nome
        };
    }

    private void AtualizarTextoTecla(Button btn, string acao)
    {
        foreach (var evento in InputMap.ActionGetEvents(acao))
        {
            if (evento is InputEventKey keyEvent)
            {
                btn.Text = keyEvent.AsText();
                return;
            }
        }
        btn.Text = "Nenhuma";
    }

    private void IniciarRebinding(string acao, Button btn)
    {
        if (_acaoEsperandoTecla != null)
        {
            _botaoEsperandoTecla.Text = _acaoEsperandoTecla;
            AtualizarTextoTecla(_botaoEsperandoTecla, _acaoEsperandoTecla);
        }

        _acaoEsperandoTecla = acao;
        _botaoEsperandoTecla = btn;
        btn.Text = "...";
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (_acaoEsperandoTecla != null && @event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.None) return;

            if (keyEvent.Keycode == Key.Escape)
            {
                AtualizarTextoTecla(_botaoEsperandoTecla, _acaoEsperandoTecla);
                _acaoEsperandoTecla = null;
                _botaoEsperandoTecla = null;
                GetViewport().SetInputAsHandled();
                return;
            }

            var eventos = InputMap.ActionGetEvents(_acaoEsperandoTecla);
            foreach (var e in eventos)
            {
                if (e is InputEventKey)
                {
                    InputMap.ActionEraseEvent(_acaoEsperandoTecla, e);
                    break;
                }
            }

            var novo = new InputEventKey();
            novo.Keycode = keyEvent.Keycode;
            InputMap.ActionAddEvent(_acaoEsperandoTecla, novo);

            AtualizarTextoTecla(_botaoEsperandoTecla, _acaoEsperandoTecla);
            _acaoEsperandoTecla = null;
            _botaoEsperandoTecla = null;

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("settings"))
        {
            AbrirFechar(null);
            GetViewport().SetInputAsHandled();
        }
    }

    // ========== SAVE/LOAD ==========

    private void SaveSettings()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SectionVideo, "fullscreen", DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen);
        cfg.SetValue(SectionVideo, "resolution_index", _resolutionOption.Selected);
        cfg.SetValue(SectionVideo, "brightness", _brightnessSlider.Value);
        cfg.SetValue(SectionAudio, "master_volume", _masterVolumeSlider.Value);
        cfg.SetValue(SectionAudio, "sfx_volume", _sfxVolumeSlider.Value);
        cfg.SetValue(SectionAudio, "music_volume", _musicVolumeSlider.Value);
        cfg.SetValue(SectionCamera, "zoom", _cameraZoomSlider.Value);

        cfg.SetValue(SectionUI, "mostrar_nome", _mostrarNomeCheck.ButtonPressed);
        cfg.SetValue(SectionUI, "mostrar_vida", _mostrarVidaCheck.ButtonPressed);
        cfg.SetValue(SectionUI, "mostrar_mana", _mostrarManaCheck.ButtonPressed);
        cfg.SetValue(SectionUI, "mostrar_tag_guild", _mostrarTagCheck.ButtonPressed);
        cfg.SetValue(SectionUI, "mostrar_emblema_guild", _mostrarEmblemaCheck.ButtonPressed);
        cfg.SetValue(SectionUI, "mostrar_party_hud", _mostrarPartyHUDCheck.ButtonPressed);

        foreach (var nome in InputMap.GetActions())
        {
            string nomeStr = nome;
            if (nomeStr.StartsWith("ui_")) continue;
            var eventos = InputMap.ActionGetEvents(nomeStr);
            foreach (var e in eventos)
            {
                if (e is InputEventKey keyEvent)
                {
                    cfg.SetValue(SectionTeclas, nomeStr, (int)keyEvent.Keycode);
                    break;
                }
            }
        }

        cfg.Save(SettingsPath);
    }

    private void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) != Error.Ok) return;

        bool fullscreen = cfg.GetValue(SectionVideo, "fullscreen", false).AsBool();
        _fullscreenCheck.ButtonPressed = fullscreen;

        int resIdx = cfg.GetValue(SectionVideo, "resolution_index", -1).AsInt32();
        if (resIdx >= 0 && resIdx < _resolutionOption.ItemCount)
        {
            _resolutionOption.Selected = resIdx;
            OnApplyVideo();
        }
        else
        {
            DisplayServer.WindowSetMode(fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        }

        _brightnessSlider.Value = cfg.GetValue(SectionVideo, "brightness", 0.5).AsDouble();

        _masterVolumeSlider.Value = cfg.GetValue(SectionAudio, "master_volume", 1.0).AsDouble();
        _sfxVolumeSlider.Value = cfg.GetValue(SectionAudio, "sfx_volume", 1.0).AsDouble();
        _musicVolumeSlider.Value = cfg.GetValue(SectionAudio, "music_volume", 1.0).AsDouble();

        _cameraZoomSlider.Value = cfg.GetValue(SectionCamera, "zoom", 0.58).AsDouble();

        _mostrarNomeCheck.ButtonPressed = cfg.GetValue(SectionUI, "mostrar_nome", true).AsBool();
        _mostrarVidaCheck.ButtonPressed = cfg.GetValue(SectionUI, "mostrar_vida", true).AsBool();
        _mostrarManaCheck.ButtonPressed = cfg.GetValue(SectionUI, "mostrar_mana", true).AsBool();
        _mostrarTagCheck.ButtonPressed = cfg.GetValue(SectionUI, "mostrar_tag_guild", true).AsBool();
        _mostrarEmblemaCheck.ButtonPressed = cfg.GetValue(SectionUI, "mostrar_emblema_guild", true).AsBool();
        _mostrarPartyHUDCheck.ButtonPressed = cfg.GetValue(SectionUI, "mostrar_party_hud", true).AsBool();
        Callable.From(() => OnMostrarPartyHUDToggled(_mostrarPartyHUDCheck.ButtonPressed)).CallDeferred();

        foreach (var nome in InputMap.GetActions())
        {
            string nomeStr = nome;
            if (nomeStr.StartsWith("ui_")) continue;
            if (cfg.HasSectionKey(SectionTeclas, nomeStr))
            {
                int keycode = cfg.GetValue(SectionTeclas, nomeStr, 0).AsInt32();
                if (keycode == 0) continue;

                var eventos = InputMap.ActionGetEvents(nomeStr);
                foreach (var e in eventos)
                {
                    if (e is InputEventKey)
                    {
                        InputMap.ActionEraseEvent(nomeStr, e);
                        break;
                    }
                }

                var novo = new InputEventKey();
                novo.Keycode = (Key)keycode;
                InputMap.ActionAddEvent(nomeStr, novo);
            }
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
        SaveSettings();
    }
}
