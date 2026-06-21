using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandlePartyInvitePacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _) || sender == null) return;
        HandlePartyInvite(peer, _sessions[peer], sender, targetName);
    }

    private void HandlePartyAcceptPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender == null) return;
        HandlePartyAccept(peer, _sessions[peer], sender);
    }

    private void HandlePartyLeavePacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender == null) return;
        HandlePartyLeave(sender);
    }

    private void HandlePartyKickPacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.PartyId < 0) return;

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null) { SendSystemMessage(peer, "Jogador não encontrado."); return; }

        var party = _world.Parties.GetParty(player.PartyId);
        if (party == null || party.LeaderEntityId != sender.Id)
        { SendSystemMessage(peer, "Apenas o líder pode expulsar."); return; }

        if (!party.Members.Contains(target.Id))
        { SendSystemMessage(peer, "Este jogador não está no grupo."); return; }

        BroadcastPartyMemberUpdate(party, target.Id, target.Name, false);
        _world.Parties.RemoveMember(target.Id);
        target.PartyId = -1;
        SendSystemMessage(peer, $"{target.Name} foi expulso do grupo.");
        if (targetPeer != null)
            SendSystemMessage(targetPeer, "Você foi expulso do grupo.");
    }

    private void HandlePartyPromotePacket(NetPeer peer, NetDataReader reader)
    {
        string targetName = reader.GetString();
        if (!TryGetPlayer(peer, out var sender, out _)) return;
        if (sender is not PlayerEntity player || player.PartyId < 0) return;

        var party = _world.Parties.GetParty(player.PartyId);
        if (party == null || party.LeaderEntityId != sender.Id)
        { SendSystemMessage(peer, "Apenas o líder pode transferir liderança."); return; }

        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || !party.Members.Contains(target.Id))
        { SendSystemMessage(peer, "Jogador não encontrado no grupo."); return; }

        party.LeaderEntityId = target.Id;
        BroadcastPartyLeaderUpdate(party, target.Id);
        SendSystemMessage(peer, $"Liderança transferida para {target.Name}.");
    }

    private void HandlePartyInvite(NetPeer peer, PlayerSession session, Entity sender, string targetName)
    {
        if (sender is not PlayerEntity player) return;

        var target = FindPlayerByName(targetName, out var targetPeer, out var _);
        if (target == null)
        {
            SendSystemMessage(peer, $"Jogador '{targetName}' não encontrado.");
            return;
        }

        _partyInvites[target.Id] = sender.Id;
        SendSystemMessage(peer, $"Convidei {target.Name} para o grupo.");

        var notify = PacketSerializer.WritePacket(PacketId.S2C_PartyInviteReceived);
        notify.Put(sender.Name);
        targetPeer!.Send(notify, DeliveryMethod.ReliableOrdered);
    }

    private void HandlePartyAccept(NetPeer peer, PlayerSession session, Entity sender)
    {
        if (sender is not PlayerEntity player) return;

        if (!_partyInvites.TryGetValue(sender.Id, out var leaderId))
        {
            SendSystemMessage(peer, "Você não tem convite de grupo pendente.");
            return;
        }
        _partyInvites.Remove(sender.Id);

        var leader = FindEntityById(leaderId, out var leaderPeer, out _);
        if (leader == null)
        {
            SendSystemMessage(peer, "O convite expirou (jogador offline).");
            return;
        }

        var party = _world.Parties.GetPlayerPartyId(leaderId) is int partyId
            ? _world.Parties.GetParty(partyId)
            : _world.Parties.CreateParty(leaderId);

        if (party == null)
        {
            SendSystemMessage(peer, "Não foi possível entrar no grupo.");
            return;
        }

        if (!_world.Parties.AddMember(party.Id, sender.Id))
        {
            SendSystemMessage(peer, "Você já está em um grupo.");
            return;
        }

        player.PartyId = party.Id;
        if (sender is PlayerEntity p) p.PartyId = party.Id;

        BroadcastPartyMemberUpdate(party, sender.Id, sender.Name, true);
    }

    private void HandlePartyLeave(Entity sender)
    {
        if (sender is not PlayerEntity player || player.PartyId < 0) return;

        int partyId = player.PartyId;
        string senderName = sender.Name;
        _world.Parties.RemoveMember(sender.Id);
        player.PartyId = -1;

        var party = _world.Parties.GetParty(partyId);
        if (party != null)
        {
            BroadcastPartyMemberUpdate(party, sender.Id, senderName, false);
        }
        else
        {
            var selfPeer = FindPeerByEntityId(sender.Id);
            if (selfPeer != null)
            {
                var w = PacketSerializer.WritePacket(PacketId.S2C_PartyData);
                w.Put(0);
                w.Put((byte)0);
                selfPeer.Send(w, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    private void SendPartyDataToPeer(NetPeer peer, Party party)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_PartyData);
        writer.Put(party.Id);
        writer.Put((byte)party.Members.Count);

        foreach (var eid in party.Members)
        {
            writer.Put(eid);
            var (name, hp, maxHp, mana, maxMana, level) = GetEntityDisplayData(eid);
            writer.Put(name);
            writer.Put(eid == party.LeaderEntityId);
            writer.Put(hp);
            writer.Put(maxHp);
            writer.Put(mana);
            writer.Put(maxMana);
            writer.Put(level);
        }

        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void BroadcastPartyData(Party party)
    {
        foreach (var eid in party.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer != null)
                SendPartyDataToPeer(peer, party);
        }
    }

    private void BroadcastPartyMemberUpdate(Party party, ulong entityId, string name, bool joined)
    {
        int hp = 0, maxHp = 0, mana = 0, maxMana = 0, level = 1;
        foreach (var ch in _world.GetAllChannels())
        {
            var e = ch.GetEntity(entityId);
            if (e != null)
            {
                hp = e.Health; maxHp = e.MaxHealth; mana = e.Mana; maxMana = e.MaxMana; level = e.Level;
                break;
            }
        }

        foreach (var eid in party.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_PartyMemberUpdate);
            w.Put(entityId);
            w.Put(name);
            w.Put(hp);
            w.Put(maxHp);
            w.Put(mana);
            w.Put(maxMana);
            w.Put(level);
            w.Put(joined);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }

    private void BroadcastPartyLeaderUpdate(Party party, ulong newLeaderId)
    {
        foreach (var eid in party.Members)
        {
            var peer = FindPeerByEntityId(eid);
            if (peer == null) continue;
            var w = PacketSerializer.WritePacket(PacketId.S2C_PartyLeaderUpdate);
            w.Put(newLeaderId);
            peer.Send(w, DeliveryMethod.ReliableOrdered);
        }
    }
}
