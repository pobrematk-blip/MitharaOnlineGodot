using Godot;

public partial class SlotUI : Control
{
    private enum TipoContainerUi { Nenhum, Inventario, Banco }

    private TextureRect _icone;
    private Label _quantidadeTexto;
    private Texture2D _texturaFundoPadrao;

    public SlotInventario SlotInterno { get; private set; }

    public override void _Ready()
    {
        _icone = GetNode<TextureRect>("Icone");
        _quantidadeTexto = GetNode<Label>("Quantidade");

        if (_icone != null)
            _texturaFundoPadrao = _icone.Texture;

        MouseEntered += OnMouseEnteredSlot;
        MouseExited += OnMouseExitedSlot;
    }

    private TipoContainerUi ObterContainerUi()
    {
        Node n = this;
        while (n != null)
        {
            if (n is BancoUI) return TipoContainerUi.Banco;
            if (n is InventarioUI) return TipoContainerUi.Inventario;
            n = n.GetParent();
        }
        return TipoContainerUi.Nenhum;
    }

    private bool EhSlotBolsaInventario => Name.ToString().StartsWith("SlotBolsa_") && !Name.ToString().StartsWith("SlotBolsaBanco_");
    private bool EhSlotBolsaBanco => Name.ToString().StartsWith("SlotBolsaBanco_");
    private bool EhQualquerSlotBolsa => EhSlotBolsaInventario || EhSlotBolsaBanco;

    private int ObterIndiceBolsa()
    {
        var partes = Name.ToString().Split('_');
        if (partes.Length < 2) return -1;
        return int.Parse(partes[^1]);
    }

