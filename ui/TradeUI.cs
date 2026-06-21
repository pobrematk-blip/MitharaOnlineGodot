using Godot;
using System.Collections.Generic;
using System.Linq;
#nullable enable annotations

public partial class TradeUI : Control
{
    private static readonly Vector2 TradeWindowSize = new(400, 262);
    private static readonly Vector2 TradeSlotSize = new(42, 42);
    private Panel _window;
    private Label _titleLabel;
    private Label _myStatus;
    private Label _partnerStatus;
    private Button _confirmBtn;
    private Button _cancelBtn;
    private Panel _itemPopup;
    private VBoxContainer _itemPopupList;

    private ulong _partnerId;
    private string _partnerName = "";
    private GameNetwork? _net;

    private Godot.Collections.Dictionary _myOffers = new(); // tradeSlot -> Dictionary
    private Godot.Collections.Dictionary _partnerOffers = new();
    private bool _myConfirmed;
    private bool _partnerConfirmed;

    private Panel[] _mySlots = new Panel[9];
    private Panel[] _partnerSlots = new Panel[9];
    private Label[] _mySlotLabels = new Label[9];
    private Label[] _partnerSlotLabels = new Label[9];

    private Godot.Collections.Array<Godot.Collections.Dictionary> _cachedInventory = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        var overlay = new ColorRect();
        overlay.Color = new Color(0, 0, 0, 0.5f);
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.MouseFilter = MouseFilterEnum.Pass;
        AddChild(overlay);

        _window = new Panel();
        _window.CustomMinimumSize = TradeWindowSize;
        _window.Size = TradeWindowSize;
        var winStyle = new StyleBoxFlat();
        winStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        winStyle.SetCornerRadiusAll(8);
        winStyle.BorderColor = new Color(0.3f, 0.3f, 0.45f, 0.8f);
        winStyle.SetBorderWidthAll(1);
        _window.AddThemeStyleboxOverride("panel", winStyle);

        var winMargin = new MarginContainer();
        winMargin.AddThemeConstantOverride("margin_left", 8);
        winMargin.AddThemeConstantOverride("margin_top", 8);
        winMargin.AddThemeConstantOverride("margin_right", 8);
        winMargin.AddThemeConstantOverride("margin_bottom", 8);
        winMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        _window.AddChild(winMargin);

        var winVbox = new VBoxContainer();
        winVbox.AddThemeConstantOverride("separation", 6);
        winMargin.AddChild(winVbox);

