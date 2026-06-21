using Godot;

[GlobalClass]
public partial class LojaCashEntry : Resource
{
    [Export] public int ItemID { get; set; }
    [Export] public int PrecoDiamantes { get; set; }
    [Export] public string NomeExibicao { get; set; } = "";
    [Export] public string Descricao { get; set; } = "";
}
