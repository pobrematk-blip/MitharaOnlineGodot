using Godot;

public partial class MarketplaceDropSlot : Panel
{
    [Signal]
    public delegate void OnMarketplaceItemDroppedEventHandler(int inventorySlot, int quantity);

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return TryReadInventoryDrop(data, out _, out _);
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TryReadInventoryDrop(data, out int inventorySlot, out int quantity))
            EmitSignal(SignalName.OnMarketplaceItemDropped, inventorySlot, quantity);
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

        if (data.VariantType == Variant.Type.Dictionary)
        {
            var dict = data.AsGodotDictionary();
            inventorySlot = GetInt(dict, "slot");
            if (inventorySlot < 0)
                inventorySlot = GetInt(dict, "inventory_slot");
            if (inventorySlot < 0)
                inventorySlot = GetInt(dict, "slot_index");

            quantity = GetInt(dict, "quantity");
            if (quantity <= 0)
                quantity = GetInt(dict, "quantidade");

            if (dict.ContainsKey("source") && dict["source"].VariantType == Variant.Type.Object)
            {
                slot = dict["source"].Obj as SlotUI ?? dict["source"].AsGodotObject() as SlotUI;
                if (slot?.SlotInterno?.Item != null)
                {
                    inventorySlot = slot.SlotIndex;
                    quantity = slot.SlotInterno.Quantidade;
                }
            }

            return inventorySlot >= 0 && quantity > 0;
        }

        return false;
    }

    private static int GetInt(Godot.Collections.Dictionary dict, string key)
    {
        return dict.ContainsKey(key) ? dict[key].AsInt32() : -1;
    }
}
