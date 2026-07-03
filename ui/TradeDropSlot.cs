using Godot;

public partial class TradeDropSlot : Panel
{
    [Signal]
    public delegate void OnTradeItemDroppedEventHandler(int tradeSlot, int inventorySlot, int quantity);

    public int TradeSlot { get; set; }
    public bool Locked { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (Locked)
            return false;

        var slot = data.AsGodotObject() as SlotUI ?? data.Obj as SlotUI;
        return slot?.SlotInterno?.Item != null && slot.SlotInterno.Quantidade > 0;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var slot = data.AsGodotObject() as SlotUI ?? data.Obj as SlotUI;
        if (slot?.SlotInterno?.Item == null || slot.SlotInterno.Quantidade <= 0)
            return;

        EmitSignal(SignalName.OnTradeItemDropped, TradeSlot, slot.SlotIndex, slot.SlotInterno.Quantidade);
    }
}
