using Godot;
using System;

public partial class SlotInventario : RefCounted
{
    public ItemResource Item { get; set; }
    public int Quantidade { get; set; }

    public SlotInventario(ItemResource item, int quantidade)
    {
        Item = item;
        Quantidade = quantidade;
    }
}