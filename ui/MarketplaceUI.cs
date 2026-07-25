#nullable enable
using Godot;
using System;
using System.Linq;

public partial class MarketplaceUI : Control
{
    private static readonly Vector2 WindowSize = new(820, 620);

    private Panel _panel = null!;
    private Label _title = null!;
    private Label _rules = null!;
    private Label _feedback = null!;
    private LineEdit _search = null!;
    private OptionButton _category = null!;
    private VBoxContainer _list = null!;
    private MarketplaceDropSlot _itemDropSlot = null!;
    private TextureRect _selectedIcon = null!;
    private Label _selectedItemLabel = null!;
    private SpinBox _quantity = null!;
    private SpinBox _goldPrice = null!;
    private SpinBox _pixPrice = null!;
    private SpinBox _goldAmount = null!;
    private OptionButton _duration = null!;
    private Button _myListingsButton = null!;
    private GameNetwork? _net;
    private ItemDatabase? _itemDb;
    private Godot.Collections.Array<Godot.Collections.Dictionary> _inventoryCache = new();
    private bool _dragging;
    private bool _ownOnly;
    private Vector2 _dragStart;
    private int _selectedInventorySlot = -1;
    private int _selectedItemId;
    private int _selectedQuantity;

    public override void _Ready()
    {
        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _itemDb = _net?.ItemDB ?? GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase") ?? GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
        BuildUi();
        if (_net != null)
        {
            _net.OnMarketplaceListResult += OnListings;
            _net.OnMarketplaceActionResult += OnActionResult;
            _net.OnInventoryData += OnInventoryData;
            _net.SendMarketplaceListRequest();
            _net.SendInventoryRequest();
        }
        CallDeferred(nameof(AbrirInventarioAoLado));
    }

    public void Configure(string title, string rules)
    {
        if (_panel == null)
            CallDeferred(nameof(Configure), title, rules);
        else
        {
            _title.Text = title;
            _rules.Text = rules;
        }
    }

