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
            if (inventorySlot < 0)
                inventorySlot = GetInt(dict, "inventorySlot");
            if (inventorySlot < 0)
                inventorySlot = GetInt(dict, "source_slot");
            if (inventorySlot < 0)
                inventorySlot = GetInt(dict, "from_slot");

            quantity = GetInt(dict, "quantity");
            if (quantity <= 0)
                quantity = GetInt(dict, "quantidade");
            if (quantity <= 0)
                quantity = GetInt(dict, "count");

            if (dict.ContainsKey("source") && dict["source"].VariantType == Variant.Type.Object)
            {
                slot = dict["source"].Obj as SlotUI ?? dict["source"].AsGodotObject() as SlotUI;
                if (slot?.SlotInterno?.Item != null)
                {
                    inventorySlot = slot.SlotIndex;
                    quantity = slot.SlotInterno.Quantidade;
                }
            }

            if (slot == null && dict.ContainsKey("slot") && dict["slot"].VariantType == Variant.Type.Object)
            {
                slot = dict["slot"].Obj as SlotUI ?? dict["slot"].AsGodotObject() as SlotUI;
                if (slot?.SlotInterno?.Item != null)
                {
                    inventorySlot = slot.SlotIndex;
                    quantity = slot.SlotInterno.Quantidade;
                }
            }

            if (slot == null && dict.ContainsKey("item") && dict["item"].VariantType == Variant.Type.Object && quantity <= 0)
                quantity = 1;

            return inventorySlot >= 0 && quantity > 0;
        }

        return false;
    }

    private static int GetInt(Godot.Collections.Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
            return -1;

        var value = dict[key];
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => Mathf.RoundToInt((float)value.AsDouble()),
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed) ? parsed : -1,
            _ => -1
        };
    }
}
