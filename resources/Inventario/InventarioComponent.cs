using Godot;
using System;
using System.Collections.Generic;

public partial class InventarioComponent : Node
{
    [Export] public int TamanhoDoInventario { get; set; } = 24;
    
    // Lista contendo os slots do nosso inventário
    public List<SlotInventario> Slots { get; private set; } = new List<SlotInventario>();

    // Sinal para avisar a UI quando o inventário mudar
    [Signal] public delegate void InventarioAtualizadoEventHandler();

    public override void _Ready()
    {
        // Inicializa o inventário com slots vazios
        for (int i = 0; i < TamanhoDoInventario; i++)
        {
            Slots.Add(new SlotInventario(null, 0));
        }
        GD.Print("[INVENTÁRIO] Sistema inicializado com ", TamanhoDoInventario, " slots!");
    }

    // Função para adicionar um item ao inventário
    public bool AdicionarItem(ItemResource novoItem, int quantidade = 1)
    {
        if (novoItem == null) return false;

        // 1. Se o item for acumulável, procura por um slot que já tenha esse item e que não esteja cheio
        if (novoItem.Acumulavel)
        {
            foreach (var slot in Slots)
            {
                if (slot.Item != null && slot.Item.Nome == novoItem.Nome && slot.Quantidade < slot.Item.QuantidadeMaximaPorSlot)
                {
                    slot.Quantidade += quantidade;
                    GD.Print($"[INVENTÁRIO] Adicionado {quantidade}x {novoItem.Nome} ao slot existente. Total: {slot.Quantidade}");
                    EmitSignal(SignalName.InventarioAtualizado);
                    return true;
                }
            }
        }

        // 2. Se não achou slot igual ou não é acumulável, procura o primeiro slot vazio disponível
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