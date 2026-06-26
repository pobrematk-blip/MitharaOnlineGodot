using Godot;

public partial class RefineUI : Control
{
    private static readonly int[] RefineSuccessRates = { 100, 80, 70, 60, 50, 40, 30, 20, 10, 5 };

    private TextureRect _itemIcone;
    private Label _itemNome;
    private Label _nivelAtual;
    private Panel _poeiraSlot;
    private TextureRect _poeiraIcone;
    private Label _poeiraNome;
    private Label _poeiraQtd;
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
    private int _stardustSlot = -1;
    private int _stardustQuantity;
    private GameNetwork _net;
    private CashManager _cash;
    private bool _processando;

    public override void _Ready()
    {
        _itemIcone = GetNode<TextureRect>("Panel/EquipSlot/ItemIcone");
        _itemIcone.CustomMinimumSize = new Vector2(40, 40);
        _itemIcone.Size = new Vector2(40, 40);
        _itemIcone.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _itemIcone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _itemNome = GetNode<Label>("Panel/EquipSlot/ItemNome");
        _nivelAtual = GetNode<Label>("Panel/EquipSlot/NivelAtual");

        _poeiraSlot = GetNode<Panel>("Panel/PoeiraSlot");
        _poeiraIcone = GetNode<TextureRect>("Panel/PoeiraSlot/PoeiraIcone");
        _poeiraIcone.CustomMinimumSize = new Vector2(40, 40);
        _poeiraIcone.Size = new Vector2(40, 40);
        _poeiraIcone.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _poeiraIcone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _poeiraNome = GetNode<Label>("Panel/PoeiraSlot/PoeiraNome");
        _poeiraQtd = GetNode<Label>("Panel/PoeiraSlot/PoeiraQtd");

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

    public bool CanDropOnSlot(Panel slot, Variant data)
    {
        if (_processando) return false;
        if (data.Obj is SlotUI slotUI && slotUI.SlotInterno?.Item != null)
        {
            if (slot == _poeiraSlot)
                return slotUI.SlotInterno.Item.ItemID == 107;

            var tipo = slotUI.SlotInterno.Item.Tipo;
            return tipo != TipoEquipamento.Nenhum && tipo != TipoEquipamento.Consumivel && tipo != TipoEquipamento.Moeda && tipo != TipoEquipamento.Feitico;
        }
        return false;
    }

    public void DropOnSlot(Panel slot, Variant data)
    {
        if (data.Obj is SlotUI slotUI && slotUI.SlotInterno?.Item != null)
        {
            if (slot == _poeiraSlot)
            {
                _stardustSlot = slotUI.SlotIndex;
                _stardustQuantity = slotUI.SlotInterno.Quantidade;
                _poeiraIcone.Texture = slotUI.SlotInterno.Item.Icone;
                _poeiraIcone.Visible = true;
                _poeiraNome.Text = slotUI.SlotInterno.Item.Nome;
            }
            else
            {
                _selectedSlot = slotUI.SlotIndex;
                _selectedItemId = slotUI.SlotInterno.Item.ItemID;
                _selectedRefineLevel = slotUI.SlotInterno.RefinoNivel;
                _itemIcone.Texture = slotUI.SlotInterno.Item.Icone;
                _itemIcone.Visible = true;
                _itemNome.Text = slotUI.SlotInterno.Item.Nome;
            }
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
        _itemNome.Text = "Arraste um equipamento para refinar";

        _stardustSlot = -1;
        _stardustQuantity = 0;
        _poeiraIcone.Texture = null;
        _poeiraIcone.Visible = false;
        _poeiraNome.Text = "Arraste Poeira Estelar";
        _poeiraQtd.Text = "";
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

        if (_stardustSlot >= 0)
            _poeiraQtd.Text = $"{_stardustQuantity}x";

        _refinarBtn.Disabled = _stardustSlot < 0;
    }

    private void OnRefinar()
    {
        if (_selectedSlot < 0 || _stardustSlot < 0 || _net == null || !_net.IsConnected || _processando) return;

        _processando = true;
        _feedbackLabel.Text = "⏳ Refinando...";
        _refinarBtn.Disabled = true;
        _net.SendRefineItem(_selectedSlot, _selectedItemId);
    }

    private void OnRefineResult(bool success, int newLevel, string message)
    {
        _processando = false;

        _stardustSlot = -1;
        _stardustQuantity = 0;
        _poeiraIcone.Texture = null;
        _poeiraIcone.Visible = false;
        _poeiraNome.Text = "Arraste Poeira Estelar";
        _poeiraQtd.Text = "";

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
