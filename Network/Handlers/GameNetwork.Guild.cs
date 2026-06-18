#nullable enable
using Godot;
using LiteNetLib.Utils;
using Mithara.Network;

partial class GameNetwork
{
    public void SendGuildCreate(string name)
    {
        _client?.SendPacket(PacketId.C2S_GuildCreate, w =>
        {
            w.Put(name);
            w.Put("");
            w.Put(-1);
        });
    }

    public void SendGuildCreateRequest(string name, string tag, int emblemIdx)
    {
        _client?.SendPacket(PacketId.C2S_GuildCreate, w =>
        {
            w.Put(name);
            w.Put(tag);
            w.Put(emblemIdx);
        });
    }

    private void HandleGuildCreateResult(NetDataReader r)
    {
        int guildId = r.GetInt();
        bool success = r.GetBool();
        string message = r.GetString();
        EmitSignal(SignalName.OnGuildCreateResult, guildId, success, message);
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
        string guildTag = r.GetString();
        int guildEmblem = r.GetInt();
        byte memberCount = r.GetByte();
        var members = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        string liderNome = "";

        GuildId = guildId;
        IsGuildLeader = false;
        int meuRank = 4;

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

            if (rank == 0)
                liderNome = name;

            if (eid == LocalPlayerId)
            {
                meuRank = rank;
                if (rank == 0)
                    IsGuildLeader = true;
            }
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

        SalvarGuildDataLocal(guildId, guildName, guildTag, guildEmblem, liderNome, guildLevel, guildXp, memberCount, meuRank);
        AtualizarOverheadUI();

        EmitSignal(SignalName.OnGuildData, guildId, guildName, guildTag, guildEmblem, members, guildLevel, guildXp, skillPoints, skills);
    }

    private void SalvarGuildDataLocal(int guildId, string guildName, string tag, int emblemIdx, string liderNome = "", int guildLevel = 1, int guildXp = 0, int memberCount = 0, int meuRank = 4)
    {
        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        string nome = escolhido?.NomePersonagem?.Replace(" ", "_") ?? "default";
        string path = $"user://guild_data_{nome}.cfg";
        var cfg = new ConfigFile();
        cfg.Load(path);
        cfg.SetValue("Guild", "id", guildId);
        cfg.SetValue("Guild", "name", guildName);
        cfg.SetValue("Guild", "tag", tag);
        cfg.SetValue("Guild", "emblem_index", emblemIdx);
        cfg.SetValue("Guild", "is_leader", IsGuildLeader);
        cfg.SetValue("Guild", "leader_name", liderNome);
        cfg.SetValue("Guild", "level", guildLevel);
        cfg.SetValue("Guild", "xp", guildXp);
        cfg.SetValue("Guild", "member_count", memberCount);
        cfg.SetValue("Guild", "meu_rank", meuRank);
        cfg.Save(path);
    }

    private void HandleGuildClear()
    {
        GuildId = -1;
        IsGuildLeader = false;
        LimparDadosGuildLocal();
        EmitSignal(SignalName.OnGuildCleared);
    }

    private void LimparDadosGuildLocal()
    {
        var escolhido = GetNodeOrNull<PersonagemEscolhido>("/root/PersonagemEscolhido");
        string nome = escolhido?.NomePersonagem?.Replace(" ", "_") ?? "default";
        string path = $"user://guild_data_{nome}.cfg";
        var cfg = new ConfigFile();
        cfg.Load(path);
        cfg.SetValue("Guild", "id", -1);
        cfg.SetValue("Guild", "name", "");
        cfg.SetValue("Guild", "tag", "");
        cfg.SetValue("Guild", "emblem_index", -1);
        cfg.SetValue("Guild", "is_leader", false);
        cfg.SetValue("Guild", "leader_name", "");
        cfg.SetValue("Guild", "level", 1);
        cfg.SetValue("Guild", "xp", 0);
        cfg.SetValue("Guild", "member_count", 0);
        cfg.SetValue("Guild", "meu_rank", 4);
        cfg.Save(path);
        AtualizarOverheadUI();
    }

    private void AtualizarOverheadUI()
    {
        var overhead = GetTree()?.Root.FindChild("OverheadUI", true, false) as OverheadUI;
        overhead?.RecarregarDadosGuild();
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
