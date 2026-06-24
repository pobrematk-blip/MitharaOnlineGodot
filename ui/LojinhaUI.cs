using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class LojinhaUI : Control
{
    private ulong _lojinhaId;
    private bool _isOwner;
    private string _ownerName;
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

    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;

    private List<LojinhaItemEntry> _items = new();

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

        _fecharBtn.Pressed += () => QueueFree();
        _coletarBtn.Pressed += OnColetar;
        _fecharLojinhaBtn.Pressed += OnFecharLojinha;

        if (_net != null)
        {
            _net.Connect(GameNetwork.SignalName.OnLojinhaData, Callable.From((ulong id, bool owner, string name, int gold, Godot.Collections.Array<Godot.Collections.Dictionary> items) => OnLojinhaData(id, owner, name, gold, items)));
            _net.Connect(GameNetwork.SignalName.OnLojinhaBuyResult, Callable.From((bool success, string message) => OnBuyResult(success, message)));
            _net.Connect(GameNetwork.SignalName.OnGoldUpdate, Callable.From((int gold) => { }));
        }

        _tituloLabel.GuiInput += OnTituloGuiInput;

        _ownerPanel.Visible = _isOwner;
        _buyerPanel.Visible = !_isOwner;
    }

    public void Setup(ulong lojinhaId, bool isOwner, string ownerName, Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        _lojinhaId = lojinhaId;
        _isOwner = isOwner;
        _ownerName = ownerName;
        _tituloLabel.Text = $"Lojinha de {ownerName}";
        _goldEarnedLabel.Text = "";

        if (_isOwner && _ownerPanel != null)
        {
            _ownerPanel.Visible = true;
            _buyerPanel.Visible = false;
        }
        else if (!_isOwner && _buyerPanel != null)
        {
            _buyerPanel.Visible = true;
            _ownerPanel.Visible = false;
        }

        UpdateItemList(items);
    }

    private void UpdateItemList(Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        foreach (var child in _itemsContainer.GetChildren())
        {
            if (child is LojinhaItemRow row)
                row.QueueFree();
        }
        _items.Clear();

        foreach (var dict in items)
        {
            var row = new LojinhaItemRow();
            row.Setup(
                (int)dict["slot"],
                (int)dict["item_id"],
                (int)dict["quantity"],
                (int)dict["price"],
                (string)dict["roll_data"],
                _isOwner,
                _lojinhaId,
                _net
            );
            _itemsContainer.AddChild(row);
            _items.Add(new LojinhaItemEntry { Slot = (int)dict["slot"], ItemId = (int)dict["item_id"] });
        }
    }

    private void OnLojinhaData(ulong lojinhaId, bool isOwner, string ownerName, int goldEarned, Godot.Collections.Array<Godot.Collections.Dictionary> items)
    {
        if (lojinhaId != _lojinhaId) return;
        UpdateItemList(items);
        _goldEarnedLabel.Text = goldEarned > 0 ? $"Gold acumulado: {goldEarned}" : "";
        _coletarBtn.Disabled = goldEarned <= 0;
    }

    private void OnColetar()
    {
        if (_net != null)
            _net.SendLojinhaCollect(_lojinhaId);
    }

    private void OnFecharLojinha()
    {
        if (_net != null)
            _net.SendLojinhaClose(_lojinhaId);
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

    public class LojinhaItemEntry
    {
        public int Slot { get; set; }
        public int ItemId { get; set; }
    }
}

public partial class LojinhaItemRow : HBoxContainer
{
    private int _slot;
    private int _itemId;
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

    public void Setup(int slot, int itemId, int quantity, int price, string rollData, bool isOwner, ulong lojinhaId, GameNetwork net)
    {
        _slot = slot;
        _itemId = itemId;
        _quantity = quantity;
        _price = price;
        _isOwner = isOwner;
        _lojinhaId = lojinhaId;
        _net = net;
        _itemDB = GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 36);

        _icone = new TextureRect();
        _icone.CustomMinimumSize = new Vector2(32, 32);
        _icone.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icone.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        AddChild(_icone);

        _nomeLabel = new Label();
        _nomeLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _nomeLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));
        AddChild(_nomeLabel);

        _qtdLabel = new Label();
        _qtdLabel.CustomMinimumSize = new Vector2(50, 0);
        AddChild(_qtdLabel);

        _precoLabel = new Label();
        _precoLabel.CustomMinimumSize = new Vector2(80, 0);
        _precoLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.3f));
        AddChild(_precoLabel);

        if (!_isOwner)
        {
            _qtdCompra = new LineEdit();
            _qtdCompra.CustomMinimumSize = new Vector2(40, 0);
            _qtdCompra.Text = "1";
            AddChild(_qtdCompra);
        }

        _acaoBtn = new Button();
        _acaoBtn.CustomMinimumSize = new Vector2(80, 0);
        AddChild(_acaoBtn);

        if (_itemDB != null)
        {
            var def = _itemDB.GetItem(itemId);
            if (def != null)
            {
                _nomeLabel.Text = def.Nome ?? $"Item {itemId}";
                if (def.Icone != null)
                    _icone.Texture = def.Icone;
            }
            else
            {
                _nomeLabel.Text = $"Item {itemId}";
            }
        }
        else
        {
            _nomeLabel.Text = $"Item {itemId}";
        }

        _qtdLabel.Text = $"x{quantity}";
        _precoLabel.Text = $"{price} gold";

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
