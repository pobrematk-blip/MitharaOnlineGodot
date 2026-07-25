#nullable enable
using Godot;

public partial class MarketplaceUI : Control
{
    private static readonly Vector2 WindowSize = new(760, 560);

    private Panel _panel = null!;
    private Label _title = null!;
    private Label _rules = null!;
    private Label _feedback = null!;
    private LineEdit _search = null!;
    private OptionButton _category = null!;
    private VBoxContainer _list = null!;
    private SpinBox _slot = null!;
    private SpinBox _quantity = null!;
    private SpinBox _goldPrice = null!;
    private SpinBox _pixPrice = null!;
    private SpinBox _goldAmount = null!;
    private GameNetwork? _net;
    private ItemDatabase? _itemDb;
    private bool _dragging;
    private Vector2 _dragStart;

    public override void _Ready()
    {
        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        _itemDb = _net?.ItemDB ?? GetNodeOrNull<ItemDatabase>("/root/GameNetwork/ItemDatabase") ?? GetNodeOrNull<ItemDatabase>("/root/ItemDatabase");
        BuildUi();
        if (_net != null)
        {
            _net.OnMarketplaceListResult += OnListings;
            _net.OnMarketplaceActionResult += OnActionResult;
            _net.SendMarketplaceListRequest();
        }
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
        _category = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
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

        var listPanel = CreateInnerPanel(0, 252);
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

        root.AddChild(CreateSectionTitle("Anunciar item por gold"));
        var sellGold = new HBoxContainer();
        sellGold.AddThemeConstantOverride("separation", 6);
        root.AddChild(sellGold);
        _slot = CreateSpin("Slot", 0, 500);
        _quantity = CreateSpin("Qtd", 1, 9999);
        _goldPrice = CreateSpin("Gold/un", 1, 1000000000);
        sellGold.AddChild(_slot);
        sellGold.AddChild(_quantity);
        sellGold.AddChild(_goldPrice);
        var createGold = CreateButton("Anunciar", 92);
        createGold.Pressed += () => _net?.SendMarketplaceCreateItemListing((int)_slot.Value, (int)_quantity.Value, 1, (int)_goldPrice.Value, 0);
        sellGold.AddChild(createGold);

        root.AddChild(CreateSectionTitle("VIP: anunciar item ou gold por PIX"));
        var sellPix = new HBoxContainer();
        sellPix.AddThemeConstantOverride("separation", 6);
        root.AddChild(sellPix);
        _pixPrice = CreateSpin("PIX centavos", 100, 10000000);
        _goldAmount = CreateSpin("Gold", 1, 1000000000);
        sellPix.AddChild(_pixPrice);
        sellPix.AddChild(_goldAmount);
        var createPixItem = CreateButton("Item por PIX", 110);
        createPixItem.Pressed += () => _net?.SendMarketplaceCreateItemListing((int)_slot.Value, (int)_quantity.Value, 2, 0, (int)_pixPrice.Value);
        sellPix.AddChild(createPixItem);
        var createPixGold = CreateButton("Gold por PIX", 110);
        createPixGold.Pressed += () => _net?.SendMarketplaceCreateGoldListing((int)_goldAmount.Value, (int)_pixPrice.Value);
        sellPix.AddChild(createPixGold);
    }

    private void AddCategory(string text, int id)
    {
        _category.AddItem(text, id);
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
        _net?.SendMarketplaceListRequest(_search.Text, type);
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
            var sellerLabel = new Label { Text = $"Vendedor: {listing["seller_name"].AsString()}" };
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
            var buy = CreateButton(currency == 1 ? "Comprar" : "Ver no site", 92);
            buy.Pressed += () =>
            {
                if (currency == 1)
                    _net?.SendMarketplaceBuyListing(id);
                else
                    OS.ShellOpen("https://mithara.online/Marketplace");
            };
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
            RequestList();
    }

    public override void _ExitTree()
    {
        if (_net != null)
        {
            _net.OnMarketplaceListResult -= OnListings;
            _net.OnMarketplaceActionResult -= OnActionResult;
        }
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
