using Godot;

[GlobalClass]
public partial class LojaCashData : Resource
{
    [Export] public Godot.Collections.Array<LojaCashEntry> Itens { get; set; } = new();
}
