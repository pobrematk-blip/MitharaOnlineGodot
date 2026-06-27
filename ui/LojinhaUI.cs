using Godot;

public partial class LojinhaUI : Control
{
    private ulong _lojinhaId;
    private bool _isOwner;
    private bool _isOpen;
    private int _maxSlots;
    private string _ownerName = "";
    private string _shopName = "";
    private GameNetwork _net;

    private Label _tituloLabel;
    private VBoxContainer _itemsContainer;
    private Label _goldEarnedLabel;
    private VBoxContainer _ownerPanel;
    private VBoxContainer _buyerPanel;
    private Button _fecharBtn;
    private Button _coletarBtn;
    private Button _fecharLojinhaBtn;
    private Panel _dropZone;
    private Label _feedbackLabel;
    private LineEdit _nomeLojaEdit;
    private Button _comprarModoBtn;
    private Button _venderModoBtn;
    private Button _abrirLojaBtn;
    private Button _salvarRascunhoBtn;
    private Label _statusLabel;

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private bool _ready;
    private Godot.Collections.Array<Godot.Collections.Dictionary> _pendingItems = new();

    public override void _Ready()
    {
        _tituloLabel = GetNode<Label>("Panel/TituloLabel");
        _itemsContainer = GetNode<VBoxContainer>("Panel/ScrollContainer/ItemsContainer");
        _goldEarnedLabel = GetNode<Label>("Panel/GoldEarnedLabel");
        _ownerPanel = GetNode<VBoxContainer>("Panel/OwnerPanel");
        _buyerPanel = GetNode<VBoxContainer>("Panel/BuyerPanel");
        _fecharBtn = GetNode<Button>("Panel/FecharBtn");
        _coletarBtn = GetNode<Button>("Panel/OwnerPanel/ButtonRow/ColetarBtn");
        _fecharLojinhaBtn = GetNode<Button>("Panel/OwnerPanel/ButtonRow/FecharLojinhaBtn");
        _dropZone = GetNode<Panel>("Panel/OwnerPanel/DropZone");
        _feedbackLabel = GetNode<Label>("Panel/FeedbackLabel");

        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");

        CriarControlesDono();

        _fecharBtn.Pressed += () => QueueFree();
        _coletarBtn.Pressed += OnColetar;
        _fecharLojinhaBtn.Pressed += OnFecharLojinha;

        if (_dropZone is LojinhaDropSlot dropSlot)
            dropSlot.Connect(LojinhaDropSlot.SignalName.OnLojinhaDrop, Callable.From<Variant>(OnLojinhaDrop));

        if (_net != null)
        {
            _net.Connect(GameNetwork.SignalName.OnLojinhaData, Callable.From((ulong id, bool owner, string ownerName, string shopName, bool isOpen, int maxSlots, int gold, Godot.Collections.Array<Godot.Collections.Dictionary> items) => OnLojinhaData(id, owner, ownerName, shopName, isOpen, maxSlots, gold, items)));
            _net.Connect(GameNetwork.SignalName.OnLojinhaBuyResult, Callable.From((bool success, string message) => OnBuyResult(success, message)));
        }

        _tituloLabel.GuiInput += OnTituloGuiInput;
        _ready = true;
        AplicarEstado();
    }

    public void Setup(ulong lojinhaId, bool isOwner, string ownerName, string shopName, bool isOpen, int maxSlots, Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        _lojinhaId = lojinhaId;
        _isOwner = isOwner;
        _ownerName = ownerName;
        _shopName = string.IsNullOrWhiteSpace(shopName) ? $"Loja de {ownerName}" : shopName;
        _isOpen = isOpen;
        _maxSlots = maxSlots;
        _pendingItems = items;
        AplicarEstado();
    }

    private void CriarControlesDono()
    {
        _statusLabel = new Label
        {
            Text = "",
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 1f));
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        _ownerPanel.AddChild(_statusLabel);
        _ownerPanel.MoveChild(_statusLabel, 0);

        _nomeLojaEdit = new LineEdit
        {
            PlaceholderText = "Nome da loja",
            CustomMinimumSize = new Vector2(0, 28),
        };
        _ownerPanel.AddChild(_nomeLojaEdit);
        _ownerPanel.MoveChild(_nomeLojaEdit, 1);