        var titleBar = new HBoxContainer();
        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 13);
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 1, 0.95f));
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleBar.AddChild(_titleLabel);

        var closeBtn = new Button { Text = "X", Flat = true };
        closeBtn.Pressed += () => CancelTrade();
        titleBar.AddChild(closeBtn);
        winVbox.AddChild(titleBar);

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        content.SizeFlagsVertical = SizeFlags.ExpandFill;

        content.AddChild(CriarLado(true));
        content.AddChild(CriarLado(false));

        winVbox.AddChild(content);

        var bottom = new HBoxContainer();
        bottom.Alignment = BoxContainer.AlignmentMode.End;
        bottom.AddThemeConstantOverride("separation", 8);

        _confirmBtn = new Button { Text = "Confirmar Troca", CustomMinimumSize = new Vector2(132, 30) };
        _confirmBtn.Pressed += () => ConfirmTrade();
        bottom.AddChild(_confirmBtn);

        _cancelBtn = new Button { Text = "Cancelar", CustomMinimumSize = new Vector2(86, 30) };
        _cancelBtn.Pressed += () => CancelTrade();
        bottom.AddChild(_cancelBtn);

        winVbox.AddChild(bottom);

        AddChild(_window);

        _itemPopup = new Panel();
        _itemPopup.CustomMinimumSize = new Vector2(240, 180);
        _itemPopup.Visible = false;
        var popupStyle = new StyleBoxFlat();
        popupStyle.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        popupStyle.SetCornerRadiusAll(6);
        popupStyle.BorderColor = new Color(0.3f, 0.3f, 0.45f, 0.8f);
        popupStyle.SetBorderWidthAll(1);
        _itemPopup.AddThemeStyleboxOverride("panel", popupStyle);

        var popupVbox = new VBoxContainer();
        popupVbox.AddThemeConstantOverride("separation", 4);
        _itemPopup.AddChild(popupVbox);

        var popupTitle = new Label { Text = "Selecione um item do inventário" };
        popupTitle.AddThemeFontSizeOverride("font_size", 11);
        popupTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 1, 0.9f));
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
            _net.OnTradePartnerConfirm += OnTradePartnerConfirm;
            _net.OnTradeEnd += OnTradeEnd;
            _net.OnInventoryData += OnInventoryData;
        }

        Visible = false;
    }

    private Panel CriarLado(bool isMine)
    {
        var panel = new Panel();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(174, 0);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0, 0, 0, 0.25f);
        style.SetCornerRadiusAll(4);
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        panel.AddChild(vbox);

        var header = new Label();
        header.AddThemeFontSizeOverride("font_size", 10);
        header.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 1, 0.85f));
        vbox.AddChild(header);

        var grid = new GridContainer();
        grid.Columns = 3;
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);

        var slots = isMine ? _mySlots : _partnerSlots;
        var labels = isMine ? _mySlotLabels : _partnerSlotLabels;

        for (int i = 0; i < 9; i++)
        {
            int slotIdx = i;
            var slotPanel = new Panel();
            slotPanel.CustomMinimumSize = TradeSlotSize;
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0, 0, 0, 0.35f);
            slotStyle.SetCornerRadiusAll(4);
            slotStyle.BorderColor = new Color(0.2f, 0.2f, 0.3f, 0.4f);
            slotStyle.SetBorderWidthAll(1);
            slotPanel.AddThemeStyleboxOverride("panel", slotStyle);

            var slotLabel = new Label();
            slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
            slotLabel.VerticalAlignment = VerticalAlignment.Center;
            slotLabel.AddThemeFontSizeOverride("font_size", 7);
            slotLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            slotLabel.ClipText = true;
            slotLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.8f, 0.6f));
            slotLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            slotPanel.AddChild(slotLabel);

            if (isMine)
            {
                slotPanel.MouseFilter = MouseFilterEnum.Pass;
                slotPanel.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                        OnMySlotClicked(slotIdx);
                };
            }

            slots[slotIdx] = slotPanel;
            labels[slotIdx] = slotLabel;
            grid.AddChild(slotPanel);
        }
        vbox.AddChild(grid);

        var statusLabel = new Label();
        statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        statusLabel.AddThemeFontSizeOverride("font_size", 9);
        vbox.AddChild(statusLabel);

        if (isMine)
        {
            header.Text = "Seus Itens";
            _myStatus = statusLabel;
        }
        else
        {
            _partnerStatus = statusLabel;
        }

        return panel;
    }

    private void OnTradeStart(ulong partnerId, string partnerName)
    {
        _partnerId = partnerId;
        _partnerName = partnerName;
        _myOffers.Clear();
        _partnerOffers.Clear();
        _myConfirmed = false;
        _partnerConfirmed = false;

        _titleLabel.Text = $"Troca com {partnerName}";

        for (int i = 0; i < 9; i++)
        {
            _mySlotLabels[i].Text = "";
            _partnerSlotLabels[i].Text = "";
            AtualizarEstiloSlot(_mySlots[i], false);
            AtualizarEstiloSlot(_partnerSlots[i], false);
        }

        _myStatus.Text = "Aguardando...";
        _partnerStatus.Text = "Aguardando...";
        _confirmBtn.Disabled = false;
        _confirmBtn.Text = "Confirmar Troca";

        CentralizarJanela();
        Visible = true;
    }

    private void OnTradeOfferUpdate(ulong playerSide, Godot.Collections.Array<Godot.Collections.Dictionary> offers)
    {
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

        for (int i = 0; i < 9; i++)
        {
            if (dict.ContainsKey(i))
            {
                var entry = (Godot.Collections.Dictionary)dict[i];
                int itemId = (int)entry["item_id"];
                int qty = (int)entry["quantity"];
                string itemName = ObterNomeItem(itemId);
                labels[i].Text = $"{itemName}\n{qty}x";
                AtualizarEstiloSlot(slots[i], true);
            }
            else
            {
                labels[i].Text = "";
                AtualizarEstiloSlot(slots[i], false);
            }
        }

        if (isMySide && _myConfirmed)
        {
            _myConfirmed = false;
            _myStatus.Text = "Aguardando...";
        }
    }

    private void OnTradePartnerConfirm(ulong playerSide, bool confirmed)
    {
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
        Visible = false;
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
        _cachedInventory = items;
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
                    var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
                    net?.SendTradeUpdateOffer(capturedSlot, 1);
                };
                _itemPopupList.AddChild(btn);
            }
        }

        _itemPopup.Visible = true;
        _itemPopup.Position = new Vector2(
            (GetViewportRect().Size.X - 240) / 2,
            (GetViewportRect().Size.Y - 180) / 2);
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
        var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        net?.SendTradeCancel();
    }

    private void AtualizarEstiloSlot(Panel slot, bool occupied)
    {
        var style = new StyleBoxFlat();
        style.BgColor = occupied ? new Color(0.1f, 0.15f, 0.25f, 0.5f) : new Color(0, 0, 0, 0.35f);
        style.SetCornerRadiusAll(4);
        style.BorderColor = occupied ? new Color(0.3f, 0.5f, 0.8f, 0.6f) : new Color(0.2f, 0.2f, 0.3f, 0.4f);
        style.SetBorderWidthAll(1);
        slot.AddThemeStyleboxOverride("panel", style);
    }

    private void CentralizarJanela()
    {
        var vp = GetViewportRect();
        _window.Position = new Vector2(
            (vp.Size.X - TradeWindowSize.X) / 2,
            (vp.Size.Y - TradeWindowSize.Y) / 2);
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
}
