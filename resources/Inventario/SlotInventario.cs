using Godot;
using System;

public class SlotInventario
{
    public ItemResource Item { get; set; }
    public int Quantidade { get; set; }
    public int RefinoNivel { get; set; }

    // Construtor 1: Vazio
    public SlotInventario()
    {
        Item = null;
        Quantidade = 0;
        RefinoNivel = 0;
    }

    // Construtor 2: Com parâmetros (Evita o erro da linha 20 do Componente!)
    public SlotInventario(ItemResource item, int quantidade, int refinoNivel = 0)
    {
        Item = item;
        Quantidade = quantidade;
        RefinoNivel = refinoNivel;
    }
}