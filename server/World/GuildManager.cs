namespace Mithara.Server.World;

public class Guild
{
    private static int _nextId;
    public int Id { get; set; }
    public string Name { get; }
    public string Tag { get; set; } = "";
    public int Emblem { get; set; } = -1;
    public ulong LeaderEntityId { get; set; }
    public HashSet<ulong> Members { get; } = new();
    public Dictionary<ulong, int> MemberRanks { get; } = new();
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int SkillPoints { get; set; }
    public Dictionary<string, int> SkillLevels { get; } = new();

    private static readonly int[] XpPerLevel = { 0, 100, 250, 500, 1000 };
    public int XpForNextLevel => Level < XpPerLevel.Length ? XpPerLevel[Level] : 999999;

    public Guild(string name, ulong leaderId, string tag = "", int emblem = -1)
    {
        Id = Interlocked.Increment(ref _nextId);
        Name = name;
        Tag = tag;
        Emblem = emblem;
        LeaderEntityId = leaderId;
        Members.Add(leaderId);
        MemberRanks[leaderId] = 0; // Lider
    }

    public void ClearLoadedMembers()
    {
        Members.Clear();
        MemberRanks.Clear();
        LeaderEntityId = 0;
    }

    public static void EnsureNextIdAtLeast(int id)
    {
        while (true)
        {
            int current = _nextId;
            if (current >= id) return;
            if (Interlocked.CompareExchange(ref _nextId, id, current) == current)
                return;
        }
    }

    public int GetRank(ulong entityId) =>
        MemberRanks.TryGetValue(entityId, out var r) ? r : 4; // Novato

    public bool IsLeader(ulong entityId) =>
        LeaderEntityId == entityId || GetRank(entityId) == 0;

    public void SetRank(ulong entityId, int rank)
    {
        if (Members.Contains(entityId))
        {
            MemberRanks[entityId] = rank;
            if (rank == 0)
                LeaderEntityId = entityId;
        }
    }

    public void AddXp(int amount)
    {
        Xp += amount;
        while (Level < XpPerLevel.Length && Xp >= XpForNextLevel)
        {
            Xp -= XpForNextLevel;
            Level++;
            SkillPoints++;
        }
    }

    public bool TryBuySkill(string skillId)
    {
        if (SkillPoints <= 0) return false;
        SkillLevels.TryGetValue(skillId, out var current);
        if (current >= 5) return false;
        SkillLevels[skillId] = current + 1;
        SkillPoints--;
        return true;
    }

    public int GetSkillLevel(string skillId) =>
        SkillLevels.TryGetValue(skillId, out var l) ? l : 0;
}

public class GuildManager
{
    private readonly Dictionary<int, Guild> _guilds = new();
    private readonly Dictionary<ulong, int> _playerGuild = new();
    private readonly object _lock = new();

    public Guild? GetGuild(int guildId)
    {
        lock (_lock) { _guilds.TryGetValue(guildId, out var g); return g; }
    }

    public int? GetPlayerGuildId(ulong entityId)
    {
        lock (_lock) { return _playerGuild.TryGetValue(entityId, out var id) ? id : null; }
    }

    public Guild? GetPlayerGuild(ulong entityId)
    {
        var id = GetPlayerGuildId(entityId);
        return id.HasValue ? GetGuild(id.Value) : null;
    }

    public Guild? CreateGuild(string name, ulong leaderId, string tag = "", int emblem = -1)
    {
        lock (_lock)
        {
            if (_playerGuild.ContainsKey(leaderId)) return null;
            if (_guilds.Values.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return null;

            var guild = new Guild(name, leaderId, tag, emblem);
            _guilds[guild.Id] = guild;
            _playerGuild[leaderId] = guild.Id;
            return guild;
        }
    }

    public bool AddMember(int guildId, ulong entityId)
    {
        lock (_lock)
        {
            if (_playerGuild.ContainsKey(entityId)) return false;
            if (!_guilds.TryGetValue(guildId, out var guild)) return false;
            guild.Members.Add(entityId);
            guild.MemberRanks[entityId] = 4; // Novato
            _playerGuild[entityId] = guildId;
            return true;
        }
    }

    public void RemoveMember(ulong entityId)
    {
        lock (_lock)
        {
            if (!_playerGuild.Remove(entityId, out var guildId)) return;
            if (!_guilds.TryGetValue(guildId, out var guild)) return;
            guild.Members.Remove(entityId);

            if (guild.Members.Count == 0)
                _guilds.Remove(guildId);
            else if (guild.LeaderEntityId == entityId)
                guild.LeaderEntityId = guild.Members.First();
        }
    }

    public void RemoveGuild(int guildId)
    {
        lock (_lock)
        {
            if (!_guilds.TryGetValue(guildId, out var guild)) return;
            foreach (var memberId in guild.Members.ToList())
            {
                _playerGuild.Remove(memberId);
            }
            guild.Members.Clear();
            _guilds.Remove(guildId);
        }
    }

    public void LoadGuild(Guild guild)
    {
        lock (_lock)
        {
            guild.ClearLoadedMembers();
            Guild.EnsureNextIdAtLeast(guild.Id);
            _guilds[guild.Id] = guild;
        }
    }

    public void LoadMember(int guildId, ulong entityId, int rank)
    {
        lock (_lock)
        {
            if (_guilds.TryGetValue(guildId, out var guild))
            {
                guild.Members.Add(entityId);
                guild.SetRank(entityId, rank);
                if (rank == 0)
                    guild.LeaderEntityId = entityId;
                _playerGuild[entityId] = guildId;
            }
        }
    }

    public void LoadSkill(int guildId, string skillId, int level)
    {
        lock (_lock)
        {
            if (_guilds.TryGetValue(guildId, out var guild))
                guild.SkillLevels[skillId] = level;
        }
    }

    public void SetMemberRank(int guildId, ulong entityId, int rank)
    {
        lock (_lock)
        {
            if (_guilds.TryGetValue(guildId, out var guild))
                guild.SetRank(entityId, rank);
        }
    }

    public bool ReplaceMemberEntityId(int guildId, ulong oldEntityId, ulong newEntityId)
    {
        lock (_lock)
        {
            if (!_guilds.TryGetValue(guildId, out var guild)) return false;
            if (!guild.Members.Contains(oldEntityId)) return false;
            if (guild.Members.Contains(newEntityId)) return false;

            guild.Members.Remove(oldEntityId);
            guild.Members.Add(newEntityId);

            if (guild.MemberRanks.TryGetValue(oldEntityId, out var rank))
            {
                guild.MemberRanks[newEntityId] = rank;
                guild.MemberRanks.Remove(oldEntityId);
            }

            if (guild.LeaderEntityId == oldEntityId)
                guild.LeaderEntityId = newEntityId;

            _playerGuild.Remove(oldEntityId);
            _playerGuild[newEntityId] = guildId;

            return true;
        }
    }

    public List<ulong> GetMemberEntityIds(int guildId)
    {
        lock (_lock)
        {
            return _guilds.TryGetValue(guildId, out var guild)
                ? guild.Members.ToList()
                : new List<ulong>();
        }
    }
}
