using Godot;
using System.Collections.Generic;

public partial class BancoUI : Control
{
    [Export] public PackedScene SlotUIPrefab;

    private Panel _panel;
    private Control _titleBar;
    private Button _closeButton;
    private GridContainer _gridContainer;
    private HBoxContainer _containerBolsas;
    private Label _infoCapacidade;

    private BancoComponent _bancoAlvo;
    private readonly List<SlotUI> _slotsVisuais = new List<SlotUI>();
    private readonly List<SlotUI> _slotsBolsasVisuais = new List<SlotUI>();

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    public bool PainelVisivel => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Control>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _gridContainer = GetNode<GridContainer>("%GridContainer");
        _containerBolsas = GetNode<HBoxContainer>("%ContainerBolsas");
        _infoCapacidade = GetNode<Label>("%InfoCapacidade");

        Visible = true;
        _panel.Visible = false;

        _closeButton.Pressed += FecharPainel;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        CallDeferred(MethodName.CentralizarPainelNaTela);
        CallDeferred(MethodName.ConectarComponenteBanco);
    }

    private void FecharPainel()
    {
        if (_panel == null) return;
        _panel.Visible = false;
        _arrastando = false;
        GD.Print("[BANCO UI] ❌ Banco fechado!");
    }

    private void CentralizarPainelNaTela()
    {
        if (_panel == null) return;
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed)
                _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            _panel.Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }

    private void ConectarComponenteBanco()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        if (player == null)
        {
            GD.PrintErr("[BANCO UI] ❌ Player não encontrado!");
            return;
        }

        _bancoAlvo = player.FindChild("BancoComponent", true, false) as BancoComponent;
        if (_bancoAlvo == null)
        {
            GD.PrintErr("[BANCO UI] ❌ Player não tem BancoComponent!");
            return;
        }

        MapearSlotsBolsasDoEditor();
        _bancoAlvo.BancoAtualizado += DesenharInterface;
        InicializarGrade();
        GD.Print("[BANCO UI] ✅ Conectado ao BancoComponent!");
    }

    private void MapearSlotsBolsasDoEditor()
    {
        if (_containerBolsas == null || _bancoAlvo == null) return;

        _slotsBolsasVisuais.Clear();

        for (int i = 0; i < 6; i++)
        {
            string nome = $"SlotBolsaBanco_{i}";
            if (!_containerBolsas.HasNode(nome))
            {
                GD.PrintErr($"[BANCO UI] ❌ Slot '{nome}' não encontrado!");
                continue;
            }

            var slot = _containerBolsas.GetNode<SlotUI>(nome);
            _slotsBolsasVisuais.Add(slot);

            if (i < _bancoAlvo.SlotsDasBolsasEquipadas.Count)
                slot.AtualizarSlot(_bancoAlvo.SlotsDasBolsasEquipadas[i]);
        }
    }

    private void InicializarGrade()
    {
        if (_bancoAlvo == null || SlotUIPrefab == null || _gridContainer == null) return;

        foreach (Node child in _gridContainer.GetChildren())
            child.QueueFree();
        _slotsVisuais.Clear();

        for (int i = 0; i < _bancoAlvo.TamanhoDoBanco; i++)
        {
            SlotUI slot = SlotUIPrefab.Instantiate<SlotUI>();
            _gridContainer.AddChild(slot);
            _slotsVisuais.Add(slot);
        }

        ForcarAtualizacaoDosDados();
    }

    private void ForcarAtualizacaoDosDados()
    {
        if (_bancoAlvo == null) return;

        for (int i = 0; i < _bancoAlvo.Slots.Count && i < _slotsVisuais.Count; i++)
            _slotsVisuais[i].AtualizarSlot(_bancoAlvo.Slots[i]);

        for (int i = 0; i < _slotsBolsasVisuais.Count; i++)
        {
            if (i < _bancoAlvo.SlotsDasBolsasEquipadas.Count)
                _slotsBolsasVisuais[i].AtualizarSlot(_bancoAlvo.SlotsDasBolsasEquipadas[i]);
        }

        if (_infoCapacidade != null)
            _infoCapacidade.Text = $"Capacidade: {_bancoAlvo.TamanhoDoBanco} slots";
    }

    private void DesenharInterface()
    {
        if (_bancoAlvo == null) return;

        if (_slotsVisuais.Count != _bancoAlvo.TamanhoDoBanco)
            CallDeferred(MethodName.InicializarGrade);
        else
            ForcarAtualizacaoDosDados();
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;

        if (@event.IsActionPressed("banco"))
        {
            _panel.Visible = !_panel.Visible;
            _arrastando = false;
            GetViewport().SetInputAsHandled();

            if (_panel.Visible)
            {
                DesenharInterface();
                GD.Print("[BANCO UI] 🏦 Banco aberto!");
            }
            else
            {
                GD.Print("[BANCO UI] 🏦 Banco fechado!");
            }
        }

        if (_panel.Visible && @event is InputEventMouseButton mouseEvent
            && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left
            && _closeButton.GetGlobalRect().HasPoint(mouseEvent.GlobalPosition))
        {
            FecharPainel();
            GetViewport().SetInputAsHandled();
        }
    }
}
