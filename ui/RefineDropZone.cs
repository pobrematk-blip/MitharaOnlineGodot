using Godot;

public partial class RefineDropZone : Panel
{
    private RefineUI _refine;

    public override void _Ready()
    {
        _refine = GetParent()?.GetParent<RefineUI>();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (_refine == null) return false;
        return _refine.CanDropOnSlot(this, data);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _refine?.DropOnSlot(this, data);
    }
}