    private InventarioComponent ObterInventario()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        return player?.FindChild("InventarioComponent", true, false) as InventarioComponent;
    }

    private BancoComponent ObterBanco()
    {
        var player = GetTree().CurrentScene.FindChild("Player", true, false);
        return player?.FindChild("BancoComponent", true, false) as BancoComponent;
    }

    private void OnMouseEnteredSlot()
    {
        if (EhQualquerSlotBolsa && SlotInterno?.Item != null && SlotInterno.Item.EhBolsa)
            _icone.SelfModulate = new Color(1.2f, 1.2f, 0.8f, 1);
    }

    private void OnMouseExitedSlot()
    {
        if (SlotInterno?.Item != null)
            _icone.SelfModulate = new Color(1, 1, 1, 1);
    }

    public void AtualizarSlot(SlotInventario slotLogico)
    {
        SlotInterno = slotLogico;

        if (_icone == null || _quantidadeTexto == null) return;

        Visible = true;
        _icone.Visible = true;

        if (slotLogico == null || slotLogico.Item == null)
        {
            if (_texturaFundoPadrao != null)
            {
                _icone.Texture = _texturaFundoPadrao;
                _icone.SelfModulate = new Color(1, 1, 1, 1);
            }
            else
            {
                _icone.Texture = GD.Load<Texture2D>("res://icon.svg");
                _icone.SelfModulate = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            }

            _quantidadeTexto.Text = "";
            _quantidadeTexto.Visible = false;
            TooltipText = "";
        }
        else
        {
            _icone.Texture = slotLogico.Item.Icone;
            _icone.SelfModulate = new Color(1, 1, 1, 1);

            if (slotLogico.Quantidade > 1)
            {
                _quantidadeTexto.Text = slotLogico.Quantidade.ToString();
                _quantidadeTexto.Visible = true;
            }
            else
            {
                _quantidadeTexto.Text = "";
                _quantidadeTexto.Visible = false;
            }

            if (EhQualquerSlotBolsa && slotLogico.Item.EhBolsa)
                TooltipText = $"🎒 {slotLogico.Item.Nome}\n+{slotLogico.Item.SlotsAdicionais} slots no {(EhSlotBolsaBanco ? "banco" : "inventário")}";
        }
    }

    public override Variant _GetDragData(Vector2 position)
    {
        if (SlotInterno == null || SlotInterno.Item == null) return default;

        var preview = new TextureRect
        {
            Texture = SlotInterno.Item.Icone,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = new Vector2(40, 40),
            Modulate = new Color(1, 1, 1, 0.7f)
        };

        SetDragPreview(preview);
        return this;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent || !mouseEvent.Pressed
            || mouseEvent.ButtonIndex != MouseButton.Right)
            return;

        if (!EhQualquerSlotBolsa || SlotInterno?.Item == null) return;

        int indexBolsa = ObterIndiceBolsa();
        if (indexBolsa < 0) return;

        if (EhSlotBolsaInventario)
            RemoverBolsaParaInventario(indexBolsa);
        else if (EhSlotBolsaBanco)
            RemoverBolsaParaBanco(indexBolsa);

        GetViewport().SetInputAsHandled();
    }

    private void RemoverBolsaParaInventario(int indexBolsa)
    {
        var inventario = ObterInventario();
        if (inventario == null) return;

        foreach (var slot in inventario.Slots)
        {
            if (slot.Item != null) continue;
            slot.Item = SlotInterno.Item;
            slot.Quantidade = SlotInterno.Quantidade;
            inventario.DesequiparBolsaNoSlot(indexBolsa);
            SlotInterno.Item = null;
            SlotInterno.Quantidade = 0;
            inventario.NotificarMudancaExterna();
            return;
        }

        GD.Print("[INVENTÁRIO] ❌ Sem espaço para retirar a bolsa!");
    }

    private void RemoverBolsaParaBanco(int indexBolsa)
    {
        var banco = ObterBanco();
        if (banco == null) return;

        foreach (var slot in banco.Slots)
        {
            if (slot.Item != null) continue;
            slot.Item = SlotInterno.Item;
            slot.Quantidade = SlotInterno.Quantidade;
            banco.DesequiparBolsaNoSlot(indexBolsa);
            SlotInterno.Item = null;
            SlotInterno.Quantidade = 0;
            banco.NotificarMudancaExterna();
            return;
        }

        GD.Print("[BANCO] ❌ Sem espaço para retirar a bolsa!");
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SlotEquipamentoUI)
            return SlotInterno != null && SlotInterno.Item == null && ObterContainerUi() == TipoContainerUi.Inventario;

        if (data.AsGodotObject() is not SlotUI slotOrigem
            || slotOrigem.SlotInterno?.Item == null)
            return false;

        if (EhQualquerSlotBolsa)
            return slotOrigem.SlotInterno.Item.EhBolsa;

        if (ObterContainerUi() != slotOrigem.ObterContainerUi())
            return SlotInterno != null && SlotInterno.Item == null;

        return true;
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SlotEquipamentoUI slotEquipOrigem)
        {
            if (slotEquipOrigem.SlotLogico?.Item == null) return;

            var equipamentos = GetTree().CurrentScene.FindChild("Player", true, false)
                ?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
            var inventario = ObterInventario();

            if (equipamentos != null && inventario != null)
            {
                SlotInterno.Item = slotEquipOrigem.SlotLogico.Item;
                SlotInterno.Quantidade = 1;
                equipamentos.Desequipar(slotEquipOrigem.TipoDeSlot, inventario);
            }
            return;
        }

        if (data.AsGodotObject() is not SlotUI slotOrigem
            || slotOrigem.SlotInterno?.Item == null)
            return;

        var containerDestino = ObterContainerUi();
        var containerOrigem = slotOrigem.ObterContainerUi();

        // Equipar bolsa no inventário
        if (EhSlotBolsaInventario)
        {
            var inventario = ObterInventario();
            if (inventario == null) return;

            int index = ObterIndiceBolsa();
            inventario.EquiparBolsaNoSlot(slotOrigem.SlotInterno, index);
            slotOrigem.SlotInterno.Item = null;
            slotOrigem.SlotInterno.Quantidade = 0;
            inventario.NotificarMudancaExterna();
            return;
        }

        // Equipar bolsa no banco
        if (EhSlotBolsaBanco)
        {
            var banco = ObterBanco();
            if (banco == null) return;

            int index = ObterIndiceBolsa();
            banco.EquiparBolsaNoSlot(slotOrigem.SlotInterno, index);
            slotOrigem.SlotInterno.Item = null;
            slotOrigem.SlotInterno.Quantidade = 0;
            banco.NotificarMudancaExterna();
            return;
        }

        // Transferência entre inventário e banco
        if (containerOrigem != containerDestino)
        {
            if (SlotInterno == null || SlotInterno.Item != null) return;

            SlotInterno.Item = slotOrigem.SlotInterno.Item;
            SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;
            slotOrigem.SlotInterno.Item = null;
            slotOrigem.SlotInterno.Quantidade = 0;

            ObterInventario()?.NotificarMudancaExterna();
            ObterBanco()?.NotificarMudancaExterna();
            return;
        }

        // Movimentação dentro do mesmo container
        if (containerOrigem == TipoContainerUi.Inventario)
            MoverDentroDoInventario(slotOrigem);
        else if (containerOrigem == TipoContainerUi.Banco)
            MoverDentroDoBanco(slotOrigem);
    }

    private void MoverDentroDoInventario(SlotUI slotOrigem)
    {
        var inventario = ObterInventario();
        if (inventario == null || SlotInterno == null) return;

        if (slotOrigem.EhSlotBolsaInventario)
        {
            int indexOrigem = slotOrigem.ObterIndiceBolsa();

            if (SlotInterno.Item == null)
            {
                SlotInterno.Item = slotOrigem.SlotInterno.Item;
                SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;
                inventario.DesequiparBolsaNoSlot(indexOrigem);
                slotOrigem.SlotInterno.Item = null;
                slotOrigem.SlotInterno.Quantidade = 0;
            }
            else if (SlotInterno.Item.EhBolsa)
            {
                TrocarItens(slotOrigem);
                inventario.EquiparBolsaNoSlot(SlotInterno, indexOrigem);
            }
        }
        else
        {
            TrocarItens(slotOrigem);
        }

        inventario.NotificarMudancaExterna();
    }

    private void MoverDentroDoBanco(SlotUI slotOrigem)
    {
        var banco = ObterBanco();
        if (banco == null || SlotInterno == null) return;

        if (slotOrigem.EhSlotBolsaBanco)
        {
            int indexOrigem = slotOrigem.ObterIndiceBolsa();

            if (SlotInterno.Item == null)
            {
                SlotInterno.Item = slotOrigem.SlotInterno.Item;
                SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;
                banco.DesequiparBolsaNoSlot(indexOrigem);
                slotOrigem.SlotInterno.Item = null;
                slotOrigem.SlotInterno.Quantidade = 0;
            }
            else if (SlotInterno.Item.EhBolsa)
            {
                TrocarItens(slotOrigem);
                banco.EquiparBolsaNoSlot(SlotInterno, indexOrigem);
            }
        }
        else
        {
            TrocarItens(slotOrigem);
        }

        banco.NotificarMudancaExterna();
    }

    private void TrocarItens(SlotUI slotOrigem)
    {
        var itemTemp = SlotInterno.Item;
        int quantTemp = SlotInterno.Quantidade;

        SlotInterno.Item = slotOrigem.SlotInterno.Item;
        SlotInterno.Quantidade = slotOrigem.SlotInterno.Quantidade;

        slotOrigem.SlotInterno.Item = itemTemp;
        slotOrigem.SlotInterno.Quantidade = quantTemp;
    }
}
