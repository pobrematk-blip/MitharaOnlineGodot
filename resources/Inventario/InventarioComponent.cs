using Godot;
using System;
using System.Collections.Generic;

public partial class InventarioComponent : Node
{
    [Signal] public delegate void InventarioAtualizadoEventHandler();

    private int _tamanhoBase = 30; // Começa fixo com 30 slots base!
    public int TamanhoDoInventario { get; private set; }
    
    // Lista contendo os slots do nosso inventário geral
    public List<SlotInventario> Slots { get; private set; } = new List<SlotInventario>();

    // Lista contendo os 6 slots especiais de bolsas
    public List<SlotInventario> SlotsDasBolsasEquipadas { get; private set; } = new List<SlotInventario>();

    public override void _Ready()
    {
        // Inicializa os 6 slots de bolsas vazios no início do jogo
        for (int i = 0; i < 6; i++)
        {
            SlotsDasBolsasEquipadas.Add(new SlotInventario());
        }

        // Calcula os slots iniciais (30 base + somatório das bolsas)
        RecalcularTamanhoDoInventario(false); // false para não disparar sinal no _Ready
        
        GD.Print($"[INVENTÁRIO] Sistema inicializado com {TamanhoDoInventario} slots base e 6 slots de bolsa!");
    }

    // Ajustado para podermos escolher se queremos ou não disparar o sinal na hora
    public void RecalcularTamanhoDoInventario(bool dispararSinal = true)
    {
        int slotsDasBolsas = 0;

        foreach (var slotBolsa in SlotsDasBolsasEquipadas)
        {
            if (slotBolsa != null && slotBolsa.Item != null && slotBolsa.Item.EhBolsa)
            {
                slotsDasBolsas += slotBolsa.Item.SlotsAdicionais;
            }
        }

        TamanhoDoInventario = _tamanhoBase + slotsDasBolsas;

        // Ajusta o tamanho da lista dinâmica de slots
        AjustarListaDeSlots();

        // Avisa a UI para redesenhar a grade na tela (se permitido)
        if (dispararSinal)
        {
            EmitSignal(SignalName.InventarioAtualizado);
        }
    }

    private void AjustarListaDeSlots()
    {
        // Se o inventário cresceu (bolsa equipada), cria novos slots vazios
        while (Slots.Count < TamanhoDoInventario)
        {
            Slots.Add(new SlotInventario(null, 0));
        }

        // Se o inventário encolheu (bolsa removida), remove os últimos slots
        while (Slots.Count > TamanhoDoInventario)
        {
            Slots.RemoveAt(Slots.Count - 1);
        }
    }

    // Equipar a bolsa em um índice específico (0 a 5) vindo do Drag & Drop
    public void EquiparBolsaNoSlot(SlotInventario slotOrigem, int indexBolsa)
    {
        if (indexBolsa < 0 || indexBolsa >= SlotsDasBolsasEquipadas.Count) return;
        if (slotOrigem == null || slotOrigem.Item == null || !slotOrigem.Item.EhBolsa) return;

        // Guarda temporariamente o item que porventura já estava equipado nesse slot específico
        ItemResource bolsaAntiga = SlotsDasBolsasEquipadas[indexBolsa].Item;

        // Transfere a nova mochila para o slot dedicado escolhido
        SlotsDasBolsasEquipadas[indexBolsa].Item = slotOrigem.Item;
        SlotsDasBolsasEquipadas[indexBolsa].Quantidade = 1;

        // Se o jogador já tinha uma mochila nesse slot, devolve a antiga para o slot de onde veio a nova
        if (bolsaAntiga != null)
        {
            slotOrigem.Item = bolsaAntiga;
            slotOrigem.Quantidade = 1;
            GD.Print($"[INVENTÁRIO] Substituída bolsa no slot {indexBolsa} por '{SlotsDasBolsasEquipadas[indexBolsa].Item.Nome}'!");
        }
        else
        {
            // Deixa para o SlotUI limpar o slot de origem de forma limpa para evitar concorrência de dados
            GD.Print($"[INVENTÁRIO] Nova bolsa '{SlotsDasBolsasEquipadas[indexBolsa].Item.Nome}' reservada no slot {indexBolsa}!");
        }

        // Recalcula o tamanho interno sem disparar o sinal ainda (deixamos o SlotUI notificar no final do drop!)
        RecalcularTamanhoDoInventario(false);
    }

