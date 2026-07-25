using Godot;

public partial class RefineUI : Control
{
    private const int PoeiraEstelarId = 107;
    private const int CristalEstelarId = 119;
    private const int OrbeSegurancaId = 120;
    private static readonly int[] RefineSuccessRates = { 100, 90, 80, 70, 60, 35, 25, 15, 8, 3 };

    private TextureRect _itemIcone;
    private Label _itemNome;
    private Label _nivelAtual;
    private Panel _materialSlotPanel;
    private TextureRect _materialIcone;
    private Label _materialNome;
    private Label _materialQtd;
    private Panel _orbeSlotPanel;
    private TextureRect _orbeIcone;
    private Label _orbeNome;
    private Label _orbeQtd;
    private Label _chanceLabel;
    private Label _custoGold;
    private Label _custoMaterial;
    private Button _refinarBtn;
    private Button _fecharBtn;
    private Label _feedbackLabel;
    private Label _tituloLabel;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private int _selectedSlot = -1;
    private int _selectedItemId;
    private int _selectedRefineLevel;
    private int _selectedRequiredLevel = 1;
    private int _materialSlot = -1;
    private int _materialItemId;
    private int _materialQuantity;
    private int _orbeSlot = -1;
    private int _orbeQuantity;
    private GameNetwork _net;
    private CashManager _cash;
    private bool _processando;

    public override void _Ready()
    {
        _itemIcone = GetNode<TextureRect>("Panel/EquipSlot/ItemIcone");
        PrepararIcone(_itemIcone);
        _itemNome = GetNode<Label>("Panel/EquipSlot/ItemNome");
        _nivelAtual = GetNode<Label>("Panel/EquipSlot/NivelAtual");

        _materialSlotPanel = GetNode<Panel>("Panel/PoeiraSlot");
        _materialIcone = GetNode<TextureRect>("Panel/PoeiraSlot/PoeiraIcone");
        PrepararIcone(_materialIcone);
        _materialNome = GetNode<Label>("Panel/PoeiraSlot/PoeiraNome");
        _materialQtd = GetNode<Label>("Panel/PoeiraSlot/PoeiraQtd");

        _orbeSlotPanel = GetNode<Panel>("Panel/OrbeSlot");
        _orbeIcone = GetNode<TextureRect>("Panel/OrbeSlot/OrbeIcone");
        PrepararIcone(_orbeIcone);
        _orbeNome = GetNode<Label>("Panel/OrbeSlot/OrbeNome");
        _orbeQtd = GetNode<Label>("Panel/OrbeSlot/OrbeQtd");

        _chanceLabel = GetNode<Label>("Panel/InfoContainer/ChanceRow/ChanceLabel");
        _custoGold = GetNode<Label>("Panel/InfoContainer/GoldRow/CustoGold");
        _custoMaterial = GetNode<Label>("Panel/InfoContainer/PoeiraRow/CustoPoeira");
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
        if (data.Obj is not SlotUI slotUI || slotUI.SlotInterno?.Item == null)
            return false;

        if (slot == _materialSlotPanel)
            return slotUI.SlotInterno.Item.ItemID == GetRequiredMaterialId();

        if (slot == _orbeSlotPanel)
            return slotUI.SlotInterno.Item.ItemID == OrbeSegurancaId;

        var tipo = slotUI.SlotInterno.Item.Tipo;
        return tipo != TipoEquipamento.Nenhum
            && tipo != TipoEquipamento.Consumivel
            && tipo != TipoEquipamento.Moeda
            && tipo != TipoEquipamento.Feitico;
    }

    public void DropOnSlot(Panel slot, Variant data)
    {
        if (data.Obj is not SlotUI slotUI || slotUI.SlotInterno?.Item == null)
            return;

        if (slot == _materialSlotPanel)
        {
            _materialSlot = slotUI.SlotIndex;
            _materialItemId = slotUI.SlotInterno.Item.ItemID;
            _materialQuantity = slotUI.SlotInterno.Quantidade;
            _materialIcone.Texture = slotUI.SlotInterno.Item.Icone;
            _materialIcone.Visible = true;
            _materialNome.Text = slotUI.SlotInterno.Item.Nome;
        }
        else if (slot == _orbeSlotPanel)
        {
            _orbeSlot = slotUI.SlotIndex;
            _orbeQuantity = slotUI.SlotInterno.Quantidade;
            _orbeIcone.Texture = slotUI.SlotInterno.Item.Icone;
            _orbeIcone.Visible = true;
            _orbeNome.Text = slotUI.SlotInterno.Item.Nome;
        }
        else
        {
            _selectedSlot = slotUI.SlotIndex;
            _selectedItemId = slotUI.SlotInterno.Item.ItemID;
            _selectedRefineLevel = slotUI.SlotInterno.RefinoNivel;
            _selectedRequiredLevel = Mathf.Max(1, slotUI.SlotInterno.Item.NivelRequerido);
            _itemIcone.Texture = slotUI.SlotInterno.Item.Icone;
            _itemIcone.Visible = true;
            _itemNome.Text = slotUI.SlotInterno.Item.Nome;
        }

        AtualizarUI();
    }

