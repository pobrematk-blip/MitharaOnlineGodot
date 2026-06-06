using Godot;
using System;

public partial class SlotUI : Control
{
    private enum TipoContainerUi { Nenhum, Inventario, Banco }

    private TextureRect _icone;
    private Label _quantidadeTexto;
    private Texture2D _texturaFundoPadrao;

    public SlotInventario SlotInterno { get; private set; }
    public int SlotIndex { get; set; }

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
        _mouseSobre = true;

        if (SlotInterno?.Item == null) return;

        if (EhQualquerSlotBolsa && SlotInterno.Item.EhBolsa)
            _icone.SelfModulate = new Color(1.2f, 1.2f, 0.8f, 1);

        MostrarTooltip();
    }

    private void OnMouseExitedSlot()
    {
        _mouseSobre = false;

        if (SlotInterno?.Item != null)
            _icone.SelfModulate = new Color(1, 1, 1, 1);

        EsconderTooltip();
    }

    private ItemTooltip ObterTooltip()
    {
        return GetNodeOrNull<ItemTooltip>("/root/main/UI/ItemTooltip");
    }

    private void MostrarTooltip()
    {
        if (SlotInterno?.Item == null) return;
        var tip = ObterTooltip();
        if (tip == null) return;
        tip.Mostrar(SlotInterno.Item, GetGlobalMousePosition());
    }

    private void EsconderTooltip()
    {
        var tip = ObterTooltip();
        tip?.Esconder();
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
                TooltipText = $"ðŸŽ’ {slotLogico.Item.Nome}\n+{slotLogico.Item.SlotsAdicionais} slots no {(EhSlotBolsaBanco ? "banco" : "inventário")}";
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

    private double _ultimoCliqueEsquerdo = 0;
    private const double IntervaloDuploClique = 300;
    private bool _mouseSobre = false;

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouseEvent || !mouseEvent.Pressed)
            return;

        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            double agora = Time.GetTicksMsec();
            bool duploClique = (agora - _ultimoCliqueEsquerdo) < IntervaloDuploClique;
            _ultimoCliqueEsquerdo = agora;

            if (!duploClique || !_mouseSobre)
                return;

            GD.Print($"[SLOT] Duplo clique no slot {SlotIndex}");
            if (SlotInterno?.Item == null)
            {
                GD.Print("[SLOT] Slot vazio.");
                return;
            }
            if (EhQualquerSlotBolsa)
            {
                GD.Print("[SLOT] Slot de bolsa, ignorando.");
                return;
            }

            EquiparItemDoSlot();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseEvent.ButtonIndex != MouseButton.Right || !_mouseSobre)
            return;

        if (SlotInterno?.Item == null) return;

        if (EhQualquerSlotBolsa)
        {
            int indexBolsa = ObterIndiceBolsa();
            if (indexBolsa < 0) return;

            if (EhSlotBolsaInventario)
                RemoverBolsaParaInventario(indexBolsa);
            else if (EhSlotBolsaBanco)
                RemoverBolsaParaBanco(indexBolsa);

            GetViewport().SetInputAsHandled();
            return;
        }

        if (SlotInterno.Item.ItemID >= 100 && SlotInterno.Item.ItemID < 200)
        {
            UsarItemNoSlot();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EquiparItemDoSlot()
    {
        var item = SlotInterno.Item;
        if (item.Tipo == TipoEquipamento.Nenhum
            || item.Tipo == TipoEquipamento.Consumivel
            || item.Tipo == TipoEquipamento.Moeda
            || item.Tipo == TipoEquipamento.Feitico
            || item.EhBolsa)
            return;

        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Player;
        if (player == null) return;

        if (!EquipamentoComponent.PodeEquipar(item, player.NomeDaClasse))
        {
            GD.Print($"[SLOT] Classe {player.NomeDaClasse} não pode equipar {item.Nome}");
            return;
        }

        var equip = player.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;
        if (equip == null) return;

        equip.Equipar(item.Tipo, SlotInterno);

        var inv = ObterInventario();
        inv?.NotificarMudancaExterna();

        var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (gameNet != null && gameNet.IsConnected)
            gameNet.SendEquipItem(SlotIndex, (int)item.Tipo);

        GD.Print($"[SLOT] Item '{item.Nome}' equipado por duplo clique.");
    }

    private void UsarItemNoSlot()
    {
        if (SlotInterno?.Item == null) return;

        int itemId = SlotInterno.Item.ItemID;
        string itemNome = SlotInterno.Item.Nome;

        if (itemId == 100 || (itemNome != null && itemNome.IndexOf("Pergaminho", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            TentarCapturarPetComPergaminho();
        }
    }

    private void TentarCapturarPetComPergaminho()
    {
        var player = GetTree().CurrentScene?.FindChild("Player", true, false) as Node2D;
        if (player == null) return;

        var inimigos = GetTree().GetNodesInGroup("Inimigos");
        Inimigo alvo = null;
        float menorDist = 200f;

        foreach (var node in inimigos)
        {
            if (node is Inimigo inimigo)
            {
                float dist = player.GlobalPosition.DistanceTo(inimigo.GlobalPosition);
                if (dist < menorDist && inimigo.VidaAtual > 0 && inimigo.VidaAtual <= inimigo.VidaMax * 0.5f)
                {
                    menorDist = dist;
                    alvo = inimigo;
                }
            }
        }

        if (alvo == null)
        {
            GD.Print("[PERGAMINHO] Nenhum inimigo com menos de 50% de vida por perto.");
            return;
        }

        // Consome o pergaminho AGORA (antes do minigame)
        var inventario = ObterInventario();
        if (inventario == null) return;

        SlotInterno.Quantidade--;
        if (SlotInterno.Quantidade <= 0)
        {
            SlotInterno.Item = null;
            SlotInterno.Quantidade = 0;
        }
        inventario.NotificarMudancaExterna();

        string petNome = alvo.NomeDoInimigo;
        int petId = 3;

        var miniGame = GD.Load<PackedScene>("res://ui/Pets/PetScrollMiniGame.tscn").Instantiate<PetScrollMiniGame>();
        var root = GetTree().CurrentScene;
        if (root != null)
            root.AddChild(miniGame);

        miniGame.Connect(PetScrollMiniGame.SignalName.MiniGameConcluido, Callable.From((int capturedPetId, string capturedPetNome, bool sucesso) =>
        {
            if (sucesso && IsInstanceValid(alvo))
            {
                var itemPet = new ItemResource
                {
                    ItemID = 200 + capturedPetId,
                    Nome = capturedPetNome,
                    Descricao = $"Pet capturado: {capturedPetNome}",
                    Tipo = TipoEquipamento.Pet,
                    Acumulavel = false,
                    QuantidadeMaximaPorSlot = 1,
                };
                inventario.AdicionarItem(itemPet, 1);
                inventario.NotificarMudancaExterna();

                alvo.QueueFree();
                GD.Print($"[PERGAMINHO] Pet {capturedPetNome} capturado!");
            }

            if (IsInstanceValid(miniGame))
                miniGame.QueueFree();
        }));

        miniGame.IniciarMiniGame(petId, petNome);
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

        GD.Print("[INVENTÁRIO] âŒ Sem espaço para retirar a bolsa!");
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

        GD.Print("[BANCO] âŒ Sem espaço para retirar a bolsa!");
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (data.AsGodotObject() is SkillBarSlotUI) return true;

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
        if (data.AsGodotObject() is SkillBarSlotUI skillBarSlot)
        {
            skillBarSlot.Clear();
            return;
        }

        if (data.AsGodotObject() is SlotEquipamentoUI slotEquipOrigem)
        {
            if (slotEquipOrigem.SlotLogico?.Item == null) return;

            var equipamentos = GetTree().CurrentScene.FindChild("Player", true, false)
                ?.FindChild("EquipamentoComponent", true, false) as EquipamentoComponent;

            if (equipamentos != null)
            {
                SlotInterno.Item = slotEquipOrigem.SlotLogico.Item;
                SlotInterno.Quantidade = 1;
                slotEquipOrigem.SlotLogico.Item = null;
                slotEquipOrigem.SlotLogico.Quantidade = 0;
                equipamentos.EmitSignal(EquipamentoComponent.SignalName.EquipamentoAtualizado);
            }

            var inv = ObterInventario();
            inv?.NotificarMudancaExterna();

            var gameNet = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            if (gameNet != null && gameNet.IsConnected)
                gameNet.SendUnequipItem((int)slotEquipOrigem.TipoDeSlot, SlotIndex);

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
            ObterBanco()?.NotificarMudancaExterna();
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
            ObterInventario()?.NotificarMudancaExterna();
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

            if (slotOrigem.EhSlotBolsaInventario)
                ObterInventario()?.DesequiparBolsaNoSlot(slotOrigem.ObterIndiceBolsa());
            else if (slotOrigem.EhSlotBolsaBanco)
                ObterBanco()?.DesequiparBolsaNoSlot(slotOrigem.ObterIndiceBolsa());

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
