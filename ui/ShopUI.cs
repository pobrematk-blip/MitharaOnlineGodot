using Godot;
using System.Collections.Generic;

public partial class ShopUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private VBoxContainer _itemsContainer;
    private Label _goldLabel;
    private Label _titleLabel;
    private Label _statusLabel;
    private GameNetwork _gameNet;
    private ItemDatabase _itemDB;
    private InventarioComponent _inventario;
    private EquipamentoComponent _equipamento;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private string _currentShopId = "";
    private bool _sellMode;

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _itemsContainer = _panel.GetNode<VBoxContainer>("MarginContainer/ScrollContainer/ItemsContainer");
        _goldLabel = _panel.GetNode<Label>("GoldLabel");
        _titleLabel = _panel.GetNode<Label>("TitleBar/TitleLabel");
        _statusLabel = _panel.GetNode<Label>("StatusLabel");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _itemDB = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase");

        if (_gameNet != null)
        {
            _gameNet.OnNpcShop += OnNpcShop;
            _gameNet.OnNpcBuyResult += OnNpcBuyResult;
            _gameNet.OnNpcSellResult += OnNpcSellResult;
            _gameNet.OnGoldUpdate += OnGoldUpdate;
        }

        _closeButton.Pressed += Fechar;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _panel.Visible = false;
    }

    private void OnNpcShop(string shopId, Godot.Collections.Array items)
    {
        _currentShopId = shopId;
        _sellMode = shopId == "merchant_sell";
        _titleLabel.Text = _sellMode ? "General Merchante - Vender" : "General Merchante - Comprar";
        _statusLabel.Text = "";

        LimparLista();

        if (_sellMode)
        {
            ConectarInventario();
            PopularItensVenda();
            MostrarPainel();
            return;
        }

        foreach (Godot.Collections.Dictionary entry in items)
        {
            int itemId = entry["item_id"].AsInt32();
            int price = entry["price"].AsInt32();
            int stock = entry["stock"].AsInt32();

            ItemResource itemRes = _itemDB?.GetItem(itemId);
            string itemName = itemRes != null ? itemRes.Nome : $"Item #{itemId}";

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.CustomMinimumSize = new Vector2(0, 32);

            var nameLabel = new Label();
            nameLabel.Text = itemName;
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.9f));

            var priceLabel = new Label();
            priceLabel.Text = $"{price} gold";
            priceLabel.CustomMinimumSize = new Vector2(70, 0);
            priceLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0, 0.9f));

            var stockLabel = new Label();
            stockLabel.Text = stock > 0 ? $"Estoque: {stock}" : "∞";
            stockLabel.CustomMinimumSize = new Vector2(70, 0);
            stockLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 0.8f));

            var buyBtn = new Button();
            buyBtn.Text = "Comprar";
            int capturedItemId = itemId;
            buyBtn.Pressed += () =>
            {
                if (_gameNet != null)
                    _gameNet.SendNpcBuyItem(_currentShopId, capturedItemId, 1);
            };

            hbox.AddChild(nameLabel);
            hbox.AddChild(priceLabel);
            hbox.AddChild(stockLabel);
            hbox.AddChild(buyBtn);

            _itemsContainer.AddChild(hbox);
        }

        if (items.Count == 0)
            AdicionarMensagemLista("Nenhum item disponível para compra.");

        MostrarPainel();
    }

    private void OnNpcBuyResult(bool success, string message)
    {
        GD.Print($"[SHOP] {message}");
        MostrarStatus(success, message);
        AtualizarGold();
    }

    private void OnNpcSellResult(bool success, string message)
    {
        GD.Print($"[SHOP] {message}");
        MostrarStatus(success, message);
        AtualizarGold();
    }

    private void ConectarInventario()
    {
        if (_inventario != null && IsInstanceValid(_inventario))
            return;

        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        _inventario = player?.FindChild("InventarioComponent", true, false) as InventarioComponent;
        _equipamento = player?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (_inventario != null)
            _inventario.InventarioAtualizado += OnInventarioAtualizado;
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado += OnEquipamentoAtualizado;
    }

    private void OnInventarioAtualizado()
    {
        if (_sellMode && _panel.Visible)
            PopularItensVenda();
    }

    private void OnEquipamentoAtualizado()
    {
        if (_sellMode && _panel.Visible)
            CallDeferred(MethodName.PopularItensVenda);
    }

    private void PopularItensVenda()
    {
        LimparLista();
        ConectarInventario();

        if (_inventario == null)
        {
            AdicionarMensagemLista("Inventário não encontrado.");
            return;
        }

        int encontrados = 0;
        var instanciasEquipadas = new HashSet<string>();
        if (_equipamento != null)
        {
            foreach (SlotInventario equipado in _equipamento.ItensEquipados.Values)
            {
                if (equipado?.Item != null && !string.IsNullOrWhiteSpace(equipado.DadosInstancia))
                    instanciasEquipadas.Add(equipado.DadosInstancia);
            }
        }

        for (int slotIndex = 0; slotIndex < _inventario.Slots.Count; slotIndex++)
        {
            SlotInventario slot = _inventario.Slots[slotIndex];
            ItemResource item = slot?.Item;
            if (item == null || !item.PodeVender || item.Valor <= 0)
                continue;
            if (!string.IsNullOrWhiteSpace(slot.DadosInstancia)
                && instanciasEquipadas.Contains(slot.DadosInstancia))
                continue;

            encontrados++;
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 36) };
            row.AddThemeConstantOverride("separation", 6);

            var nameLabel = new Label
            {
                Text = $"{item.Nome} x{slot.Quantidade}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            int unitPrice = Mathf.Max(1, item.Valor / 4);
            var priceLabel = new Label
            {
                Text = $"{unitPrice} gold/un.",
                CustomMinimumSize = new Vector2(85, 0),
            };
            priceLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0f));

            int capturedSlot = slotIndex;
            int capturedQuantity = slot.Quantidade;
            var sellOne = new Button { Text = "Vender 1" };
            sellOne.Pressed += () => _gameNet?.SendNpcSellItem(capturedSlot, 1);

            var sellAll = new Button { Text = "Vender tudo" };
            sellAll.Pressed += () => _gameNet?.SendNpcSellItem(capturedSlot, capturedQuantity);

            row.AddChild(nameLabel);
            row.AddChild(priceLabel);
            row.AddChild(sellOne);
            row.AddChild(sellAll);
            _itemsContainer.AddChild(row);
        }

        if (encontrados == 0)
            AdicionarMensagemLista("Você não possui itens que possam ser vendidos.");
    }

    private void LimparLista()
    {
        foreach (Node child in _itemsContainer.GetChildren())
        {
            _itemsContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void AdicionarMensagemLista(string message)
    {
        _itemsContainer.AddChild(new Label
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 48),
        });
    }

    private void MostrarPainel()
    {
        _panel.Visible = true;
        CallDeferred(MethodName.Centralizar);
        AtualizarGold();
    }

    private void MostrarStatus(bool success, string message)
    {
        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", success
            ? new Color(0.3f, 0.9f, 0.4f)
            : new Color(1f, 0.35f, 0.35f));
    }

    private void OnGoldUpdate(int gold)
    {
        _goldLabel.Text = $"Gold: {gold}";
    }

    private void AtualizarGold()
    {
        _goldLabel.Text = $"Gold: {_gameNet?.Gold ?? 0}";
    }

    private void Centralizar()
    {
        Vector2 tela = GetViewportRect().Size;
        _panel.Position = (tela / 2) - (_panel.Size / 2);
    }

    private void Fechar()
    {
        _panel.Visible = false;
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
        {
            _gameNet.OnNpcShop -= OnNpcShop;
            _gameNet.OnNpcBuyResult -= OnNpcBuyResult;
            _gameNet.OnNpcSellResult -= OnNpcSellResult;
            _gameNet.OnGoldUpdate -= OnGoldUpdate;
        }

        if (_inventario != null)
            _inventario.InventarioAtualizado -= OnInventarioAtualizado;
        if (_equipamento != null)
            _equipamento.EquipamentoAtualizado -= OnEquipamentoAtualizado;
    }
}
