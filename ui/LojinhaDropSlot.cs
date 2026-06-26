using Godot;

public partial class LojinhaDropSlot : Panel
{
    [Signal] public delegate void OnLojinhaDropEventHandler(Variant data);

    public override Variant _GetDragData(Vector2 atPosition)
    {
        return default;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.AsGodotObject() is SlotUI slot && slot.SlotInterno?.Item != null;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        EmitSignal(SignalName.OnLojinhaDrop, data);
    }
}
