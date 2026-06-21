using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private readonly Dictionary<ulong, ulong> _duelInvites = new();

    private void HandleDuelRequestPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender is not PlayerEntity player) return;

        string targetName = reader.GetString();
        var target = FindPlayerByName(targetName, out var targetPeer, out _);
        if (target == null || targetPeer == null)
        {
            SendSystemMessage(peer, $"Jogador '{targetName}' não encontrado.");
            return;
        }

        if (target.Id == player.Id)
        {
            SendSystemMessage(peer, "Você não pode duelar consigo mesmo.");
            return;
        }

        _duelInvites[target.Id] = player.Id;

        var w = PacketSerializer.WritePacket(PacketId.S2C_DuelRequested);
        w.Put(sender.Name);
        targetPeer.Send(w, DeliveryMethod.ReliableOrdered);

        SendSystemMessage(peer, $"Você desafiou {target.Name} para um duelo!");
    }

    private void HandleDuelAcceptPacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender is not PlayerEntity player) return;

        if (!_duelInvites.TryGetValue(player.Id, out var challengerId))
        {
            SendSystemMessage(peer, "Você não tem um convite de duelo pendente.");
            return;
        }
        _duelInvites.Remove(player.Id);

        var challenger = FindEntityById(challengerId, out var challengerPeer, out _);
        if (challenger == null || challengerPeer == null)
        {
            SendSystemMessage(peer, "O desafiante está offline.");
            return;
        }

        var w1 = PacketSerializer.WritePacket(PacketId.S2C_DuelStart);
        w1.Put(player.Id);
        w1.Put(sender.Name);
        challengerPeer.Send(w1, DeliveryMethod.ReliableOrdered);

        var w2 = PacketSerializer.WritePacket(PacketId.S2C_DuelStart);
        w2.Put(challengerId);
        w2.Put(challenger.Name);
        peer.Send(w2, DeliveryMethod.ReliableOrdered);

        Logger.Info($"[DUEL] {sender.Name} vs {challenger.Name} iniciado!");
    }

    private void HandleDuelDeclinePacket(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var sender, out _) || sender is not PlayerEntity player) return;

        if (!_duelInvites.TryGetValue(player.Id, out var challengerId))
            return;
        _duelInvites.Remove(player.Id);

        var challengerPeer = FindPeerByEntityId(challengerId);
        if (challengerPeer != null)
            SendSystemMessage(challengerPeer, $"{sender.Name} recusou seu duelo.");
    }
}
