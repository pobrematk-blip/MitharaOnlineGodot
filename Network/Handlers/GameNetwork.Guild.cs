#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendGuildCreate(string name)
    {
        _client?.SendPacket(PacketId.C2S_GuildCreate, w => w.Put(name));
    }

    public void SendGuildInvite(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_GuildInvite, w => w.Put(targetName));
    }

    public void SendGuildAccept()
    {
        _client?.SendPacket(PacketId.C2S_GuildAccept, w => { });
    }

    public void SendGuildLeave()
    {
        _client?.SendPacket(PacketId.C2S_GuildLeave, w => { });
    }

    public void SendGuildKick(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_GuildKick, w => w.Put(targetName));
    }

    public void SendGuildPromote(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_GuildPromote, w => w.Put(targetName));
    }

    public void SendGuildDemote(string targetName)
    {
        _client?.SendPacket(PacketId.C2S_GuildDemote, w => w.Put(targetName));
    }

    public void SendGuildBuySkill(string skillId)
    {
        _client?.SendPacket(PacketId.C2S_GuildBuySkill, w =>
        {
            w.Put(skillId);
        });
    }

    private void HandleGuildData(NetDataReader r)
    {
        int guildId = r.GetInt();
        string guildName = r.GetString();
        byte memberCount = r.GetByte();
        var members = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        for (int i = 0; i < memberCount; i++)
        {
            ulong eid = r.GetULong();
            string name = r.GetString();
            byte rank = r.GetByte();
            int hp = r.GetInt();
            int maxHp = r.GetInt();
            int mana = r.GetInt();
            int maxMana = r.GetInt();
            int level = r.GetInt();
            var m = new Godot.Collections.Dictionary
            {
                ["entity_id"] = (long)eid,
                ["name"] = name,
                ["rank"] = (int)rank,
                ["health"] = hp,
                ["max_health"] = maxHp,
                ["mana"] = mana,
                ["max_mana"] = maxMana,
                ["level"] = level,
            };
            members.Add(m);
        }

        int guildLevel = r.GetInt();
        int guildXp = r.GetInt();
        int skillPoints = r.GetInt();

        byte skillCount = r.GetByte();
        var skills = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        for (int i = 0; i < skillCount; i++)
        {
            string sk = r.GetString();
            int sl = r.GetInt();
            var s = new Godot.Collections.Dictionary { ["id"] = sk, ["level"] = sl };
            skills.Add(s);
        }

        EmitSignal(SignalName.OnGuildData, guildId, guildName, members, guildLevel, guildXp, skillPoints, skills);
    }

    private void HandleGuildMemberUpdate(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        string name = r.GetString();
        int rank = r.GetByte();
        bool joined = r.GetBool();
        EmitSignal(SignalName.OnGuildMemberUpdate, entityId, name, rank, joined);
    }

    private void HandleGuildRankUpdate(NetDataReader r)
    {
        ulong entityId = r.GetULong();
        int newRank = r.GetByte();
        EmitSignal(SignalName.OnGuildRankUpdate, entityId, newRank);
    }

    private void HandleGuildSkillUpdate(NetDataReader r)
    {
        string skillId = r.GetString();
        int newLevel = r.GetInt();
        EmitSignal(SignalName.OnGuildSkillUpdate, skillId, newLevel);
    }
}
