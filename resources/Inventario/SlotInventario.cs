using Godot;
using System;

public class SlotInventario
{
    public ItemResource Item { get; set; }
    public int Quantidade { get; set; }

    // Construtor 1: Vazio
    public SlotInventario()
    {
        Item = null;
        Quantidade = 0;
    }

    // Construtor 2: Com parâmetros (Evita o erro da linha 20 do Componente!)
    public SlotInventario(ItemResource item, int quantidade)
    {
        Item = item;
        Quantidade = quantidade;
    }
}