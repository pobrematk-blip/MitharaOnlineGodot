using Godot;
using System.Collections.Generic;

public partial class ShopUI : Control
{
    private Panel _panel;
    private Panel _titleBar;
    private Button _closeButton;
    private VBoxContainer _itemsContainer;
    private Label _goldLabel;
    private GameNetwork _gameNet;
    private ItemDatabase _itemDB;
    private bool _arrastando;
    private Vector2 _pontoCliqueOriginal;
    private string _currentShopId = "";

    public override void _Ready()
    {
        _panel = GetNode<Panel>("Panel");
        _titleBar = _panel.GetNode<Panel>("TitleBar");
        _closeButton = _panel.GetNode<Button>("CloseButton");
        _itemsContainer = _panel.GetNode<VBoxContainer>("MarginContainer/ScrollContainer/ItemsContainer");
        _goldLabel = _panel.GetNode<Label>("GoldLabel");

        _gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _itemDB = GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase");

        if (_gameNet != null)
        {
            _gameNet.OnNpcShop += OnNpcShop;
            _gameNet.OnNpcBuyResult += OnNpcBuyResult;
            _gameNet.OnNpcSellResult += OnNpcSellResult;
        }

        _closeButton.Pressed += Fechar;
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _panel.Visible = false;
    }

    private void OnNpcShop(string shopId, Godot.Collections.Array items)
    {
        _currentShopId = shopId;

        foreach (var child in _itemsContainer.GetChildren())
            child.QueueFree();

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
            int capturedPrice = price;
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

        _panel.Visible = true;
        CallDeferred(MethodName.Centralizar);
        AtualizarGold();
    }

    private void OnNpcBuyResult(bool success, string message)
    {
        GD.Print($"[SHOP] {message}");
        AtualizarGold();
    }

    private void OnNpcSellResult(bool success, string message)
    {
        GD.Print($"[SHOP] {message}");
        AtualizarGold();
    }

    private void AtualizarGold()
    {
        var player = GetTree().CurrentScene?.FindChild("Player", true, false);
        if (player == null) return;
        var label = player.FindChild("GoldCountLabel", true, false) as Label;
        if (label != null)
            _goldLabel.Text = $"Gold: {label.Text}";
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
        }
    }
}
