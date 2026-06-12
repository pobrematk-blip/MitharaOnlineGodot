using Godot;
using System.Collections.Generic;

public partial class DialogUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private Label _npcText;
    private VBoxContainer _optionsContainer;
    private GameNetwork _gameNet;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _npcText = _panel.GetNode<Label>("NpcText");
        _optionsContainer = _panel.GetNode<VBoxContainer>("OptionsContainer");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_gameNet != null)
        {
            _gameNet.OnNpcDialog += OnNpcDialog;
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

            string capturedAction = action;
            string capturedData = actionData;
            btn.Pressed += () =>
            {
                if (_gameNet != null)
                    _gameNet.SendNpcSelectOption(capturedAction, capturedData);
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
        foreach (var child in _optionsContainer.GetChildren())
            child.QueueFree();

        switch (dialogId?.ToLower())
        {
            case "banco":
                _npcText.Text = $"[{npcNome}]\nPrecisa guardar seus tesouros em um local seguro?\nDeseja abrir o banco agora?";
                AdicionarOpcaoLocal("Sim, abrir banco", () => AbrirBanco());
                AdicionarOpcaoLocal("Não, depois", Fechar);
                break;

            case "guilda":
                _npcText.Text = $"[{npcNome}]\nBem-vindo, aventureiro! Já ouviu falar de Solareth?\n\nDizem que é uma terra próspera onde aventureiros audaciosos fundaram sua própria guilda.\nVocê tem coragem de começar essa jornada?\n\n(Para fundar uma guilda é preciso ter 10.000 moedas de ouro ou um Pergaminho de Criação de Clã.)";
                AdicionarOpcaoLocal("Quero fundar uma guilda em Solareth!", () => AbrirCriacaoGuilda());
                AdicionarOpcaoLocal("Ainda não estou pronto", Fechar);
                break;

            default:
                _npcText.Text = $"[{npcNome}]\nOlá, aventureiro! Em breve terei mais opções para você.";
                break;
        }

        AdicionarOpcaoLocal("Sair", Fechar);

        _panel.Visible = true;
        CallDeferred(MethodName.Centralizar);
    }

    private void AdicionarOpcaoLocal(string texto, System.Action acao)
    {
        var btn = new Button();
        btn.Text = texto;
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        btn.AddThemeStyleboxOverride("normal", CriarBotaoEstilo(new Color(0.15f, 0.15f, 0.22f, 0.95f)));
        btn.AddThemeStyleboxOverride("hover", CriarBotaoEstilo(new Color(0.25f, 0.25f, 0.35f, 0.95f)));
        btn.Pressed += acao;
        _optionsContainer.AddChild(btn);
    }

    private void AbrirBanco()
    {
        Fechar();
        var bancoScene = ResourceLoader.Load<PackedScene>("res://Banco/BancoUI.tscn");
        if (bancoScene == null) return;
        var banco = bancoScene.Instantiate<BancoUI>();
        GetTree().CurrentScene.AddChild(banco);
    }

    private void AbrirGuilda()
    {
        Fechar();
        var guildScene = ResourceLoader.Load<PackedScene>("res://ui/GuildUI.tscn");
        if (guildScene == null) return;
        var guild = guildScene.Instantiate<GuildUI>();
        GetTree().CurrentScene.AddChild(guild);
    }

    private void AbrirCriacaoGuilda()
    {
        Fechar();
        var scene = ResourceLoader.Load<PackedScene>("res://ui/GuildCreateUI.tscn");
        if (scene == null) return;
        var ui = scene.Instantiate<GuildCreateUI>();
        GetTree().CurrentScene.AddChild(ui);
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
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
    }

    public override void _ExitTree()
    {
        if (_gameNet != null)
            _gameNet.OnNpcDialog -= OnNpcDialog;
    }
}
