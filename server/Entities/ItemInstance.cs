namespace Mithara.Server.Entities;

public class ItemInstance
{
    public int DbId { get; set; }
    public int ItemId { get; set; }
    public int Slot { get; set; }
    public int Quantity { get; set; }
    public int RefineLevel { get; set; }

    public ItemDefinition? Definition => ItemDefinitions.Get(ItemId);
    public string Name => Definition?.Name ?? $"Item#{ItemId}";
    public ItemType Type => Definition?.Type ?? ItemType.None;
    public bool IsStackable => Definition?.IsStackable ?? false;
    public int MaxStack => Definition?.MaxStack ?? 1;
}
