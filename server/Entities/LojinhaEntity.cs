namespace Mithara.Server.Entities;

public class LojinhaEntity
{
    private static ulong _nextId = 2000000;
    public ulong Id { get; }
    public int OwnerCharacterId { get; set; }
    public ulong OwnerEntityId { get; set; }
    public string OwnerName { get; set; } = "";
    public string ShopName { get; set; } = "";
    public string OwnerClass { get; set; } = "";
    public string OwnerRace { get; set; } = "";
    public bool IsOpen { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int ChannelId { get; set; }
    public int GoldEarned { get; set; }
    public int MaxSlots { get; set; } = 5;
    public List<LojinhaItem> Items { get; set; } = new();
    public ulong DbId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LojinhaEntity(float x, float y)
    {
        Id = Interlocked.Increment(ref _nextId);
        X = x;
        Y = y;
    }
}
