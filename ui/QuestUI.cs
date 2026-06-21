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
    private TextureButton _toggleButton;

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
        GetTree().Root.SizeChanged += OnRootSizeChanged;

        CriarBotaoToggle();
    }

    private void CriarBotaoToggle()
    {
        _toggleButton = new TextureButton();
        _toggleButton.Name = "QuestToggleButton";
        _toggleButton.TextureNormal = GD.Load<Texture2D>("res://ui/Incone de Menu/Quest.png");
        _toggleButton.TextureHover = GD.Load<Texture2D>("res://ui/Incone de Menu/Quest Selecionado.png");
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
        btn.Position = new Vector2(tela.X - 44, tela.Y - 176);
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
}