    private void LimparSelecao()
    {
        _selectedSlot = -1;
        _selectedItemId = 0;
        _selectedRefineLevel = 0;
        _selectedRequiredLevel = 1;
        _itemIcone.Texture = null;
        _itemIcone.Visible = false;
        _itemNome.Text = "Arraste um equipamento para refinar";

        LimparMaterial();
        LimparOrbe();
        _feedbackLabel.Text = "";
    }

    private void LimparMaterial()
    {
        _materialSlot = -1;
        _materialItemId = 0;
        _materialQuantity = 0;
        _materialIcone.Texture = null;
        _materialIcone.Visible = false;
        _materialNome.Text = _selectedSlot >= 0 ? $"Arraste {GetMaterialName(GetRequiredMaterialId())}" : "Arraste o material de refino";
        _materialQtd.Text = "";
    }

    private void LimparOrbe()
    {
        _orbeSlot = -1;
        _orbeQuantity = 0;
        _orbeIcone.Texture = null;
        _orbeIcone.Visible = false;
        _orbeNome.Text = "Orbe de Seguranca (opcional)";
        _orbeQtd.Text = "";
    }

    private void AtualizarUI()
    {
        if (_selectedSlot < 0)
        {
            _nivelAtual.Text = "";
            _chanceLabel.Text = "";
            _custoGold.Text = "";
            _custoMaterial.Text = "";
            _refinarBtn.Disabled = true;
            return;
        }

        _nivelAtual.Text = $"+{_selectedRefineLevel}";

        if (_selectedRefineLevel >= 10)
        {
            _chanceLabel.Text = "Maximo (+10)";
            _custoGold.Text = "";
            _custoMaterial.Text = "";
            _refinarBtn.Disabled = true;
            return;
        }

        int targetLevel = _selectedRefineLevel + 1;
        int requiredMaterialId = GetRequiredMaterialId();
        string requiredMaterialName = GetMaterialName(requiredMaterialId);

        if (_materialSlot >= 0 && _materialItemId != requiredMaterialId)
            LimparMaterial();

        _chanceLabel.Text = $"{RefineSuccessRates[Mathf.Min(_selectedRefineLevel, 9)]}%";
        int itemTier = GetSelectedItemTier();
        _custoGold.Text = $"{GetPreviewGoldCost(targetLevel, itemTier)} Gold";
        _custoMaterial.Text = $"{GetPreviewMaterialCost(targetLevel, itemTier)}x {requiredMaterialName}";

        if (_materialSlot >= 0)
            _materialQtd.Text = $"{_materialQuantity}x";
        else
            _materialNome.Text = $"Arraste {requiredMaterialName}";

        if (_orbeSlot >= 0)
            _orbeQtd.Text = $"{_orbeQuantity}x";

        _refinarBtn.Disabled = _materialSlot < 0;
    }

    private void OnRefinar()
    {
        if (_selectedSlot < 0 || _materialSlot < 0 || _net == null || !_net.IsConnected || _processando) return;

        _processando = true;
        _feedbackLabel.Text = "Refinando...";
        _feedbackLabel.RemoveThemeColorOverride("font_color");
        _refinarBtn.Disabled = true;
        _net.SendRefineItem(_selectedSlot, _selectedItemId, _orbeSlot);
    }

    private void OnRefineResult(bool success, int newLevel, string message)
    {
        _processando = false;
        LimparMaterial();
        LimparOrbe();

        if (success)
        {
            _feedbackLabel.Text = $"{message} (+{newLevel})";
            _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.4f));
            _selectedRefineLevel = newLevel;
        }
        else
        {
            _feedbackLabel.Text = message;
            _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
            if (newLevel >= 0)
                _selectedRefineLevel = newLevel;
            else
                LimparSelecao();
        }

        AtualizarUI();

        if (newLevel >= 10)
            _feedbackLabel.Text = "Item atingiu o nivel maximo (+10)!";
    }

    private int GetRequiredMaterialId()
        => _selectedRefineLevel + 1 <= 5 ? PoeiraEstelarId : CristalEstelarId;

    private static string GetMaterialName(int itemId)
        => itemId == CristalEstelarId ? "Cristal Estelar" : "Poeira Estelar";

    private int GetSelectedItemTier()
        => 1 + Mathf.Max(0, (_selectedRequiredLevel - 1) / 10);

    private static int GetPreviewGoldCost(int targetLevel, int itemTier)
        => (targetLevel <= 5 ? targetLevel * 120 : targetLevel * 350) * itemTier;

    private static int GetPreviewMaterialCost(int targetLevel, int itemTier)
        => (targetLevel <= 5 ? targetLevel : (targetLevel - 5) * 2) + Mathf.Max(0, (itemTier - 1) / 2);

    private static void PrepararIcone(TextureRect icon)
    {
        icon.CustomMinimumSize = new Vector2(40, 40);
        icon.Size = new Vector2(40, 40);
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
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
