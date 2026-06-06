namespace Mithara.Server.World;

public class Party
{
    private static int _nextId;
    public int Id { get; }
    public ulong LeaderEntityId { get; set; }
    public HashSet<ulong> Members { get; } = new();

    public Party(ulong leaderId)
    {
        Id = Interlocked.Increment(ref _nextId);
        LeaderEntityId = leaderId;
        Members.Add(leaderId);
    }
}

public class PartyManager
{
    private readonly Dictionary<int, Party> _parties = new();
    private readonly Dictionary<ulong, int> _playerParty = new();
    private readonly object _lock = new();

    public Party? GetParty(int partyId)
    {
        lock (_lock) { _parties.TryGetValue(partyId, out var p); return p; }
    }

    public int? GetPlayerPartyId(ulong entityId)
    {
        lock (_lock) { return _playerParty.TryGetValue(entityId, out var id) ? id : null; }
    }

    public Party? CreateParty(ulong leaderId)
    {
        lock (_lock)
        {
            if (_playerParty.ContainsKey(leaderId)) return null;
            var party = new Party(leaderId);
            _parties[party.Id] = party;
            _playerParty[leaderId] = party.Id;
            return party;
        }
    }

    public bool AddMember(int partyId, ulong entityId)
    {
        lock (_lock)
        {
            if (_playerParty.ContainsKey(entityId)) return false;
            if (!_parties.TryGetValue(partyId, out var party)) return false;
            party.Members.Add(entityId);
            _playerParty[entityId] = partyId;
            return true;
        }
    }

    public void RemoveMember(ulong entityId)
    {
        lock (_lock)
        {
            if (!_playerParty.Remove(entityId, out var partyId)) return;
            if (!_parties.TryGetValue(partyId, out var party)) return;
            party.Members.Remove(entityId);

            if (party.Members.Count <= 1)
            {
                var last = party.Members.FirstOrDefault();
                if (last != 0)
                {
                    _playerParty.Remove(last);
                }
                _parties.Remove(partyId);
            }
            else if (party.LeaderEntityId == entityId)
            {
                party.LeaderEntityId = party.Members.First();
            }
        }
    }

    public List<ulong> GetMemberEntityIds(int partyId)
    {
        lock (_lock)
        {
            return _parties.TryGetValue(partyId, out var party)
                ? party.Members.ToList()
                : new List<ulong>();
        }
    }
}
