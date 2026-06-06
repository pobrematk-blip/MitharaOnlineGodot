using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleGuildCreatePacket(NetPeer peer, NetDataReader reader)
    {
        string name = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _) || sender == null) return;
        HandleGuildCreate(peer, sender, name);
        if (sender.GuildId >= 0)
        {
            var guild = _world.Guilds.GetGuild(sender.GuildId);
            if (guild != null)
            {
                _db.SaveGuild(guild.Id, guild.Name, guild.Level, guild.Xp, guild.SkillPoints);
                _db.SaveGuildMember(guild.Id, sender.Id, sender.Name, 0);
                BroadcastGuildData(sender);
            }
        }
    }

    private void HandleGuildInvitePacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out var ch)) return;
        HandleGuildInvite(peer, _sessions[peer], sender, targetName);
    }

    private void HandleGuildAcceptPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out var ch)) return;
        var oldGuildId = sender.GuildId;
        HandleGuildAccept(peer, _sessions[peer], sender);
        if (sender.GuildId >= 0 && sender.GuildId != oldGuildId)
        {
            var guild = _world.Guilds.GetGuild(sender.GuildId);
            if (guild != null)
            {
                _db.SaveGuildMember(guild.Id, sender.Id, sender.Name, 4);
                BroadcastGuildMemberUpdate(guild, sender.Id, sender.Name, true);
                BroadcastGuildData(sender);
            }
        }
    }

    private void HandleGuildLeavePacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        HandleGuildLeave(sender);
    }

    private void HandleGuildKickPacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null || guild.LeaderEntityId != sender.Id)
        { SendSystemMessage(peer, "Apenas o líder pode expulsar."); return; }

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || !guild.Members.Contains(target.Id))
        { SendSystemMessage(peer, "Jogador não encontrado na guilda."); return; }

        if (target.Id == guild.LeaderEntityId)
        { SendSystemMessage(peer, "Você não pode expulsar a si mesmo."); return; }

        _db.DeleteGuildMember(guild.Id, target.Id);
        BroadcastGuildMemberUpdate(guild, target.Id, target.Name, false);
        _world.Guilds.RemoveMember(target.Id);
        target.GuildId = -1;
        target.GuildName = "";
        SendSystemMessage(peer, $"{target.Name} foi expulso da guilda.");
        if (targetPeer != null)
            SendSystemMessage(targetPeer, "Você foi expulso da guilda.");
    }

    private void HandleGuildPromotePacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null || guild.LeaderEntityId != sender.Id)
        { SendSystemMessage(peer, "Apenas o líder pode promover."); return; }

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || !guild.Members.Contains(target.Id))
        { SendSystemMessage(peer, "Jogador não encontrado na guilda."); return; }

        int current = guild.GetRank(target.Id);
        if (current <= 0)
        { SendSystemMessage(peer, "Este cargo já é o máximo."); return; }

        guild.SetRank(target.Id, current - 1);
        _db.SaveGuildMember(guild.Id, target.Id, target.Name, current - 1);
        BroadcastGuildRankUpdate(guild, target.Id, current - 1);
        SendSystemMessage(peer, $"{target.Name} promovido para cargo {current - 1}.");
    }

    private void HandleGuildDemotePacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null || guild.LeaderEntityId != sender.Id)
        { SendSystemMessage(peer, "Apenas o líder pode rebaixar."); return; }

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || !guild.Members.Contains(target.Id))
        { SendSystemMessage(peer, "Jogador não encontrado na guilda."); return; }

        int current = guild.GetRank(target.Id);
        if (current >= 4)
        { SendSystemMessage(peer, "Este cargo já é o mínimo."); return; }

        guild.SetRank(target.Id, current + 1);
        _db.SaveGuildMember(guild.Id, target.Id, target.Name, current + 1);
        BroadcastGuildRankUpdate(guild, target.Id, current + 1);
        SendSystemMessage(peer, $"{target.Name} rebaixado para cargo {current + 1}.");
    }

    private void HandleGuildBuySkillPacket(NetPeer peer, NetDataReader reader)
    {
        string skillId = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null || guild.LeaderEntityId != sender.Id)
        { SendSystemMessage(peer, "Apenas o líder pode comprar skills."); return; }

        if (!guild.TryBuySkill(skillId))
        { SendSystemMessage(peer, "Não foi possível comprar a skill (pontos insuficientes ou nível máximo)."); return; }

        int newLevel = guild.GetSkillLevel(skillId);
        _db.SaveGuildSkill(guild.Id, skillId, newLevel);
        BroadcastGuildSkillUpdate(guild, skillId, newLevel);
        SendSystemMessage(peer, $"Skill '{skillId}' evoluída para nível {newLevel}!");
    }

    private void HandleGuildCreate(NetPeer peer, Entity sender, string guildName)
    {
        if (sender is not PlayerEntity player) return;

        if (player.GuildId >= 0)
        {
            SendSystemMessage(peer, "Você já está em uma guilda.");
            return;
        }

        if (guildName.Length < 2 || guildName.Length > 30)
        {
            SendSystemMessage(peer, "O nome da guilda deve ter entre 2 e 30 caracteres.");
            return;
        }

        var guild = _world.Guilds.CreateGuild(guildName, sender.Id);
        if (guild == null)
        {
            SendSystemMessage(peer, "Já existe uma guilda com este nome, ou você já está em uma.");
            return;
        }

        player.GuildId = guild.Id;
        player.GuildName = guild.Name;
        _db.SaveGuild(guild.Id, guild.Name, guild.Level, guild.Xp, guild.SkillPoints);
        _db.SaveGuildMember(guild.Id, sender.Id, sender.Name, 0);
        BroadcastGuildData(sender);
        SendSystemMessage(peer, $"Guilda '{guildName}' criada com sucesso!");
    }

    private void HandleGuildInvite(NetPeer peer, PlayerSession session, Entity sender, string targetName)
    {
        if (sender is not PlayerEntity player || player.GuildId < 0)
        {
            SendSystemMessage(peer, "Você não está em uma guilda.");
            return;
        }

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null || guild.LeaderEntityId != sender.Id)
        {
            SendSystemMessage(peer, "Apenas o líder da guilda pode convidar.");
            return;
        }

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null)
        {
            SendSystemMessage(peer, $"Jogador '{targetName}' não encontrado.");
            return;
        }

        _guildInvites[target.Id] = sender.Id;
        SendSystemMessage(peer, $"Convidei {target.Name} para a guilda.");
        SendSystemMessage(targetPeer!, $"{sender.Name} convidou você para a guilda '{guild.Name}'. Digite /gaceitar para entrar.");
    }

    private void HandleGuildAccept(NetPeer peer, PlayerSession session, Entity sender)
    {
        if (sender is not PlayerEntity player) return;

        if (!_guildInvites.TryGetValue(sender.Id, out var leaderId))
        {
            SendSystemMessage(peer, "Você não tem convite de guilda pendente.");
            return;
        }
        _guildInvites.Remove(sender.Id);

        var leader = FindEntityById(leaderId, out var leaderPeer, out _);
        if (leader is not PlayerEntity leaderPlayer || leaderPlayer.GuildId < 0)
        {
            SendSystemMessage(peer, "O convite expirou.");
            return;
        }

        if (!_world.Guilds.AddMember(leaderPlayer.GuildId, sender.Id))
        {
            SendSystemMessage(peer, "Você já está em uma guilda.");
            return;
        }

        var guild = _world.Guilds.GetGuild(leaderPlayer.GuildId);
        player.GuildId = leaderPlayer.GuildId;
        player.GuildName = guild?.Name ?? "";

        if (guild != null)
        {
            _db.SaveGuildMember(guild.Id, sender.Id, sender.Name, 4);
            BroadcastGuildMemberUpdate(guild, sender.Id, sender.Name, true);
        }
    }

    private void HandleGuildLeave(Entity sender)
    {
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var guild = _world.Guilds.GetGuild(player.GuildId);
        _world.Guilds.RemoveMember(sender.Id);
        player.GuildId = -1;
        player.GuildName = "";

        if (guild != null)
        {
            _db.DeleteGuildMember(guild.Id, sender.Id);
            var remaining = _world.Guilds.GetGuild(guild.Id);
            if (remaining == null)
                _db.DeleteGuild(guild.Id);
            else
                BroadcastGuildMemberUpdate(remaining, sender.Id, sender.Name, false);
        }
    }

    private void BroadcastGuildData(Entity requester)
    {
        if (requester is not PlayerEntity player || player.GuildId < 0) return;
        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null) return;

        foreach (var eid in guild.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            WriteGuildDataPacket(peer, guild);
        }
    }

    private void WriteGuildDataPacket(NetPeer peer, Guild guild)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_GuildData);
        writer.Put(guild.Id);
        writer.Put(guild.Name);
        writer.Put((byte)guild.Members.Count);

        foreach (var eid in guild.Members.OrderBy(e => guild.GetRank(e)))
        {
            var (name, hp, maxHp, mana, maxMana, level) = GetEntityDisplayData(eid);
            writer.Put(eid);
            writer.Put(name);
            writer.Put((byte)guild.GetRank(eid));
            writer.Put(hp);
            writer.Put(maxHp);
            writer.Put(mana);
            writer.Put(maxMana);
            writer.Put(level);
        }

        writer.Put(guild.Level);
        writer.Put(guild.Xp);
        writer.Put(guild.SkillPoints);

        string[] allSkills = { "hp", "xp", "defesa", "dano", "velocidade", "drop" };
        writer.Put((byte)allSkills.Length);
        foreach (var sk in allSkills)
        {
            writer.Put(sk);
            writer.Put(guild.GetSkillLevel(sk));
        }

        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void BroadcastGuildMemberUpdate(Guild guild, ulong entityId, string name, bool joined)
    {
        int hp = 0, maxHp = 0, mana = 0, maxMana = 0, level = 1;
        foreach (var ch in _world.GetAllChannels())
        {
            var e = ch.GetEntity(entityId);
            if (e != null) { hp = e.Health; maxHp = e.MaxHealth; mana = e.Mana; maxMana = e.MaxMana; level = e.Level; break; }
        }

        foreach (var eid in guild.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_GuildMemberUpdate);
            w.Put(entityId);
            w.Put(name);
            w.Put((byte)guild.GetRank(entityId));
            w.Put(joined);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private void BroadcastGuildRankUpdate(Guild guild, ulong entityId, int newRank)
    {
        foreach (var eid in guild.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_GuildRankUpdate);
            w.Put(entityId);
            w.Put((byte)newRank);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private void BroadcastGuildSkillUpdate(Guild guild, string skillId, int newLevel)
    {
        foreach (var eid in guild.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_GuildSkillUpdate);
            w.Put(skillId);
            w.Put(newLevel);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }
}
