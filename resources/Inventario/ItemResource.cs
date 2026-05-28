using Godot;
using System;

[GlobalClass] // Faz com que a Godot reconheça essa classe em todo o editor
public partial class ItemResource : Resource
{
    [Export] public string Nome { get; set; } = "Item Novo";
    [Export] public Texture2D Icone { get; set; }
    [Export] public bool Acumulavel { get; set; } = false;
    [Export] public int QuantidadeMaximaPorSlot { get; set; } = 99;
}