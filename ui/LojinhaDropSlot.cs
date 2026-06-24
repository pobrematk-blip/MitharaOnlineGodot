using Godot;

public partial class LojinhaDropSlot : Control
{
    [Signal] public delegate void OnLojinhaDropEventHandler(Variant data);

    public override Variant _GetDragData(Vector2 atPosition)
    {
        return default;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (data.Obj is SlotUI slot && slot.SlotInterno?.Item != null)
        {
            var tipo = slot.SlotInterno.Item.Tipo;
            return tipo != TipoEquipamento.Nenhum;
        }
        return false;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        EmitSignal(SignalName.OnLojinhaDrop, data);
    }
}