    private void BuildUi()
    {
        AnchorLeft = 0;
        AnchorTop = 0;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Stop;

        _panel = new Panel { Size = WindowSize, CustomMinimumSize = WindowSize };
        _panel.Position = (GetViewportRect().Size - WindowSize) / 2;
        _panel.AddThemeStyleboxOverride("panel", MitharaUiTheme.Panel(0.95f));
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var titleBar = new HBoxContainer { Name = "TitleBar", MouseFilter = MouseFilterEnum.Stop };
        titleBar.GuiInput += OnTitleBarGuiInput;
        root.AddChild(titleBar);
        _title = new Label
        {
            Name = "Title",
            Text = "Mercado de Jogadores",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _title.AddThemeFontSizeOverride("font_size", 15);
        _title.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        titleBar.AddChild(_title);
        var close = CreateButton("X", 30);
        close.Flat = true;
        close.Pressed += QueueFree;
        titleBar.AddChild(close);

        var rulesPanel = CreateInnerPanel(0, 48);
        root.AddChild(rulesPanel);
        _rules = new Label
        {
            Text = "Compras por PIX nao possuem reembolso e so entregam apos confirmacao segura.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _rules.AddThemeFontSizeOverride("font_size", 11);
        _rules.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
        AddPadded(rulesPanel, _rules, 8, 5);

        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 7);
        root.AddChild(filters);
        _search = new LineEdit
        {
            PlaceholderText = "Pesquisar item ou vendedor",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        filters.AddChild(_search);
        _category = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
        AddCategory("Todas", -1);
        AddCategory("Armas", 1);
        AddCategory("Escudos", 2);
        AddCategory("Armaduras", 4);
        AddCategory("Consumiveis", 17);
        AddCategory("Materiais", 18);
        filters.AddChild(_category);
        var refresh = CreateButton("Buscar", 90);
        refresh.Pressed += RequestList;
        filters.AddChild(refresh);
        _myListingsButton = CreateButton("Meus anuncios", 120);
        _myListingsButton.ToggleMode = true;
        _myListingsButton.Pressed += () =>
        {
            _ownOnly = _myListingsButton.ButtonPressed;
            RequestList();
        };
        filters.AddChild(_myListingsButton);

        var listPanel = CreateInnerPanel(0, 286);
        listPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(listPanel);
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddPadded(listPanel, scroll, 8, 8);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(_list);

        _feedback = new Label { Text = "" };
        _feedback.AddThemeFontSizeOverride("font_size", 11);
        _feedback.AddThemeColorOverride("font_color", MitharaUiTheme.Accent);
        root.AddChild(_feedback);

        root.AddChild(CreateSectionTitle("Anunciar item selecionado"));
        var sellGold = new HBoxContainer();
        sellGold.AddThemeConstantOverride("separation", 8);
        root.AddChild(sellGold);

        _itemDropSlot = new MarketplaceDropSlot { CustomMinimumSize = new Vector2(54, 54) };
        _itemDropSlot.AddThemeStyleboxOverride("panel", MitharaUiTheme.Slot());
        _itemDropSlot.OnMarketplaceItemDropped += OnMarketplaceItemDropped;
        _selectedIcon = new TextureRect
        {
            Size = new Vector2(42, 42),
            Position = new Vector2(6, 6),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _itemDropSlot.AddChild(_selectedIcon);
        sellGold.AddChild(_itemDropSlot);

        _selectedItemLabel = new Label
        {
            Text = "Arraste um item do inventario",
            CustomMinimumSize = new Vector2(150, 54),
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _selectedItemLabel.AddThemeFontSizeOverride("font_size", 11);
        _selectedItemLabel.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
        sellGold.AddChild(_selectedItemLabel);

        _quantity = CreateSpin("Qtd", 1, 9999);
        _goldPrice = CreateSpin("Gold/un", 1, 1000000000);
        _duration = new OptionButton { CustomMinimumSize = new Vector2(100, 28) };
        _duration.AddItem("24 horas", 24);
        _duration.AddItem("48 horas", 48);
        _duration.AddItem("7 dias", 168);
        sellGold.AddChild(_quantity);
        sellGold.AddChild(_goldPrice);
        sellGold.AddChild(_duration);
        var createGold = CreateButton("Anunciar", 92);
        createGold.Pressed += () => AnunciarItem(1);
        sellGold.AddChild(createGold);

        root.AddChild(CreateSectionTitle("VIP: anunciar item ou gold por PIX"));
        var sellPix = new HBoxContainer();
        sellPix.AddThemeConstantOverride("separation", 6);
        root.AddChild(sellPix);
        _pixPrice = CreateSpin("PIX centavos", 100, 10000000);
        _goldAmount = CreateSpin("Pacote fechado de 1.000.000 gold", 1000000, 1000000);
        _goldAmount.Value = 1000000;
        _goldAmount.Editable = false;
        sellPix.AddChild(_pixPrice);
        sellPix.AddChild(_goldAmount);
        var createPixItem = CreateButton("Item por PIX", 110);
        createPixItem.Pressed += () => AnunciarItem(2);
        sellPix.AddChild(createPixItem);
        var createPixGold = CreateButton("Pacote 1M PIX", 118);
        createPixGold.Pressed += () => _net?.SendMarketplaceCreateGoldListing(1000000, (int)_pixPrice.Value, GetSelectedDurationHours());
        sellPix.AddChild(createPixGold);
    }

    private void AddCategory(string text, int id)
    {
        _category.AddItem(text, id);
    }

    private void OnMarketplaceItemDropped(int inventorySlot, int quantity)
    {
        SelectInventorySlot(inventorySlot, quantity);
    }

    private void SelectInventorySlot(int inventorySlot, int quantity)
    {
        _selectedInventorySlot = inventorySlot;
        _selectedQuantity = Math.Max(1, quantity);
        _selectedItemId = 0;
        foreach (var entry in _inventoryCache)
        {
            if (!entry.ContainsKey("slot") || entry["slot"].AsInt32() != inventorySlot)
                continue;
            _selectedItemId = entry["item_id"].AsInt32();
            _selectedQuantity = Math.Max(1, entry["quantity"].AsInt32());
            break;
        }

        var item = _selectedItemId > 0 ? _itemDb?.GetItem(_selectedItemId) : null;
        _selectedIcon.Texture = item?.Icone;
        _selectedItemLabel.Text = item != null
            ? $"{item.Nome}\nSlot {_selectedInventorySlot} | Qtd {_selectedQuantity}"
            : $"Slot {_selectedInventorySlot} | Qtd {_selectedQuantity}";
        _selectedItemLabel.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        _quantity.MaxValue = _selectedQuantity;
        _quantity.Value = _selectedQuantity;
    }

    private void AnunciarItem(int currencyType)
    {
        if (_selectedInventorySlot < 0)
        {
            OnActionResult(false, "Arraste um item do inventario para o slot do leilao.");
            return;
        }

        _net?.SendMarketplaceCreateItemListing(
            _selectedInventorySlot,
            (int)_quantity.Value,
            currencyType,
            currencyType == 1 ? (int)_goldPrice.Value : 0,
            currencyType == 2 ? (int)_pixPrice.Value : 0,
            GetSelectedDurationHours());
    }

    private int GetSelectedDurationHours()
    {
        int id = _duration.GetSelectedId();
        return id is 24 or 48 or 168 ? id : 24;
    }

    private static SpinBox CreateSpin(string tooltip, double min, double max)
    {
        return new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Value = min,
            Step = 1,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(112, 28),
        };
    }

    private void RequestList()
    {
        int type = _category.GetSelectedId();
        _net?.SendMarketplaceListRequest(_search.Text, type, _ownOnly);
    }

    private void OnInventoryData(Godot.Collections.Array<Godot.Collections.Dictionary> items, Godot.Collections.Array<Godot.Collections.Dictionary> equipment)
    {
        _inventoryCache = items;
        if (_selectedInventorySlot >= 0 && !items.Any(e => e.ContainsKey("slot") && e["slot"].AsInt32() == _selectedInventorySlot))
            LimparItemSelecionado();
    }

    private void OnListings(Godot.Collections.Array<Godot.Collections.Dictionary> listings)
    {
        foreach (var child in _list.GetChildren())
            child.QueueFree();

        if (listings.Count == 0)
        {
            var empty = new Label { Text = "Nenhum anuncio encontrado." };
            empty.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            _list.AddChild(empty);
            return;
        }

        foreach (var listing in listings)
        {
            long id = listing["listing_id"].AsInt64();
            int listingType = listing["listing_type"].AsInt32();
            int currency = listing["currency_type"].AsInt32();
            string name = listingType == 2
                ? $"{listing["gold_amount"].AsInt32()} Gold"
                : listing["item_name"].AsString();
            string price = currency == 1
                ? $"{listing["price_per_unit_gold"].AsInt32()} gold/un"
                : $"PIX R$ {(listing["price_total_cents"].AsInt32() / 100.0):0.00}";
            bool isMine = listing.ContainsKey("is_mine") && listing["is_mine"].AsBool();
            string status = listing.ContainsKey("status") ? listing["status"].AsString() : "";
            int proceedsGold = listing.ContainsKey("proceeds_gold") ? listing["proceeds_gold"].AsInt32() : 0;
            bool proceedsClaimed = listing.ContainsKey("proceeds_claimed") && listing["proceeds_claimed"].AsBool();
            long expiresBinary = listing.ContainsKey("expires_at") ? listing["expires_at"].AsInt64() : 0L;
            string expiresText = expiresBinary != 0
                ? DateTime.FromBinary(expiresBinary).ToLocalTime().ToString("dd/MM HH:mm")
                : "--";

            var rowPanel = CreateInnerPanel(0, 48, 0.62f);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            AddPadded(rowPanel, row, 6, 5);

            var iconSlot = new Panel { CustomMinimumSize = new Vector2(38, 38) };
            iconSlot.AddThemeStyleboxOverride("panel", MitharaUiTheme.Slot());
            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new Vector2(32, 32),
                Position = new Vector2(3, 3),
                MouseFilter = MouseFilterEnum.Ignore
            };
            if (listing.ContainsKey("item_id"))
            {
                var item = _itemDb?.GetItem(listing["item_id"].AsInt32());
                if (item?.Icone != null)
                    icon.Texture = item.Icone;
            }
            iconSlot.AddChild(icon);
            row.AddChild(iconSlot);

            var nameBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            nameBox.AddThemeConstantOverride("separation", 0);
            var nameLabel = new Label { Text = name, ClipText = true };
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            nameLabel.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
            nameBox.AddChild(nameLabel);
            var sellerLabel = new Label
            {
                Text = isMine
                    ? $"Status: {status} | Expira: {expiresText}"
                    : $"Vendedor: {listing["seller_name"].AsString()} | Expira: {expiresText}"
            };
            sellerLabel.AddThemeFontSizeOverride("font_size", 10);
            sellerLabel.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
            nameBox.AddChild(sellerLabel);
            row.AddChild(nameBox);

            var qtyLabel = new Label { Text = $"Qtd {listing["quantity"].AsInt32()}", CustomMinimumSize = new Vector2(72, 0) };
            qtyLabel.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
            row.AddChild(qtyLabel);
            var priceLabel = new Label { Text = price, CustomMinimumSize = new Vector2(132, 0) };
            priceLabel.AddThemeColorOverride("font_color", currency == 1 ? MitharaUiTheme.Accent : Color.FromHtml("#72d6ff"));
            row.AddChild(priceLabel);
            var buy = CreateButton(GetListingButtonText(isMine, status, currency, proceedsGold, proceedsClaimed), 104);
            buy.Pressed += () =>
            {
                if (isMine && status == "sold" && currency == 1 && proceedsGold > 0 && !proceedsClaimed)
                    _net?.SendMarketplaceClaimGold(id);
                else if (isMine && (status == "active" || status == "pending_payment" || status == "expired"))
                    _net?.SendMarketplaceCancelListing(id);
                else if (currency == 1)
                    _net?.SendMarketplaceBuyListing(id);
                else
                    OS.ShellOpen("https://mithara.online/Marketplace");
            };
            buy.Disabled = isMine && status == "sold" && (currency != 1 || proceedsGold <= 0 || proceedsClaimed);
            row.AddChild(buy);
            _list.AddChild(rowPanel);
        }
    }

    private void OnActionResult(bool success, string message)
    {
        if (_feedback != null && GodotObject.IsInstanceValid(_feedback))
        {
            _feedback.Text = message;
            _feedback.AddThemeColorOverride("font_color", success ? Color.FromHtml("#80e28e") : Color.FromHtml("#ff6b6b"));
        }
        GD.Print($"[MERCADO] {(success ? "OK" : "ERRO")}: {message}");
        if (success)
        {
            LimparItemSelecionado();
            _net?.SendInventoryRequest();
            RequestList();
        }
    }

    public override void _ExitTree()
    {
        if (_net != null)
        {
            _net.OnMarketplaceListResult -= OnListings;
            _net.OnMarketplaceActionResult -= OnActionResult;
            _net.OnInventoryData -= OnInventoryData;
        }
    }

    private static string GetListingButtonText(bool isMine, string status, int currency, int proceedsGold, bool proceedsClaimed)
    {
        if (isMine && status == "sold" && currency == 1)
            return proceedsGold > 0 && !proceedsClaimed ? "Retirar gold" : "Gold retirado";
        if (isMine && status == "sold")
            return "Vendido";
        if (isMine && (status == "active" || status == "pending_payment" || status == "expired"))
            return "Retirar";
        return currency == 1 ? "Comprar" : "Ver no site";
    }

    private void LimparItemSelecionado()
    {
        _selectedInventorySlot = -1;
        _selectedItemId = 0;
        _selectedQuantity = 0;
        if (_selectedIcon != null)
            _selectedIcon.Texture = null;
        if (_selectedItemLabel != null)
        {
            _selectedItemLabel.Text = "Arraste um item do inventario";
            _selectedItemLabel.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
        }
        if (_quantity != null)
        {
            _quantity.MaxValue = 9999;
            _quantity.Value = 1;
        }
    }

    private void AbrirInventarioAoLado()
    {
        var tree = GetTree();
        var inv = tree?.Root?.FindChild("InventarioUI", true, false) as InventarioUI
            ?? tree?.CurrentScene?.FindChild("InventarioUI", true, false) as InventarioUI;
        if (inv == null)
        {
            GD.PrintErr("[MERCADO] InventarioUI nao encontrado para abrir junto com o leilao.");
            return;
        }

        inv.AbrirPainel();
        inv.PosicionarPainel(_panel.GlobalPosition + new Vector2(_panel.Size.X + 14f, 0f));
    }

    private static Label CreateSectionTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", MitharaUiTheme.Accent);
        return label;
    }

    private static Button CreateButton(string text, float width)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 28),
        };
        return button;
    }

    private static Panel CreateInnerPanel(float width, float height, float alpha = 0.68f)
    {
        var panel = new Panel();
        panel.CustomMinimumSize = new Vector2(width, height);
        panel.AddThemeStyleboxOverride("panel", MitharaUiTheme.Inner(alpha));
        return panel;
    }

    private static void AddPadded(Control parent, Control child, int horizontal, int vertical)
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", horizontal);
        margin.AddThemeConstantOverride("margin_top", vertical);
        margin.AddThemeConstantOverride("margin_right", horizontal);
        margin.AddThemeConstantOverride("margin_bottom", vertical);
        parent.AddChild(margin);
        margin.AddChild(child);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _dragging = mouseEvent.Pressed;
            if (mouseEvent.Pressed)
            {
                _dragStart = mouseEvent.Position;
                MoveToFront();
                _panel.MoveToFront();
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _dragging)
        {
            _panel.Position += mouseMotion.Position - _dragStart;
            ResponsiveUI.ClampInsideViewport(_panel, 4f);
        }
    }
}
