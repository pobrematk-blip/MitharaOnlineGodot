using Godot;
using System.Collections.Generic;

public partial class BancoComponent : Node
{
    [Signal] public delegate void BancoAtualizadoEventHandler();

    public const int ColunasPorLinha = 8;

    private int _tamanhoBase = 30;
    public int TamanhoDoBanco { get; private set; }

    public List<SlotInventario> Slots { get; private set; } = new List<SlotInventario>();
    public List<SlotInventario> SlotsDasBolsasEquipadas { get; private set; } = new List<SlotInventario>();

    private InventarioComponent _inventarioDoPlayer;

    public override void _Ready()
    {
        for (int i = 0; i < 6; i++)
            SlotsDasBolsasEquipadas.Add(new SlotInventario());

        RecalcularTamanhoDoBanco(false);

        var player = GetParent();
        _inventarioDoPlayer = player?.FindChild("InventarioComponent", true, false) as InventarioComponent;

        if (_inventarioDoPlayer != null)
            GD.Print("[BANCO] ✓ Conectado ao Inventário do Player!");
        else
            GD.PrintErr("[BANCO] ✘ InventarioComponent não encontrado no Player!");

        GD.Print($"[BANCO] ðŸ¦ Banco inicializado com {TamanhoDoBanco} slots e 6 slots de bolsa!");
    }

    public void RecalcularTamanhoDoBanco(bool dispararSinal = true)
    {
        int slotsDasBolsas = 0;

        foreach (var slotBolsa in SlotsDasBolsasEquipadas)
        {
            if (slotBolsa?.Item != null && slotBolsa.Item.EhBolsa)
                slotsDasBolsas += slotBolsa.Item.SlotsAdicionais;
        }

        TamanhoDoBanco = _tamanhoBase + slotsDasBolsas;
        AjustarListaDeSlots();

        if (dispararSinal)
            EmitSignal(SignalName.BancoAtualizado);
    }

    private void AjustarListaDeSlots()
    {
        while (Slots.Count < TamanhoDoBanco)
            Slots.Add(new SlotInventario(null, 0));

        while (Slots.Count > TamanhoDoBanco)
            Slots.RemoveAt(Slots.Count - 1);
    }

    public void EquiparBolsaNoSlot(SlotInventario slotOrigem, int indexBolsa)
    {
        if (indexBolsa < 0 || indexBolsa >= SlotsDasBolsasEquipadas.Count) return;
        if (slotOrigem?.Item == null || !slotOrigem.Item.EhBolsa) return;

        ItemResource bolsaAntiga = SlotsDasBolsasEquipadas[indexBolsa].Item;

        SlotsDasBolsasEquipadas[indexBolsa].Item = slotOrigem.Item;
        SlotsDasBolsasEquipadas[indexBolsa].Quantidade = 1;

        if (bolsaAntiga != null)
        {
            slotOrigem.Item = bolsaAntiga;
            slotOrigem.Quantidade = 1;
            GD.Print($"[BANCO] Substituída bolsa no slot {indexBolsa} por '{SlotsDasBolsasEquipadas[indexBolsa].Item.Nome}'!");
        }
        else
        {
            GD.Print($"[BANCO] Nova bolsa '{SlotsDasBolsasEquipadas[indexBolsa].Item.Nome}' equipada no slot {indexBolsa}!");
        }

        RecalcularTamanhoDoBanco(false);
    }

    // Retorna true se conseguiu remover, false se os slots extras ainda têm itens
    public bool DesequiparBolsaNoSlot(int indexBolsa)
    {
        if (indexBolsa < 0 || indexBolsa >= SlotsDasBolsasEquipadas.Count) return false;

        var bolsa = SlotsDasBolsasEquipadas[indexBolsa]?.Item;
        if (bolsa == null) return false;

        int slotsDaBolsa = bolsa.SlotsAdicionais;
        int novoLimite = Slots.Count - slotsDaBolsa;
        for (int i = novoLimite; i < Slots.Count; i++)
        {
            if (Slots[i].Item != null)
            {
                NotificarSistema($"Não é possível remover a bolsa: slot extra contém '{Slots[i].Item.Nome}'. Esvazie-o primeiro.");
                return false;
            }
        }

        SlotsDasBolsasEquipadas[indexBolsa].Item = null;
        SlotsDasBolsasEquipadas[indexBolsa].Quantidade = 0;

        RecalcularTamanhoDoBanco(false);
        GD.Print($"[BANCO] Bolsa removida do slot {indexBolsa}. Capacidade recalculada.");
        return true;
    }

