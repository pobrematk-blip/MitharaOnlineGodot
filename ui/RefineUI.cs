using Godot;

public partial class RefineUI : Control
{
    private static readonly int[] RefineSuccessRates = { 100, 80, 70, 60, 50, 40, 30, 20, 10, 5 };

    private Panel _dropZone;
    private TextureRect _itemIcone;
    private Label _itemNome;
    private Label _nivelAtual;
    private Label _chanceLabel;
    private Label _custoGold;
    private Label _custoPoeira;
    private Button _refinarBtn;
    private Button _fecharBtn;
    private Label _feedbackLabel;
    private Label _tituloLabel;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private int _selectedSlot = -1;
    private int _selectedItemId;
    private int _selectedRefineLevel;
    private GameNetwork _net;
    private CashManager _cash;
    private bool _processando;

    public override void _Ready()
    {
        _dropZone = GetNode<Panel>("Panel/DropZone");
        _itemIcone = GetNode<TextureRect>("Panel/DropZone/ItemIcone");
        _itemNome = GetNode<Label>("Panel/DropZone/ItemNome");
        _nivelAtual = GetNode<Label>("Panel/DropZone/NivelAtual");
        _chanceLabel = GetNode<Label>("Panel/InfoContainer/ChanceRow/ChanceLabel");
        _custoGold = GetNode<Label>("Panel/InfoContainer/GoldRow/CustoGold");
        _custoPoeira = GetNode<Label>("Panel/InfoContainer/PoeiraRow/CustoPoeira");
        _refinarBtn = GetNode<Button>("Panel/ButtonRow/RefinarBtn");
        _fecharBtn = GetNode<Button>("Panel/ButtonRow/FecharBtn");
        _feedbackLabel = GetNode<Label>("Panel/FeedbackLabel");
        _tituloLabel = GetNode<Label>("Panel/TituloLabel");

        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _cash = GetNodeOrNull<CashManager>("/root/CashManager");

        _fecharBtn.Pressed += () => QueueFree();
        _refinarBtn.Pressed += OnRefinar;
        _tituloLabel.GuiInput += OnTituloGuiInput;

        if (_net != null)
            _net.Connect(GameNetwork.SignalName.OnRefineResult, Callable.From((bool success, int newLevel, string message) => OnRefineResult(success, newLevel, message)));

        LimparSelecao();
        AtualizarUI();
    }

    public bool CanDropOnSlot(Variant data)
    {
        if (_processando) return false;
        if (data.Obj is SlotUI slot && slot.SlotInterno?.Item != null)
        {
            var tipo = slot.SlotInterno.Item.Tipo;
            return tipo != TipoEquipamento.Nenhum && tipo != TipoEquipamento.Consumivel && tipo != TipoEquipamento.Moeda && tipo != TipoEquipamento.Feitico;
        }
        return false;
    }

    public void DropOnSlot(Variant data)
    {
        if (data.Obj is SlotUI slot && slot.SlotInterno?.Item != null)
        {
            _selectedSlot = slot.SlotIndex;
            _selectedItemId = slot.SlotInterno.Item.ItemID;
            _selectedRefineLevel = slot.SlotInterno.RefinoNivel;
            _itemIcone.Texture = slot.SlotInterno.Item.Icone;
            _itemIcone.Visible = true;
            _itemNome.Text = slot.SlotInterno.Item.Nome;
            AtualizarUI();
        }
    }

    private void LimparSelecao()
    {
        _selectedSlot = -1;
        _selectedItemId = 0;
        _selectedRefineLevel = 0;
        _itemIcone.Texture = null;
        _itemIcone.Visible = false;
        _itemNome.Text = "Arraste um item para refinar";
    }

    private void AtualizarUI()
    {
        if (_selectedSlot < 0)
        {
            _nivelAtual.Text = "";
            _chanceLabel.Text = "";
            _custoGold.Text = "";
            _custoPoeira.Text = "";
            _refinarBtn.Disabled = true;
            return;
        }

        _nivelAtual.Text = $"+{_selectedRefineLevel}";

        if (_selectedRefineLevel >= 10)
        {
            _chanceLabel.Text = "Máximo (+10)";
            _custoGold.Text = "";
            _custoPoeira.Text = "";
            _refinarBtn.Disabled = true;
            return;
        }

        int chance = RefineSuccessRates[Mathf.Min(_selectedRefineLevel, 9)];
        int goldCost = (_selectedRefineLevel + 1) * 1000;
        int stardustCost = (_selectedRefineLevel + 1) * 5;

        _chanceLabel.Text = $"{chance}%";
        _custoGold.Text = $"{goldCost} Gold";
        _custoPoeira.Text = $"{stardustCost}x Poeira Estelar";
        _refinarBtn.Disabled = false;
    }

    private void OnRefinar()
    {
        if (_selectedSlot < 0 || _net == null || !_net.IsConnected || _processando) return;

        _processando = true;
        _feedbackLabel.Text = "⏳ Refinando...";
        _refinarBtn.Disabled = true;
        _net.SendRefineItem(_selectedSlot, _selectedItemId);
    }

    private void OnRefineResult(bool success, int newLevel, string message)
    {
        _processando = false;

        if (success)
        {
            _feedbackLabel.Text = $"✓ {message} (+{newLevel})";
            _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.4f));
            _selectedRefineLevel = newLevel;
        }
        else
        {
            _feedbackLabel.Text = $"✗ {message}";
            _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
            if (newLevel >= 0)
                _selectedRefineLevel = newLevel;
            else
                LimparSelecao();
        }

        AtualizarUI();
        _refinarBtn.Disabled = false;

        if (newLevel >= 10)
        {
            _feedbackLabel.Text = "✓ Item atingiu o nível máximo (+10)!";
        }
    }

    private void OnTituloGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastando = mouseEvent.Pressed;
            if (mouseEvent.Pressed) _pontoCliqueOriginal = mouseEvent.Position;
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastando)
        {
            Position += mouseMotion.Position - _pontoCliqueOriginal;
        }
    }
}
