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
        string tag = reader.GetString();
        int emblem = reader.GetInt();
        if (!TryGetPlayer(peer, out var sender, out _) || sender == null) return;
        bool success = HandleGuildCreate(peer, sender, name, tag, emblem);
        int guildId = success && sender.GuildId >= 0 ? sender.GuildId : -1;
        SendGuildCreateResult(peer, guildId, success, success
            ? $"Guilda '{name}' criada com sucesso!"
            : "Não foi possível criar a guilda.");
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
        SendGuildClear(peer);
    }

    private void HandleGuildKickPacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.GuildId < 0) return;

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null) return;

        int actorRank = guild.GetRank(sender.Id);
        if (actorRank > 2)
        { SendSystemMessage(peer, "Apenas lider, capitao ou oficial podem expulsar."); return; }

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || !guild.Members.Contains(target.Id))
        { SendSystemMessage(peer, "Jogador não encontrado na guilda."); return; }

        if (target.Id == guild.LeaderEntityId)
        { SendSystemMessage(peer, "Você não pode expulsar a si mesmo."); return; }

        int targetRank = guild.GetRank(target.Id);
        if (actorRank != 0 && targetRank <= actorRank)
        { SendSystemMessage(peer, "Voce nao pode expulsar alguem de cargo igual ou maior."); return; }

        _db.DeleteGuildMember(guild.Id, target.Id);
        BroadcastGuildMemberUpdate(guild, target.Id, target.Name, false);
        _world.Guilds.RemoveMember(target.Id);
        target.GuildId = -1;
        target.GuildName = "";
        SendSystemMessage(peer, $"{target.Name} foi expulso da guilda.");
        if (targetPeer != null)
        {
            SendGuildClear(targetPeer);
            SendSystemMessage(targetPeer, "Você foi expulso da guilda.");
        }
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

        // Promover para líder (rank 0) requer confirmação do alvo
        if (current == 1)
        {
            if (targetPeer == null)
            { SendSystemMessage(peer, "O jogador alvo está offline."); return; }

            _guildLeaderPromotions[target.Id] = new GuildLeaderPromotion(sender.Id, guild.Id);
            SendSystemMessage(peer, $"Convite de liderança enviado para {target.Name}. Aguardando aceitação.");

            var notify = PacketSerializer.WritePacket(PacketId.S2C_GuildPromoteLeaderRequest);
            notify.Put(sender.Name);
            notify.Put(guild.Name);
            targetPeer.Send(notify, DeliveryMethod.ReliableOrdered);
            return;
        }

        if (current <= 0)
        { SendSystemMessage(peer, "Este cargo já é o máximo."); return; }

        guild.SetRank(target.Id, current - 1);
        _db.SaveGuildMember(guild.Id, target.Id, target.Name, current - 1);
        BroadcastGuildRankUpdate(guild, target.Id, current - 1);
        SendSystemMessage(peer, $"{target.Name} promovido para cargo {current - 1}.");
    }

    private void HandleGuildPromoteLeaderAcceptPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player) return;

        if (!_guildLeaderPromotions.TryGetValue(sender.Id, out var promotion))
        { SendSystemMessage(peer, "Você não tem convite de liderança pendente."); return; }

        _guildLeaderPromotions.Remove(sender.Id);

        var guild = _world.Guilds.GetGuild(promotion.GuildId);
        if (guild == null)
        { SendSystemMessage(peer, "A guilda não existe mais."); return; }

        var oldLeader = FindEntityById(promotion.LeaderId, out var leaderPeer, out _);
        if (oldLeader == null)
        { SendSystemMessage(peer, "O líder atual está offline."); return; }

        guild.SetRank(promotion.LeaderId, 1);
        guild.SetRank(sender.Id, 0);
        guild.LeaderEntityId = sender.Id;
        _db.SaveGuildMember(guild.Id, promotion.LeaderId, oldLeader.Name, 1);
        _db.SaveGuildMember(guild.Id, sender.Id, sender.Name, 0);
        BroadcastGuildRankUpdate(guild, promotion.LeaderId, 1);
        BroadcastGuildRankUpdate(guild, sender.Id, 0);
        BroadcastGuildData(sender);
        SendSystemMessage(peer, $"Você agora é o líder da guilda '{guild.Name}'!");
        if (leaderPeer != null)
            SendSystemMessage(leaderPeer, $"{player.Name} aceitou a liderança da guilda.");
    }

    private void HandleGuildPromoteLeaderDeclinePacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player) return;

        if (!_guildLeaderPromotions.TryGetValue(sender.Id, out var promotion))
        { SendSystemMessage(peer, "Você não tem convite de liderança pendente."); return; }

        _guildLeaderPromotions.Remove(sender.Id);

        var leader = FindPeerByEntityId(promotion.LeaderId);
        if (leader != null)
            SendSystemMessage(leader, $"{player.Name} recusou a liderança da guilda.");
        SendSystemMessage(peer, "Você recusou a liderança da guilda.");
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

    private bool HandleGuildCreate(NetPeer peer, Entity sender, string guildName, string tag = "", int emblem = -1)
    {
        if (sender is not PlayerEntity player) return false;

        Logger.Info($"[GUILD] HandleGuildCreate: player={sender.Name}, guildName={guildName}, tag={tag}, emblem={emblem}, gold={player.Gold}, itemsCount={player.Items.Count}");
        foreach (var item in player.Items)
            Logger.Info($"[GUILD] Item: DbId={item.DbId}, ItemId={item.ItemId}, Slot={item.Slot}, Qty={item.Quantity}");

        if (player.GuildId >= 0)
        {
            SendSystemMessage(peer, "Você já está em uma guilda.");
            return false;
        }

        if (guildName.Length < 2 || guildName.Length > 30)
        {
            SendSystemMessage(peer, "O nome da guilda deve ter entre 2 e 30 caracteres.");
            return false;
        }

        const int custoGold = 10000;
        var pergaminho = player.Items.FirstOrDefault(i => i.ItemId == ItemDefinitions.PergaminhoCriacaoCla && i.Quantity > 0);
        bool temPergaminho = pergaminho != null;
        Logger.Info($"[GUILD] temPergaminho={temPergaminho}, gold={player.Gold}, custoGold={custoGold}");

        if (player.Gold < custoGold && !temPergaminho)
        {
            SendSystemMessage(peer, $"Você precisa de {custoGold:N0} moedas de ouro ou um Pergaminho de Criação de Clã.");
            return false;
        }

        var guild = _world.Guilds.CreateGuild(guildName, sender.Id, tag, emblem);
        if (guild == null)
        {
            SendSystemMessage(peer, "Já existe uma guilda com este nome.");
            return false;
        }

        if (temPergaminho)
        {
            pergaminho!.Quantity--;
            Logger.Info($"[GUILD] Pergaminho consumido! Qtd restante: {pergaminho.Quantity}");
            if (pergaminho.Quantity <= 0)
            {
                player.Items.Remove(pergaminho);
                if (_sessions.TryGetValue(peer, out var session) && session.SelectedCharacter != null)
                    _db.DeleteItem(session.SelectedCharacter.Id, pergaminho.DbId);
                Logger.Info("[GUILD] Pergaminho removido do inventario e DB!");
            }
            else
            {
                if (_sessions.TryGetValue(peer, out var session) && session.SelectedCharacter != null)
                    _db.SaveItem(session.SelectedCharacter.Id, pergaminho);
            }
            SendInventoryData(peer, player);
        }
        else
        {
            player.Gold -= custoGold;
            if (_sessions.TryGetValue(peer, out var session) && session.SelectedCharacter != null)
                _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);
            SendGoldUpdate(peer, player.Gold);
        }

        player.GuildId = guild.Id;
        player.GuildName = guild.Name;
        _db.SaveGuild(guild.Id, guild.Name, guild.Level, guild.Xp, guild.SkillPoints);
        _db.SaveGuildMember(guild.Id, sender.Id, sender.Name, 0);
        BroadcastGuildData(sender);
        SendSystemMessage(peer, $"Guilda '{guildName}' criada com sucesso!");
        Logger.Info($"[GUILD] Guild '{guildName}' criada com sucesso por {sender.Name}!");
        return true;
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

        var notify = PacketSerializer.WritePacket(PacketId.S2C_GuildInviteReceived);
        notify.Put(sender.Name);
        targetPeer!.Send(notify, DeliveryMethod.ReliableOrdered);
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
        writer.Put(guild.Tag);
        writer.Put(guild.Emblem);
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

    private void HandleGuildDisband(NetPeer peer, PlayerEntity player)
    {
        Logger.Info($"[GUILD] HandleGuildDisband: player={player.Name}, guildId={player.GuildId}");

        if (player.GuildId < 0)
        {
            SendSystemMessage(peer, "Você não está em uma guilda.");
            SendNpcDialog(peer, "Você não está em uma guilda.", new List<(string, string, string)>());
            return;
        }

        var guild = _world.Guilds.GetGuild(player.GuildId);
        if (guild == null)
        {
            SendSystemMessage(peer, "Guilda não encontrada.");
            return;
        }

        if (guild.LeaderEntityId != player.Id)
        {
            SendSystemMessage(peer, "Apenas o líder pode dissolver a guilda.");
            SendNpcDialog(peer, "Apenas o líder pode dissolver a guilda.", new List<(string, string, string)>());
            return;
        }

        if (guild.Members.Count > 1)
        {
            SendSystemMessage(peer, "Remova todos os membros antes de dissolver a guilda.");
            SendNpcDialog(peer, "Remova todos os membros antes de dissolver a guilda.", new List<(string, string, string)>());
            return;
        }

        _db.DeleteGuildMember(guild.Id, player.Id);
        _db.DeleteGuild(guild.Id);
        _world.Guilds.RemoveGuild(guild.Id);
        player.GuildId = -1;
        player.GuildName = "";

        SendGuildClear(peer);
        SendSystemMessage(peer, $"A guilda '{guild.Name}' foi dissolvida.");
        SendNpcDialog(peer, $"A guilda '{guild.Name}' foi dissolvida.", new List<(string, string, string)>());
        Logger.Info($"[GUILD] Guild '{guild.Name}' dissolvida por {player.Name}!");
    }

    private void SendGuildClear(NetPeer peer)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_GuildClear);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendGuildCreateResult(NetPeer peer, int guildId, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_GuildCreateResult);
        writer.Put(guildId);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleGuildEnterBase(NetPeer peer, PlayerEntity player, Channel channel)
    {
        if (player.GuildId < 0)
        {
            SendSystemMessage(peer, "Você não está em uma guilda.");
            SendNpcDialog(peer, "Você não está em uma guilda.", new List<(string, string, string)>());
            return;
        }

        float baseX = 500, baseY = 500;
        channel.MoveEntity(player.Id, baseX, baseY);
        SendTeleportPlayer(peer, player.Id, baseX, baseY);

        if (_sessions.TryGetValue(peer, out var session) && session.SelectedCharacter != null)
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, baseX, baseY);

        SendNpcDialog(peer, "Bem-vindo à base da guilda!", new List<(string, string, string)>());
        Logger.Info($"[GUILD] {player.Name} entrou na base da guilda.");
    }

    private void HandleGuildEnterGvg(NetPeer peer, PlayerEntity player, Channel channel)
    {
        if (player.GuildId < 0)
        {
            SendSystemMessage(peer, "Você não está em uma guilda.");
            SendNpcDialog(peer, "Você não está em uma guilda.", new List<(string, string, string)>());
            return;
        }

        float gvgX = 700, gvgY = 700;
        channel.MoveEntity(player.Id, gvgX, gvgY);
        SendTeleportPlayer(peer, player.Id, gvgX, gvgY);

        if (_sessions.TryGetValue(peer, out var session) && session.SelectedCharacter != null)
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, gvgX, gvgY);

        SendNpcDialog(peer, "Bem-vindo à arena GvG!", new List<(string, string, string)>());
        Logger.Info($"[GUILD] {player.Name} entrou na GvG.");
    }

    private void SendTeleportPlayer(NetPeer peer, ulong entityId, float x, float y)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_Teleport);
        writer.Put(entityId);
        writer.Put(x);
        writer.Put(y);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