    private void NotificarSistema(string message)
    {
        var chat = GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
        chat?.AddSystemMessage(message);
        GD.Print($"[SISTEMA] {message}");
    }

    public void NotificarMudancaExterna()
    {
        EmitSignal(SignalName.BancoAtualizado);
    }

    public void AplicarDadosServidor(Godot.Collections.Array<Godot.Collections.Dictionary> items, ItemDatabase itemDb)
    {
        if (itemDb == null) return;

        foreach (var slot in Slots)
        {
            slot.Item = null;
            slot.Quantidade = 0;
            slot.RefinoNivel = 0;
            slot.DadosInstancia = "";
        }

        foreach (var entry in items)
        {
            int slotIndex = (int)entry["slot"];
            if (slotIndex < 0 || slotIndex >= Slots.Count) continue;

            int itemId = (int)entry["item_id"];
            int quantity = (int)entry["quantity"];
            int refineLevel = (int)entry["refine_level"];
            string instanceData = (string)entry["instance_data"];
            var resource = itemDb.GetItem(itemId);
            if (resource == null)
            {
                itemDb.Refresh();
                resource = itemDb.GetItem(itemId);
            }
            if (resource == null) continue;

            Slots[slotIndex] = new SlotInventario(resource, quantity, refineLevel, instanceData);
        }

        EmitSignal(SignalName.BancoAtualizado);
    }

    public bool AdicionarItem(ItemResource novoItem, int quantidade = 1)
    {
        if (novoItem == null) return false;

        if (novoItem.Acumulavel)
        {
            foreach (var slot in Slots)
            {
                if (slot.Item != null && slot.Item.Nome == novoItem.Nome
                    && slot.Quantidade < slot.Item.QuantidadeMaximaPorSlot)
                {
                    slot.Quantidade += quantidade;
                    EmitSignal(SignalName.BancoAtualizado);
                    return true;
                }
            }
        }

        foreach (var slot in Slots)
        {
            if (slot.Item == null)
            {
                slot.Item = novoItem;
                slot.Quantidade = quantidade;
                EmitSignal(SignalName.BancoAtualizado);
                return true;
            }
        }

        GD.Print("[BANCO] Banco cheio! Não foi possível adicionar: ", novoItem.Nome);
        return false;
    }

    /// <summary>
    /// Transfere item do inventário do jogador para o banco.
    /// </summary>
    public bool TransferirDoInventario(SlotInventario slotInventario, int quantidade = 1)
    {
        if (_inventarioDoPlayer == null || slotInventario?.Item == null) return false;

        ItemResource item = slotInventario.Item;
        int qtd = Mathf.Min(quantidade, slotInventario.Quantidade);

        if (!AdicionarItem(item, qtd)) return false;

        slotInventario.Quantidade -= qtd;
        if (slotInventario.Quantidade <= 0)
        {
            slotInventario.Item = null;
            slotInventario.Quantidade = 0;
        }

        _inventarioDoPlayer.NotificarMudancaExterna();
        GD.Print($"[BANCO] ↑ {qtd}x {item.Nome} depositado do inventário.");
        return true;
    }

    /// <summary>
    /// Transfere item do banco para o inventário do jogador.
    /// </summary>
    public bool TransferirParaInventario(SlotInventario slotBanco, int quantidade = 1)
    {
        if (_inventarioDoPlayer == null || slotBanco?.Item == null) return false;

        ItemResource item = slotBanco.Item;
        int qtd = Mathf.Min(quantidade, slotBanco.Quantidade);

        if (!_inventarioDoPlayer.AdicionarItem(item, qtd)) return false;

        slotBanco.Quantidade -= qtd;
        if (slotBanco.Quantidade <= 0)
        {
            slotBanco.Item = null;
            slotBanco.Quantidade = 0;
        }

        EmitSignal(SignalName.BancoAtualizado);
        GD.Print($"[BANCO] ↓ {qtd}x {item.Nome} sacado para o inventário.");
        return true;
    }
}
