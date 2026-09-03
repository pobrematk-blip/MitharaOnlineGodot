namespace Mithara.Server.World;

public enum DungeonMode : byte
{
    Solo = 0,
    Group = 1,
}

public sealed class DungeonInstance
{
    public string Id { get; init; } = "";
    public string DungeonId { get; init; } = "";
    public DungeonMode Mode { get; init; }
    public int PartyId { get; init; } = -1;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public HashSet<ulong> Members { get; } = new();
    public HashSet<ulong> EncounterEntityIds { get; } = new();
    public bool EncounterSpawned { get; set; }
    public bool ExitUnlocked { get; set; }
    public ulong ExitCrystalEntityId { get; set; }
}

public sealed class DungeonManager
{
    private long _nextInstanceId;
    private readonly Dictionary<string, DungeonInstance> _instances = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public DungeonInstance Create(string dungeonId, DungeonMode mode, IEnumerable<ulong> members, int partyId = -1)
    {
        lock (_lock)
        {
            string id = $"{dungeonId}:{DateTime.UtcNow:yyyyMMdd}:{Interlocked.Increment(ref _nextInstanceId)}";
            var instance = new DungeonInstance
            {
                Id = id,
                DungeonId = dungeonId,
                Mode = mode,
                PartyId = partyId,
            };
            foreach (ulong member in members)
                instance.Members.Add(member);
            _instances[id] = instance;
            return instance;
        }
    }

    public DungeonInstance? Get(string instanceId)
    {
        lock (_lock)
            return _instances.TryGetValue(instanceId, out var instance) ? instance : null;
    }

    public void RemoveMember(string instanceId, ulong entityId)
    {
        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out var instance)) return;
            instance.Members.Remove(entityId);
            if (instance.Members.Count == 0)
                _instances.Remove(instanceId);
        }
    }
}
