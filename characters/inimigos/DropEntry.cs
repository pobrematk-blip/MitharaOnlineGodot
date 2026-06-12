using Godot;

[GlobalClass]
public partial class DropEntry : Resource
{
    [Export] public int ItemId { get; set; }
    [Export] public double Chance { get; set; }
    [Export] public int MinQty { get; set; } = 1;
    [Export] public int MaxQty { get; set; } = 1;

    public override string ToString()
    {
        return $"Item {ItemId} | {Chance*100:F1}% | {MinQty}-{MaxQty}";
    }
}
