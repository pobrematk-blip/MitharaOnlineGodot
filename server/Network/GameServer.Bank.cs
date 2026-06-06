using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Database;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleBankDeposit(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var ch = session.SelectedCharacter;
        if (ch == null) return;

        int amount = reader.GetInt();
        if (amount <= 0)
        {
            SendBankResult(peer, false, "Valor inválido.");
            return;
        }

        if (player.Gold < amount)
        {
            SendBankResult(peer, false, "Gold insuficiente no inventário.");
            return;
        }

        player.Gold -= amount;
        ch.BankGold += amount;
        _db.SaveCharacterBankGold(ch.Id, ch.BankGold);
        _db.SaveCharacterGold(ch.Id, player.Gold);

        SendBankData(peer, player.Gold, ch.BankGold);
        SendGoldUpdate(peer, player.Gold);
        SendBankResult(peer, true, $"Depositados {amount} de ouro com sucesso!");
    }

    private void HandleBankWithdraw(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var ch = session.SelectedCharacter;
        if (ch == null) return;

        int amount = reader.GetInt();
        if (amount <= 0)
        {
            SendBankResult(peer, false, "Valor inválido.");
            return;
        }

        if (ch.BankGold < amount)
        {
            SendBankResult(peer, false, "Saldo insuficiente no banco.");
            return;
        }

        ch.BankGold -= amount;
        player.Gold += amount;
        _db.SaveCharacterBankGold(ch.Id, ch.BankGold);
        _db.SaveCharacterGold(ch.Id, player.Gold);

        SendBankData(peer, player.Gold, ch.BankGold);
        SendGoldUpdate(peer, player.Gold);
        SendBankResult(peer, true, $"Sacados {amount} de ouro com sucesso!");
    }

    private void HandleBankRequest(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var ch = session.SelectedCharacter;
        if (ch == null) return;

        SendBankData(peer, player.Gold, ch.BankGold);
        SendGoldUpdate(peer, player.Gold);
    }

    private void SendBankData(NetPeer peer, int onHandGold, int bankGold)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_BankData);
        writer.Put(onHandGold);
        writer.Put(bankGold);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendBankResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_BankResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    internal void SendGoldUpdate(NetPeer peer, int gold)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_GoldUpdate);
        writer.Put(gold);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
