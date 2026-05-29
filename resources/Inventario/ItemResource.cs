using Godot;
using System;

[GlobalClass] // Faz com que a Godot reconheça essa classe em todo o editor
public partial class ItemResource : Resource
{
    [Export] public string Nome { get; set; } = "Item Novo";
    [Export] public Texture2D Icone { get; set; }
    [Export] public bool Acumulavel { get; set; } = false;
    [Export] public int QuantidadeMaximaPorSlot { get; set; } = 99;
    
    // Novas propriedades para o sistema de Bolsas (estilo Farm Together / WoW)
    [Export] public bool EhBolsa { get; set; } = false; // Define se este item pode ser equipado como mochila
    [Export] public int SlotsAdicionais { get; set; } = 10; // Quantos slots essa bolsa vai liberar (ex: 5, 10, 20)
}