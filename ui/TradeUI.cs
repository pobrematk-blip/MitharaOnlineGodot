using Godot;
using System.Collections.Generic;
using System.Linq;
#nullable enable annotations

public partial class TradeUI : Control
{
    private static readonly Vector2 TradeWindowSize = new(430, 352);
    private static readonly Vector2 TradeSlotSize = new(46, 46);
    private Panel _window;
    private Label _titleLabel;
    private Label _myStatus;
    private Label _partnerStatus;
    private Button _confirmBtn;
    private Button _cancelBtn;
    private SpinBox _myGoldSpin;
    private Label _myGoldOfferLabel;
    private Label _partnerGoldOfferLabel;
    private Panel _itemPopup;
    private VBoxContainer _itemPopupList;

    private ulong _partnerId;
    private string _partnerName = "";
    private GameNetwork? _net;

    private Godot.Collections.Dictionary _myOffers = new(); // tradeSlot -> Dictionary
    private Godot.Collections.Dictionary _partnerOffers = new();
    private bool _myConfirmed;
    private bool _partnerConfirmed;
    private int _myGoldOffer;
    private int _partnerGoldOffer;

    private Panel[] _mySlots = new Panel[9];
    private Panel[] _partnerSlots = new Panel[9];
    private Label[] _mySlotLabels = new Label[9];
    private Label[] _partnerSlotLabels = new Label[9];
    private TextureRect[] _mySlotIcons = new TextureRect[9];
    private TextureRect[] _partnerSlotIcons = new TextureRect[9];
    private Label[] _mySlotQuantities = new Label[9];
    private Label[] _partnerSlotQuantities = new Label[9];
    private bool _arrastandoJanela;
    private Vector2 _pontoCliqueOriginal;
    private bool _disposed;

    private Godot.Collections.Array<Godot.Collections.Dictionary> _cachedInventory = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _window = new Panel();
        _window.CustomMinimumSize = TradeWindowSize;
        _window.Size = TradeWindowSize;
        _window.AddThemeStyleboxOverride("panel", MitharaUiTheme.Panel(0.95f));

        var winMargin = new MarginContainer();
        winMargin.AddThemeConstantOverride("margin_left", 10);
        winMargin.AddThemeConstantOverride("margin_top", 8);
        winMargin.AddThemeConstantOverride("margin_right", 10);
        winMargin.AddThemeConstantOverride("margin_bottom", 10);
        winMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        _window.AddChild(winMargin);

        var winVbox = new VBoxContainer();
        winVbox.AddThemeConstantOverride("separation", 7);
        winMargin.AddChild(winVbox);