    // Desequipar a bolsa de um slot específico (0 a 5) quando arrastada para fora
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

        RecalcularTamanhoDoInventario(false);
        GD.Print($"[INVENTÁRIO] Bolsa do slot {indexBolsa} removida da lógica! Capacidade recalculada.");
        return true;
    }

    private void NotificarSistema(string message)
    {
        var chat = GetNodeOrNull<ChatUI>("/root/main/HUD/ChatUI");
        chat?.AddSystemMessage(message);
        GD.Print($"[SISTEMA] {message}");
    }

    // Permite forçar o redesenho manual da interface no momento exato desejado
    public void NotificarMudancaExterna()
    {
        EmitSignal(SignalName.InventarioAtualizado);
    }

    public void AplicarDadosServidor(Godot.Collections.Array<Godot.Collections.Dictionary> items, ItemDatabase itemDB)
    {
        Slots.Clear();
        RecalcularTamanhoDoInventario(false);

        for (int i = 0; i < Slots.Count; i++)
            Slots[i] = new SlotInventario(null, 0);

        foreach (var entry in items)
        {
            int slot = (int)entry["slot"];
            int itemId = (int)entry["item_id"];
            int qty = (int)entry["quantity"];
            int refineLevel = entry.ContainsKey("refine_level") ? (int)entry["refine_level"] : 0;
            string instanceData = entry.ContainsKey("instance_data") ? (string)entry["instance_data"] : "";

            if (slot >= 0 && slot < Slots.Count)
            {
                var resource = itemDB.GetItem(itemId);
                if (resource == null)
                {
                    itemDB.Refresh();
                    resource = itemDB.GetItem(itemId);
                }
                if (resource != null)
                    Slots[slot] = new SlotInventario(resource, qty, refineLevel, instanceData);
                else
                    GD.PrintErr($"[INVENTÁRIO] Item online ID {itemId} não existe no catálogo exportado do cliente.");
            }
        }

        EmitSignal(SignalName.InventarioAtualizado);
    }

    // Função para adicionar um item comum ao inventário
    public bool AdicionarItem(ItemResource novoItem, int quantidade = 1)
    {
        if (novoItem == null) return false;

        if (novoItem.Acumulavel)
        {
            int restante = quantidade;
            int maxStack = Math.Max(1, novoItem.QuantidadeMaximaPorSlot);

            foreach (var slot in Slots)
            {
                if (slot.Item != null && slot.Item.ItemID == novoItem.ItemID && slot.Quantidade < maxStack)
                {
                    int adicionar = Math.Min(restante, maxStack - slot.Quantidade);
                    slot.Quantidade += adicionar;
                    restante -= adicionar;
                    GD.Print($"[INVENTÁRIO] Adicionado {adicionar}x {novoItem.Nome} ao slot existente. Total: {slot.Quantidade}");
                    if (restante <= 0)
                    {
                        EmitSignal(SignalName.InventarioAtualizado);
                        return true;
                    }
                }
            }

            for (int i = 0; i < Slots.Count && restante > 0; i++)
            {
                if (Slots[i].Item != null)
                    continue;

                int adicionar = Math.Min(restante, maxStack);
                Slots[i].Item = novoItem;
                Slots[i].Quantidade = adicionar;
                restante -= adicionar;
                GD.Print($"[INVENTÁRIO] {novoItem.Nome} x{adicionar} colocado no Slot {i}!");
            }

            if (restante <= 0)
            {
                EmitSignal(SignalName.InventarioAtualizado);
                return true;
            }

            GD.Print("[INVENTÁRIO] Inventário cheio! Não foi possível coletar: ", novoItem.Nome);
            return false;
        }

        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Item == null)
            {
                Slots[i].Item = novoItem;
                Slots[i].Quantidade = quantidade;
                GD.Print($"[INVENTÁRIO] {novoItem.Nome} colocado no Slot {i}!");
                EmitSignal(SignalName.InventarioAtualizado);
                return true;
            }
        }

        GD.Print("[INVENTÁRIO] Inventário cheio! Não foi possível coletar: ", novoItem.Nome);
        return false;
    }
}
