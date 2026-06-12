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
        _npcText.Text = $"[{npcNome}]\n(DialogId: {dialogId})\n\n— Modo offline —";

        foreach (var child in _optionsContainer.GetChildren())
            child.QueueFree();

        var btn = new Button();
        btn.Text = "Fechar";
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.Pressed += Fechar;
        _optionsContainer.AddChild(btn);

        _panel.Visible = true;
        CallDeferred(MethodName.Centralizar);
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