        var modoRow = new HBoxContainer();
        _comprarModoBtn = new Button { Text = "Comprar", ToggleMode = true };
        _venderModoBtn = new Button { Text = "Vender", ToggleMode = true, ButtonPressed = true };
        _comprarModoBtn.Pressed += () => DefinirModoVenda(false);
        _venderModoBtn.Pressed += () => DefinirModoVenda(true);
        modoRow.AddChild(_comprarModoBtn);
        modoRow.AddChild(_venderModoBtn);
        _ownerPanel.AddChild(modoRow);
        _ownerPanel.MoveChild(modoRow, 2);

        var abrirRow = new HBoxContainer();
        _salvarRascunhoBtn = new Button { Text = "Salvar" };
        _abrirLojaBtn = new Button { Text = "Abrir Loja" };
        _salvarRascunhoBtn.Pressed += () => EnviarConfig(false);
        _abrirLojaBtn.Pressed += () => EnviarConfig(true);
        abrirRow.AddChild(_salvarRascunhoBtn);
        abrirRow.AddChild(_abrirLojaBtn);
        _ownerPanel.AddChild(abrirRow);
        _ownerPanel.MoveChild(abrirRow, 3);
    }

    private void AplicarEstado()
    {
        if (!_ready) return;

        _tituloLabel.Text = _shopName;
        _nomeLojaEdit.Text = _shopName;
        _ownerPanel.Visible = _isOwner;
        _buyerPanel.Visible = !_isOwner;
        _statusLabel.Text = _isOpen ? $"Aberta | Slots {_pendingItems.Count}/{_maxSlots}" : $"Rascunho | Slots {_pendingItems.Count}/{_maxSlots}";
        _abrirLojaBtn.Text = _isOpen ? "Atualizar Loja" : "Abrir Loja";

        DefinirModoVenda(_isOwner);
        UpdateItemList(_pendingItems);
    }

    private void DefinirModoVenda(bool venda)
    {
        if (!_ready || !_isOwner) return;
        _comprarModoBtn.ButtonPressed = !venda;
        _venderModoBtn.ButtonPressed = venda;
        _dropZone.Visible = venda;
        var hint = _dropZone.GetNodeOrNull<Label>("DropLabel");
        if (hint != null)
            hint.Text = venda ? "Solte um item aqui para escolher quantidade e preco" : "Modo compra: confira os itens da sua loja";
    }

    private void UpdateItemList(Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        foreach (var child in _itemsContainer.GetChildren())
            child.QueueFree();

        foreach (var dict in items)
        {
            var row = new LojinhaItemRow();
            row.Setup(
                (int)dict["slot"],
                (int)dict["item_id"],
                dict.ContainsKey("name") ? (string)dict["name"] : "",
                (int)dict["quantity"],
                (int)dict["price"],
                (string)dict["roll_data"],
                _isOwner,
                _lojinhaId,
                _net
            );
            _itemsContainer.AddChild(row);
        }
    }

    private void OnLojinhaData(ulong lojinhaId, bool isOwner, string ownerName, string shopName, bool isOpen, int maxSlots, int goldEarned, Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        if (lojinhaId != _lojinhaId) return;

        _isOwner = isOwner;
        _ownerName = ownerName;
        _shopName = string.IsNullOrWhiteSpace(shopName) ? $"Loja de {ownerName}" : shopName;
        _isOpen = isOpen;
        _maxSlots = maxSlots;
        _pendingItems = items;

        if (_ready)
        {
            AplicarEstado();
            _goldEarnedLabel.Text = goldEarned > 0 ? $"Gold acumulado: {goldEarned}" : "";
            _coletarBtn.Disabled = goldEarned <= 0;
        }
    }

    private void OnLojinhaDrop(Variant data)
    {
        if (data.AsGodotObject() is not SlotUI slot || slot.SlotInterno?.Item == null)
            return;

        int maxQtd = Mathf.Max(1, slot.SlotInterno.Quantidade);
        string itemName = slot.SlotInterno.Item.Nome ?? $"Item {slot.SlotInterno.Item.ItemID}";
        PedirQuantidade(slot.SlotIndex, maxQtd, itemName);
    }

    private void PedirQuantidade(int invSlot, int maxQtd, string itemName)
    {
        var dialog = CriarDialogoTexto($"Vender {itemName}", $"Quantidade (max {maxQtd})", "1", out var input);
        dialog.Confirmed += () =>
        {
            int quantidade = 1;
            if (int.TryParse(input.Text, out var parsed))
                quantidade = Mathf.Clamp(parsed, 1, maxQtd);
            PedirPreco(invSlot, quantidade, itemName);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(320, 150));
    }

    private void PedirPreco(int invSlot, int quantidade, string itemName)
    {
        var dialog = CriarDialogoTexto($"Preco de {itemName}", "Gold por unidade", "1", out var input);
        dialog.Confirmed += () =>
        {
            int preco = 1;
            if (int.TryParse(input.Text, out var parsed))
                preco = Mathf.Max(1, parsed);
            _net?.SendLojinhaAddItem(_lojinhaId, invSlot, quantidade, preco);
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(320, 150));
    }

    private ConfirmationDialog CriarDialogoTexto(string titulo, string label, string valor, out LineEdit input)
    {
        var dialog = new ConfirmationDialog
        {
            Title = titulo,
            OkButtonText = "Confirmar",
            CancelButtonText = "Cancelar",
        };
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = label });
        input = new LineEdit { Text = valor, SelectAllOnFocus = true };
        box.AddChild(input);
        dialog.AddChild(box);
        return dialog;
    }

    private void EnviarConfig(bool abrir)
    {
        _net?.SendLojinhaConfigure(_lojinhaId, _nomeLojaEdit.Text, abrir);
    }

    private void OnColetar()
    {
        _net?.SendLojinhaCollect(_lojinhaId);
    }

    private void OnFecharLojinha()
    {
        _net?.SendLojinhaClose(_lojinhaId);
        QueueFree();
    }

    private void OnBuyResult(bool success, string message)
    {
        _feedbackLabel.Text = message;
        _feedbackLabel.Modulate = success ? new Color(0.3f, 0.8f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
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

public partial class LojinhaItemRow : HBoxContainer
{
    private int _slot;
    private int _itemId;
    private string _serverItemName = "";
    private int _quantity;
    private int _price;
    private bool _isOwner;
    private ulong _lojinhaId;
    private GameNetwork _net;

    private TextureRect _icone;
    private Label _nomeLabel;
    private Label _qtdLabel;
    private Label _precoLabel;
    private Button _acaoBtn;
    private LineEdit _qtdCompra;
    private ItemDatabase _itemDB;

    public void Setup(int slot, int itemId, string itemName, int quantity, int price, string rollData, bool isOwner, ulong lojinhaId, GameNetwork net)
    {
        _slot = slot;
        _itemId = itemId;
        _serverItemName = itemName ?? "";
        _quantity = quantity;
        _price = price;
        _isOwner = isOwner;
        _lojinhaId = lojinhaId;
        _net = net;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 38);

        _icone = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        AddChild(_icone);

        _nomeLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _nomeLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        AddChild(_nomeLabel);

        _qtdLabel = new Label { CustomMinimumSize = new Vector2(50, 0) };
        AddChild(_qtdLabel);

        _precoLabel = new Label { CustomMinimumSize = new Vector2(90, 0) };
        _precoLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.3f));
        AddChild(_precoLabel);

        if (!_isOwner)
        {
            _qtdCompra = new LineEdit
            {
                CustomMinimumSize = new Vector2(44, 0),
                Text = "1",
            };
            AddChild(_qtdCompra);
        }

        _acaoBtn = new Button { CustomMinimumSize = new Vector2(88, 0) };
        AddChild(_acaoBtn);

        _nomeLabel.Text = string.IsNullOrWhiteSpace(_serverItemName) ? $"Item {itemId}" : _serverItemName;
        CallDeferred(nameof(AtualizarVisualItem));

        _qtdLabel.Text = $"x{quantity}";
        _precoLabel.Text = $"{price} cada";

        if (_isOwner)
        {
            _acaoBtn.Text = "Remover";
            _acaoBtn.Pressed += OnRemover;
        }
        else
        {
            _acaoBtn.Text = "Comprar";
            _acaoBtn.Pressed += OnComprar;
        }
    }

    private void AtualizarVisualItem()
    {
        _itemDB ??= GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase");
        _itemDB ??= GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
        var def = _itemDB?.GetItem(_itemId);
        if (def == null)
            return;

        _nomeLabel.Text = string.IsNullOrWhiteSpace(def.Nome) ? _nomeLabel.Text : def.Nome;
        if (def.Icone != null)
            _icone.Texture = def.Icone;
    }

    private void OnRemover()
    {
        _net?.SendLojinhaRemoveItem(_lojinhaId, _slot);
    }

    private void OnComprar()
    {
        int qtd = 1;
        if (_qtdCompra != null && int.TryParse(_qtdCompra.Text, out var parsed) && parsed > 0)
            qtd = parsed;
        qtd = Mathf.Min(qtd, _quantity);
        _net?.SendLojinhaBuyItem(_lojinhaId, _slot, qtd);
    }
}