        var titleBar = new HBoxContainer();
        titleBar.MouseFilter = MouseFilterEnum.Stop;
        titleBar.GuiInput += OnTitleBarGuiInput;
        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 13);
        _titleLabel.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        titleBar.AddChild(_titleLabel);

        var closeBtn = new Button { Text = "X", Flat = true, CustomMinimumSize = new Vector2(28, 24) };
        closeBtn.Pressed += () => CancelTrade();
        titleBar.AddChild(closeBtn);
        winVbox.AddChild(titleBar);

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        content.SizeFlagsVertical = SizeFlags.ExpandFill;

        content.AddChild(CriarLado(true));
        content.AddChild(CriarLado(false));

        winVbox.AddChild(content);

        var bottom = new HBoxContainer();
        bottom.Alignment = BoxContainer.AlignmentMode.End;
        bottom.AddThemeConstantOverride("separation", 8);

        _confirmBtn = new Button { Text = "Confirmar Troca", CustomMinimumSize = new Vector2(138, 30) };
        _confirmBtn.Pressed += () => ConfirmTrade();
        bottom.AddChild(_confirmBtn);

        _cancelBtn = new Button { Text = "Cancelar", CustomMinimumSize = new Vector2(86, 30) };
        _cancelBtn.Pressed += () => CancelTrade();
        bottom.AddChild(_cancelBtn);

        winVbox.AddChild(bottom);

        AddChild(_window);

        _itemPopup = new Panel();
        _itemPopup.CustomMinimumSize = new Vector2(260, 196);
        _itemPopup.Visible = false;
        _itemPopup.AddThemeStyleboxOverride("panel", MitharaUiTheme.Panel(0.95f));

        var popupVbox = new VBoxContainer();
        popupVbox.AddThemeConstantOverride("separation", 4);
        _itemPopup.AddChild(popupVbox);

        var popupTitle = new Label { Text = "Selecione um item do inventário" };
        popupTitle.AddThemeFontSizeOverride("font_size", 11);
        popupTitle.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        popupVbox.AddChild(popupTitle);

        var popupScroll = new ScrollContainer();
        popupScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _itemPopupList = new VBoxContainer();
        _itemPopupList.AddThemeConstantOverride("separation", 2);
        _itemPopupList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        popupScroll.AddChild(_itemPopupList);
        popupVbox.AddChild(popupScroll);

        var popupClose = new Button { Text = "Fechar" };
        popupClose.Pressed += () => _itemPopup.Visible = false;
        popupVbox.AddChild(popupClose);

        AddChild(_itemPopup);

        _net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (_net != null)
        {
            _net.OnTradeStart += OnTradeStart;
            _net.OnTradeOfferUpdate += OnTradeOfferUpdate;
            _net.OnTradeGoldUpdate += OnTradeGoldUpdate;
            _net.OnTradePartnerConfirm += OnTradePartnerConfirm;
            _net.OnTradeEnd += OnTradeEnd;
            _net.OnInventoryData += OnInventoryData;
            _net.OnGoldUpdate += OnGoldUpdate;
            if (_net.PendingInventoryData != null)
                _cachedInventory = _net.PendingInventoryData;
        }

        Visible = false;

        if (_net != null && _net.PendingTradeActive)
            CallDeferred(nameof(OpenPendingTrade));
    }

    private void OpenPendingTrade()
    {
        if (_net == null || !_net.PendingTradeActive)
            return;

        OnTradeStart(_net.PendingTradePartnerId, _net.PendingTradePartnerName);
    }

    private Panel CriarLado(bool isMine)
    {
        var panel = new Panel();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(168, 0);

        panel.AddThemeStyleboxOverride("panel", MitharaUiTheme.Inner(0.68f));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);
        panel.AddChild(vbox);

        var header = new Label();
        header.AddThemeFontSizeOverride("font_size", 10);
        header.AddThemeColorOverride("font_color", MitharaUiTheme.Text);
        vbox.AddChild(header);

        var grid = new GridContainer();
        grid.Columns = 3;
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 5);
        grid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        var slots = isMine ? _mySlots : _partnerSlots;
        var labels = isMine ? _mySlotLabels : _partnerSlotLabels;
        var icons = isMine ? _mySlotIcons : _partnerSlotIcons;
        var quantities = isMine ? _mySlotQuantities : _partnerSlotQuantities;

        for (int i = 0; i < 9; i++)
        {
            int slotIdx = i;
            var slotPanel = isMine ? new TradeDropSlot { TradeSlot = slotIdx } : new Panel();
            slotPanel.CustomMinimumSize = TradeSlotSize;
            slotPanel.Size = TradeSlotSize;
            slotPanel.AddThemeStyleboxOverride("panel", MitharaUiTheme.Slot());

            var slotLabel = new Label();
            slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
            slotLabel.VerticalAlignment = VerticalAlignment.Center;
            slotLabel.AddThemeFontSizeOverride("font_size", 7);
            slotLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            slotLabel.ClipText = true;
            slotLabel.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
            slotLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            slotLabel.MouseFilter = MouseFilterEnum.Ignore;
            slotPanel.AddChild(slotLabel);

            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                Size = new Vector2(36, 36),
                Position = new Vector2(5, 3),
                Visible = false
            };
            slotPanel.AddChild(icon);

            var qtyLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            qtyLabel.AddThemeFontSizeOverride("font_size", 9);
            qtyLabel.AddThemeColorOverride("font_color", Colors.White);
            qtyLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
            qtyLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            qtyLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            qtyLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            slotPanel.AddChild(qtyLabel);

            if (isMine)
            {
                slotPanel.MouseFilter = MouseFilterEnum.Stop;
                if (slotPanel is TradeDropSlot dropSlot)
                    dropSlot.OnTradeItemDropped += OnTradeItemDropped;

                slotPanel.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        OnMySlotClicked(slotIdx);
                };
            }

            slots[slotIdx] = slotPanel;
            labels[slotIdx] = slotLabel;
            icons[slotIdx] = icon;
            quantities[slotIdx] = qtyLabel;
            grid.AddChild(slotPanel);
        }
        vbox.AddChild(grid);

        var statusLabel = new Label();
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        statusLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(statusLabel);

        var goldBox = new VBoxContainer();
        goldBox.AddThemeConstantOverride("separation", 3);
        var goldTitle = new Label
        {
            Text = "Ouro",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        goldTitle.AddThemeFontSizeOverride("font_size", 10);
        goldTitle.AddThemeColorOverride("font_color", Color.FromHtml("#f5d76e"));
        goldBox.AddChild(goldTitle);

        if (isMine)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            _myGoldSpin = new SpinBox
            {
                MinValue = 0,
                MaxValue = _net?.Gold ?? 0,
                Step = 1,
                Rounded = true,
                CustomMinimumSize = new Vector2(96, 28),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            row.AddChild(_myGoldSpin);

            var setGoldBtn = new Button { Text = "OK", CustomMinimumSize = new Vector2(42, 28) };
            setGoldBtn.Pressed += SendGoldOfferFromUi;
            row.AddChild(setGoldBtn);
            goldBox.AddChild(row);

            _myGoldOfferLabel = new Label
            {
                Text = "Ofertado: 0",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _myGoldOfferLabel.AddThemeFontSizeOverride("font_size", 9);
            _myGoldOfferLabel.AddThemeColorOverride("font_color", MitharaUiTheme.TextMuted);
            goldBox.AddChild(_myGoldOfferLabel);
        }
        else
        {
            _partnerGoldOfferLabel = new Label
            {
                Text = "Ofertado: 0",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _partnerGoldOfferLabel.AddThemeFontSizeOverride("font_size", 10);
            _partnerGoldOfferLabel.AddThemeColorOverride("font_color", Color.FromHtml("#f5d76e"));
            goldBox.AddChild(_partnerGoldOfferLabel);
        }

        vbox.AddChild(goldBox);

        if (isMine)
        {
            header.Text = "Seus itens";
            _myStatus = statusLabel;
        }
        else
        {
            header.Text = "Itens do parceiro";
            _partnerStatus = statusLabel;
        }

        return panel;
    }

    private void OnTradeStart(ulong partnerId, string partnerName)
    {
        if (_disposed || !IsInsideTree())
            return;

        _partnerId = partnerId;
        _partnerName = partnerName;
        _myOffers.Clear();
        _partnerOffers.Clear();
        _myConfirmed = false;
        _partnerConfirmed = false;
        _myGoldOffer = 0;
        _partnerGoldOffer = 0;
        _net?.SendInventoryRequest();

        _titleLabel.Text = $"Troca com {partnerName}";

        for (int i = 0; i < 9; i++)
        {
            _mySlotLabels[i].Text = "";
            _partnerSlotLabels[i].Text = "";
            LimparVisualSlot(_mySlots[i], _mySlotLabels[i], _mySlotIcons[i], _mySlotQuantities[i]);
            LimparVisualSlot(_partnerSlots[i], _partnerSlotLabels[i], _partnerSlotIcons[i], _partnerSlotQuantities[i]);
            AtualizarEstiloSlot(_mySlots[i], false);
            AtualizarEstiloSlot(_partnerSlots[i], false);
            if (_mySlots[i] is TradeDropSlot dropSlot)
                dropSlot.Locked = false;
        }

        _myStatus.Text = "Aguardando...";
        _partnerStatus.Text = "Aguardando...";
        _confirmBtn.Disabled = false;
        _confirmBtn.Text = "Confirmar Troca";
        AtualizarGoldUi();

        CentralizarJanela();
        Visible = true;
        MoveToFront();
        _window.MoveToFront();
        CallDeferred(nameof(AbrirInventarioParaTroca));
    }

    private void OnTradeOfferUpdate(ulong playerSide, Godot.Collections.Array<Godot.Collections.Dictionary> offers)
    {
        if (_disposed || !IsInsideTree())
            return;

        bool isMySide = playerSide == (_net?.LocalPlayerId ?? 0);
        var dict = isMySide ? _myOffers : _partnerOffers;
        dict.Clear();

        foreach (var o in offers)
        {
            int slot = (int)o["slot"];
            dict[slot] = o;
        }

        var slots = isMySide ? _mySlots : _partnerSlots;
        var labels = isMySide ? _mySlotLabels : _partnerSlotLabels;
        var icons = isMySide ? _mySlotIcons : _partnerSlotIcons;
        var quantities = isMySide ? _mySlotQuantities : _partnerSlotQuantities;

        for (int i = 0; i < 9; i++)
        {
            if (dict.ContainsKey(i))
            {
                var entry = (Godot.Collections.Dictionary)dict[i];
                int itemId = (int)entry["item_id"];
                int qty = (int)entry["quantity"];
                AtualizarVisualSlot(slots[i], labels[i], icons[i], quantities[i], itemId, qty);
                AtualizarEstiloSlot(slots[i], true);
            }
            else
            {
                LimparVisualSlot(slots[i], labels[i], icons[i], quantities[i]);
                AtualizarEstiloSlot(slots[i], false);
            }
        }

        if (isMySide && _myConfirmed)
        {
            _myConfirmed = false;
            _myStatus.Text = "Aguardando...";
        }
    }

    private void OnTradeGoldUpdate(ulong playerSide, int gold)
    {
        if (_disposed || !IsInsideTree())
            return;

        bool isMySide = playerSide == (_net?.LocalPlayerId ?? 0);
        if (isMySide)
        {
            _myGoldOffer = Mathf.Max(0, gold);
            if (_myConfirmed)
            {
                _myConfirmed = false;
                _myStatus.Text = "Aguardando...";
            }
        }
        else
        {
            _partnerGoldOffer = Mathf.Max(0, gold);
        }

        AtualizarGoldUi();
    }

    private void OnGoldUpdate(int gold)
    {
        if (IsControlAlive(_myGoldSpin))
            _myGoldSpin.MaxValue = Mathf.Max(0, gold);
    }

    private void OnTradePartnerConfirm(ulong playerSide, bool confirmed)
    {
        if (_disposed || !IsInsideTree())
            return;

        bool isMySide = playerSide == (_net?.LocalPlayerId ?? 0);
        if (isMySide)
        {
            _myConfirmed = confirmed;
            _myStatus.Text = confirmed ? "Confirmado!" : "Aguardando...";
        }
        else
        {
            _partnerConfirmed = confirmed;
            _partnerStatus.Text = confirmed ? "Confirmado!" : "Aguardando...";
        }
    }

    private void OnTradeEnd(bool success)
    {
        if (_disposed || !IsInsideTree())
            return;

        Visible = false;
        _net?.ClearPendingTrade();
        if (success)
        {
            var popup = new AcceptDialog();
            popup.DialogText = "Troca realizada com sucesso!";
            popup.Title = "Troca";
            AddChild(popup);
            popup.PopupCentered();
        }
    }

    private void OnInventoryData(Godot.Collections.Array<Godot.Collections.Dictionary> items, Godot.Collections.Array<Godot.Collections.Dictionary> equipment)
    {
        if (_disposed)
            return;

        _cachedInventory = items;
    }

    public bool TryOfferInventorySlot(int inventorySlot, int quantity = 1)
    {
        if (!Visible || _myConfirmed || inventorySlot < 0)
            return false;

        _net?.SendTradeUpdateOffer(inventorySlot, Mathf.Max(1, quantity));
        return true;
    }

    private void OnMySlotClicked(int slotIdx)
    {
        if (_myConfirmed) return;

        if (_myOffers.ContainsKey(slotIdx))
        {
            var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            net?.SendTradeRemoveOffer(slotIdx);
        }
        else
        {
            MostrarSelecaoItem();
        }
    }

    private void MostrarSelecaoItem()
    {
        _net?.SendInventoryRequest();

        foreach (var child in _itemPopupList.GetChildren())
            child.QueueFree();

        if (_cachedInventory.Count == 0)
        {
            var lbl = new Label { Text = "Inventário vazio." };
            lbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f, 0.7f));
            _itemPopupList.AddChild(lbl);
        }
        else
        {
            foreach (var item in _cachedInventory)
            {
                int itemId = (int)item["item_id"];
                int qty = (int)item["quantity"];
                int slot = (int)item["slot"];

                if (qty <= 0) continue;

                var btn = new Button { Flat = true, Text = $"{ObterNomeItem(itemId)} x{qty} (Slot {slot})" };
                btn.CustomMinimumSize = new Vector2(0, 24);
                int capturedSlot = slot;
                int capturedQty = qty;
                btn.Pressed += () =>
                {
                    _itemPopup.Visible = false;
                    TryOfferInventorySlot(capturedSlot, capturedQty);
                };
                _itemPopupList.AddChild(btn);
            }
        }

        _itemPopup.Visible = true;
        _itemPopup.Position = new Vector2(
            (GetViewportRect().Size.X - 260) / 2,
            (GetViewportRect().Size.Y - 196) / 2);
        _itemPopup.MoveToFront();
    }

    private void OnTradeItemDropped(int tradeSlot, int inventorySlot, int quantity)
    {
        if (_myConfirmed)
            return;

        if (_myOffers.ContainsKey(tradeSlot))
            _net?.SendTradeRemoveOffer(tradeSlot);

        _net?.SendTradeUpdateOffer(inventorySlot, Mathf.Max(1, quantity));
    }

    private void SendGoldOfferFromUi()
    {
        if (_myConfirmed || _myGoldSpin == null)
            return;

        int gold = Mathf.Max(0, (int)_myGoldSpin.Value);
        _net?.SendTradeUpdateGold(gold);
    }

    private void AtualizarGoldUi()
    {
        if (IsControlAlive(_myGoldSpin))
        {
            _myGoldSpin.MaxValue = Mathf.Max(0, _net?.Gold ?? 0);
            _myGoldSpin.Value = Mathf.Min(_myGoldOffer, (int)_myGoldSpin.MaxValue);
            _myGoldSpin.Editable = !_myConfirmed;
        }

        if (IsControlAlive(_myGoldOfferLabel))
            _myGoldOfferLabel.Text = $"Ofertado: {_myGoldOffer}";
        if (IsControlAlive(_partnerGoldOfferLabel))
            _partnerGoldOfferLabel.Text = $"Ofertado: {_partnerGoldOffer}";
    }

    private void ConfirmTrade()
    {
        if (_myConfirmed) return;
        _myConfirmed = true;
        _myStatus.Text = "Confirmado!";
        _confirmBtn.Disabled = true;
        _confirmBtn.Text = "Aguardando parceiro...";
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendTradeConfirm();
    }

    private void CancelTrade()
    {
        Visible = false;
        _net?.ClearPendingTrade();
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendTradeCancel();
    }

    private void AbrirInventarioParaTroca()
    {
        var tree = GetTree();
        var inv = tree?.Root?.FindChild("InventarioUI", true, false) as InventarioUI
            ?? tree?.CurrentScene?.FindChild("InventarioUI", true, false) as InventarioUI;
        if (inv == null)
        {
            GD.PrintErr("[TRADE] InventarioUI nao encontrado para abrir junto com a troca.");
            return;
        }

        inv.AbrirPainel();
        var vp = GetViewportRect();
        _window.Position = new Vector2(Mathf.Max(8, vp.Size.X - TradeWindowSize.X - 24), Mathf.Max(8, (vp.Size.Y - TradeWindowSize.Y) / 2));
        inv.PosicionarPainel(new Vector2(24, Mathf.Max(8, (vp.Size.Y - 420) / 2)));
    }

    private void AtualizarEstiloSlot(Panel slot, bool occupied)
    {
        slot.AddThemeStyleboxOverride("panel", MitharaUiTheme.Slot(occupied));
        if (slot is TradeDropSlot dropSlot)
            dropSlot.Locked = _myConfirmed;
    }

    private void CentralizarJanela()
    {
        var vp = GetViewportRect();
        _window.Position = new Vector2(
            (vp.Size.X - TradeWindowSize.X) / 2,
            (vp.Size.Y - TradeWindowSize.Y) / 2);
    }

    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            _arrastandoJanela = mouseEvent.Pressed;
            if (mouseEvent.Pressed)
            {
                _pontoCliqueOriginal = mouseEvent.Position;
                MoveToFront();
                _window.MoveToFront();
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _arrastandoJanela)
        {
            _window.Position += mouseMotion.Position - _pontoCliqueOriginal;
            ResponsiveUI.ClampInsideViewport(_window, 4f);
        }
    }

    private static string ObterNomeItem(int itemId)
    {
        return itemId switch
        {
            0 => "Ouro",
            1 => "Poção de Vida Pequena",
            2 => "Poção de Mana Pequena",
            101 => "Pergaminho de Ressurreição",
            103 => "Pergaminho VIP 7 Dias",
            104 => "Pergaminho VIP 15 Dias",
            105 => "Pergaminho VIP 30 Dias",
            106 => "Pergaminho VIP Trial",
            >= 1001 and <= 1021 => "Arco",
            >= 1051 and <= 1071 => "Aljava",
            >= 2001 and <= 2021 => "Adaga",
            >= 2051 and <= 2071 => "Adaga Secundária",
            >= 3001 and <= 3021 => "Machado",
            >= 3051 and <= 3071 => "Bumerangue",
            >= 4001 and <= 4021 => "Espada",
            >= 4051 and <= 4071 => "Escudo",
            >= 5001 and <= 5021 => "Cajado",
            >= 5051 and <= 5071 => "Orbe",
            >= 6001 and <= 6021 => "Martelo",
            >= 6051 and <= 6071 => "Escudo Sagrado",
            _ => $"Item #{itemId}",
        };
    }

    private void AtualizarVisualSlot(Panel slot, Label fallbackLabel, TextureRect icon, Label qtyLabel, int itemId, int quantity)
    {
        string itemName = ObterNomeItem(itemId);
        ItemResource? item = _net?.ItemDB?.GetItem(itemId);
        Texture2D? texture = item?.Icone;
        if (item != null)
            itemName = item.Nome;

        if (IsControlAlive(icon) && texture != null)
        {
            icon.Texture = texture;
            icon.Visible = true;
            fallbackLabel.Text = "";
        }
        else
        {
            if (IsControlAlive(icon))
            {
                icon.Texture = null;
                icon.Visible = false;
            }
            fallbackLabel.Text = $"{itemName}\n{quantity}x";
        }

        if (IsControlAlive(qtyLabel))
        {
            qtyLabel.Text = quantity > 1 ? quantity.ToString() : "";
            qtyLabel.Visible = quantity > 1;
        }

        slot.TooltipText = $"{itemName}\nQuantidade: {quantity}";
    }

    private static void LimparVisualSlot(Panel slot, Label fallbackLabel, TextureRect icon, Label qtyLabel)
    {
        if (GodotObject.IsInstanceValid(fallbackLabel))
            fallbackLabel.Text = "";
        if (GodotObject.IsInstanceValid(icon))
        {
            icon.Texture = null;
            icon.Visible = false;
        }
        if (GodotObject.IsInstanceValid(qtyLabel))
        {
            qtyLabel.Text = "";
            qtyLabel.Visible = false;
        }
        if (GodotObject.IsInstanceValid(slot))
            slot.TooltipText = "";
    }

    public override void _ExitTree()
    {
        _disposed = true;

        if (_net != null)
        {
            _net.OnTradeStart -= OnTradeStart;
            _net.OnTradeOfferUpdate -= OnTradeOfferUpdate;
            _net.OnTradeGoldUpdate -= OnTradeGoldUpdate;
            _net.OnTradePartnerConfirm -= OnTradePartnerConfirm;
            _net.OnTradeEnd -= OnTradeEnd;
            _net.OnInventoryData -= OnInventoryData;
            _net.OnGoldUpdate -= OnGoldUpdate;
        }

        _net = null;
    }

    private static bool IsControlAlive(Control control)
    {
        return control != null && GodotObject.IsInstanceValid(control) && !control.IsQueuedForDeletion();
    }
}
