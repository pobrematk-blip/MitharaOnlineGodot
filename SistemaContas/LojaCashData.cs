using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class LojaCashEntry : Resource
{
    [Export] public int ItemID { get; set; }
    [Export] public int PrecoDiamantes { get; set; }
    [Export] public string NomeExibicao { get; set; } = "";
    [Export] public string Descricao { get; set; } = "";
}

[GlobalClass]
public partial class LojaCashData : Resource
{
    [Export] public Godot.Collections.Array<LojaCashEntry> Itens { get; set; } = new();
}
