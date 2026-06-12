using Godot;
using System.Collections.Generic;

public partial class QuestUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private VBoxContainer _questsList;
    private Label _emptyLabel;

    private const string QuestPath = "user://quests.cfg";

    public bool EstaAberto => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _questsList = _panel.GetNode<VBoxContainer>("VBox/ScrollContainer/QuestsList");
        _emptyLabel = _panel.GetNode<Label>("VBox/EmptyLabel");

        _closeButton.Pressed += OnClose;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _panel.Visible = false;

        CallDeferred(MethodName.Centralizar);
        GetTree().Root.SizeChanged += () => CallDeferred(MethodName.Centralizar);

        CriarBotaoToggle();
    }

    private void CriarBotaoToggle()
    {
        var btn = new TextureButton();
        btn.Name = "QuestToggleButton";
        btn.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Quest.png");
        btn.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Quest Selecionado.png");
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
        btn.Position = new Vector2(tela.X - 44, tela.Y - 176);
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

        if (@event.IsActionPressed("quest"))
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
}
