using Godot;

public partial class PetCollarSlot : Panel
{
    public const int ColeiraPetItemId = 121;

    [Signal]
    public delegate void OnPetCollarDroppedEventHandler(int inventorySlot);

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return TryReadInventorySlot(data, out _, out int itemId) && itemId == ColeiraPetItemId;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TryReadInventorySlot(data, out int inventorySlot, out int itemId) && itemId == ColeiraPetItemId)
            EmitSignal(SignalName.OnPetCollarDropped, inventorySlot);
    }

    private static bool TryReadInventorySlot(Variant data, out int inventorySlot, out int itemId)
    {
        inventorySlot = -1;
        itemId = 0;

        var slot = data.AsGodotObject() as SlotUI ?? data.Obj as SlotUI;
        if (slot?.SlotInterno?.Item != null)
        {
            inventorySlot = slot.SlotIndex;
            itemId = slot.SlotInterno.Item.ItemID;
            return inventorySlot >= 0;
        }

        if (data.VariantType != Variant.Type.Dictionary)
            return false;

        var dict = data.AsGodotDictionary();
        inventorySlot = GetInt(dict, "slot");
        if (inventorySlot < 0) inventorySlot = GetInt(dict, "inventory_slot");
        if (inventorySlot < 0) inventorySlot = GetInt(dict, "slot_index");
        if (inventorySlot < 0) inventorySlot = GetInt(dict, "source_slot");

        itemId = GetInt(dict, "item_id");
        if (itemId <= 0) itemId = GetInt(dict, "itemId");

        if (dict.ContainsKey("source") && dict["source"].VariantType == Variant.Type.Object)
        {
            slot = dict["source"].Obj as SlotUI ?? dict["source"].AsGodotObject() as SlotUI;
            if (slot?.SlotInterno?.Item != null)
            {
                inventorySlot = slot.SlotIndex;
                itemId = slot.SlotInterno.Item.ItemID;
            }
        }

        if (dict.ContainsKey("slot") && dict["slot"].VariantType == Variant.Type.Object)
        {
            slot = dict["slot"].Obj as SlotUI ?? dict["slot"].AsGodotObject() as SlotUI;
            if (slot?.SlotInterno?.Item != null)
            {
                inventorySlot = slot.SlotIndex;
                itemId = slot.SlotInterno.Item.ItemID;
            }
        }

        return inventorySlot >= 0 && itemId > 0;
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
            _ => -1,
        };
    }
}
