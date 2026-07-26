namespace Mithara.Server.Entities;

public class LootEntity
{
    private static ulong _nextId = 1000000;
    public ulong Id { get; }
    public float X { get; set; }
    public float Y { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public ulong OwnerId { get; set; }
    public double SpawnTime { get; set; }
    public bool PickedUp { get; set; }
    public ItemInstance? PreservedItem { get; set; }

    public LootEntity(float x, float y, int itemId, int quantity, ulong ownerId, double gameTime)
    {
        Id = Interlocked.Increment(ref _nextId);
        X = x;
        Y = y;
        ItemId = itemId;
        Quantity = quantity;
        OwnerId = ownerId;
        SpawnTime = gameTime;
    }
}
