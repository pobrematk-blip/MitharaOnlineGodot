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

        return TryReadInventoryDrop(data, out _, out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryReadInventoryDrop(data, out int inventorySlot, out int quantity))
            return;

        EmitSignal(SignalName.OnTradeItemDropped, TradeSlot, inventorySlot, quantity);
    }

    private static bool TryReadInventoryDrop(Variant data, out int inventorySlot, out int quantity)
    {
        inventorySlot = -1;
        quantity = 0;

        var slot = data.AsGodotObject() as SlotUI ?? data.Obj as SlotUI;
        if (slot?.SlotInterno?.Item != null && slot.SlotInterno.Quantidade > 0)
        {
            inventorySlot = slot.SlotIndex;
            quantity = slot.SlotInterno.Quantidade;
            return inventorySlot >= 0;
        }

        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        inventorySlot = GetInt(dict, "inventory_slot", GetInt(dict, "slot", GetInt(dict, "slot_index", -1)));
        quantity = GetInt(dict, "quantity", 1);
        return inventorySlot >= 0 && quantity > 0;
    }

    private static int GetInt(Godot.Collections.Dictionary dict, string key, int fallback)
    {
        return dict.ContainsKey(key) ? dict[key].AsInt32() : fallback;
    }
}
