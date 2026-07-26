using Godot;

public partial class DialogUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private Label _npcText;
    private ScrollContainer _npcTextScroll;
    private VBoxContainer _optionsContainer;
    private GameNetwork _gameNet;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _npcTextScroll = _panel.GetNode<ScrollContainer>("NpcTextScroll");
        _npcText = _panel.GetNode<Label>("NpcTextScroll/NpcText");
        _optionsContainer = _panel.GetNode<VBoxContainer>("OptionsContainer");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnNpcDialog += OnNpcDialog;
            _gameNet.OnOpenGuildForm += AbrirCriacaoGuilda;
            _gameNet.OnOpenRefine += AbrirRefine;
            _gameNet.OnOpenLojinha += AbrirLojinha;
            _gameNet.OnOpenMarketplace += AbrirMercado;
        }

        _closeButton.Pressed += Fechar;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _panel.Visible = false;
    }

    private void OnNpcDialog(string text, Godot.Collections.Array options)
    {
        if (string.IsNullOrEmpty(text))
        {
            Fechar();
            return;
        }

        _npcText.Text = text;
        _npcTextScroll.ScrollVertical = 0;

        foreach (var child in _optionsContainer.GetChildren())
            child.QueueFree();

        foreach (Godot.Collections.Dictionary opt in options)
        {
            string optText = opt["text"].AsString();
            string action = opt["action"].AsString();
            string actionData = opt["data"].AsString();

            var btn = new Button();
            btn.Text = optText;
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            btn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
            btn.AddThemeStyleboxOverride("normal", CriarBotaoEstilo(new Color(0.15f, 0.15f, 0.22f, 0.95f)));
            btn.AddThemeStyleboxOverride("hover", CriarBotaoEstilo(new Color(0.25f, 0.25f, 0.35f, 0.95f)));

            btn.Pressed += () =>
            {
                if (action == "guild_manage")
                {
                    Fechar();
                    AbrirGuilda();
                    return;
                }

                _gameNet?.SendNpcSelectOption(action, actionData);
            };

            _optionsContainer.AddChild(btn);
        }

        _panel.Visible = true;
        CallDeferred(MethodName.Centralizar);
    }

    private static StyleBoxFlat CriarBotaoEstilo(Color bg)
    {
        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.CornerRadiusTopLeft = 4;
        style.CornerRadiusTopRight = 4;
        style.CornerRadiusBottomLeft = 4;
        style.CornerRadiusBottomRight = 4;
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        return style;
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    public void MostrarDialogoLocal(string npcNome, string dialogId)
    {
        GD.PrintErr($"[DIALOG] Dialogo local bloqueado para NPC '{npcNome}' ({dialogId}). Use SendNpcInteract/network_id para receber S2C_NpcDialog do servidor.");
    }

    private void AbrirGuilda()
    {
        Fechar();
        var guild = GetTree().Root.FindChild("GuildUI", true, false) as GuildUI;
        if (guild == null)
        {
            var guildScene = ResourceLoader.Load<PackedScene>("res://ui/GuildUI.tscn");
            if (guildScene == null) return;
            guild = guildScene.Instantiate<GuildUI>();
            GetTree().CurrentScene.AddChild(guild);
        }
        guild.AbrirFechar(true);
    }

    private void AbrirCriacaoGuilda()
    {
        GD.Print("[GUILD] AbrirCriacaoGuilda chamado!");
        Fechar();
        var scene = ResourceLoader.Load<PackedScene>("res://ui/GuildCreateUI.tscn");
        if (scene == null) { GD.Print("[GUILD] ERRO: cena GuildCreateUI.tscn n?o encontrada!"); return; }
        GD.Print("[GUILD] Cena carregada com sucesso!");
        var ui = scene.Instantiate<GuildCreateUI>();
        if (ui == null) { GD.Print("[GUILD] ERRO: Instantiate retornou null!"); return; }
        GD.Print("[GUILD] GuildCreateUI instanciado com sucesso!");
        var hud = GetTree().Root.FindChild("HUD", true, false);
        if (hud != null)
        {
            hud.AddChild(ui);
            GD.Print("[GUILD] GuildCreateUI adicionado ao HUD!");
        }
        else
        {
            GetTree().CurrentScene.AddChild(ui);
            GD.Print("[GUILD] GuildCreateUI adicionado ao CurrentScene (fallback)!");
        }
    }

    private void AbrirLojinha(ulong lojinhaId, bool isOwner, string ownerName, string shopName, bool isOpen, int maxSlots, Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        Fechar();
        var hud = GetTree().Root.FindChild("HUD", true, false);
        if (hud == null) return;

        var existing = hud.FindChild("LojinhaUI", true, false);
        if (existing != null)
            existing.QueueFree();

        var scene = ResourceLoader.Load<PackedScene>("res://ui/LojinhaUI.tscn");
        if (scene == null) return;

        var ui = scene.Instantiate<LojinhaUI>();
        ui.Setup(lojinhaId, isOwner, ownerName, shopName, isOpen, maxSlots, items);
        hud.AddChild(ui);
    }

    private void AbrirMercado(string title, string rules)
    {
        Fechar();
        var hud = GetTree().Root.FindChild("HUD", true, false);
        Node parent = hud ?? GetTree().CurrentScene;
        if (parent == null) return;

        var existing = parent.FindChild("MarketplaceUI", true, false) as MarketplaceUI;
        if (existing != null)
        {
            existing.Configure(title, rules);
            existing.Visible = true;
            existing.MoveToFront();
            existing.RefreshMarketplaceOnOpen();
            return;
        }

        var ui = new MarketplaceUI { Name = "MarketplaceUI" };
        parent.AddChild(ui);
        ui.Configure(title, rules);
    }

    private void AbrirRefine()
    {
        var hud = GetTree().Root.FindChild("HUD", true, false);
        if (hud == null)
        {
            GD.PrintErr("[REFINE] HUD CanvasLayer não encontrado!");
            return;
        }

        var existing = hud.FindChild("RefineUI", true, false);
        if (existing != null)
        {
            existing.QueueFree();
        }

        var scene = ResourceLoader.Load<PackedScene>("res://ui/RefineUI.tscn");
        if (scene == null)
        {
            GD.PrintErr("[REFINE] Cena RefineUI.tscn não encontrada!");
            return;
        }

        var ui = scene.Instantiate<Control>();
        hud.AddChild(ui);
    }

    private void Fechar()
    {
        _panel.Visible = false;
        foreach (var child in _optionsContainer.GetChildren())
            child.QueueFree();
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    public override void _ExitTree()
    {
        if (_gameNet != null)
        {
            _gameNet.OnNpcDialog -= OnNpcDialog;
            _gameNet.OnOpenGuildForm -= AbrirCriacaoGuilda;
            _gameNet.OnOpenRefine -= AbrirRefine;
            _gameNet.OnOpenLojinha -= AbrirLojinha;
            _gameNet.OnOpenMarketplace -= AbrirMercado;
        }
    }
}
