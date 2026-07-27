using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Database;
using Mithara.Server.Entities;
using Mithara.Server.Packets;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const int BankSlotBase = 1000;
    private const int BankSlotCount = 30;

    private void HandleBankDeposit(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var ch = session.SelectedCharacter;
        if (ch == null) return;

        int amount = reader.GetInt();
        if (amount <= 0)
        {
            SendBankResult(peer, false, "Valor invalido.");
            return;
        }

        if (player.Gold < amount)
        {
            SendBankResult(peer, false, "Gold insuficiente no inventario.");
            return;
        }

        player.Gold -= amount;
        ch.BankGold += amount;
        _db.SaveCharacterBankGold(ch.Id, ch.BankGold);
        _db.SaveCharacterGold(ch.Id, player.Gold);

        SendBankData(peer, player.Gold, ch.BankGold, ch.Id);
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
            SendBankResult(peer, false, "Valor invalido.");
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

        SendBankData(peer, player.Gold, ch.BankGold, ch.Id);
        SendGoldUpdate(peer, player.Gold);
        SendBankResult(peer, true, $"Sacados {amount} de ouro com sucesso!");
    }

    private void HandleBankRequest(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var ch = session.SelectedCharacter;
        if (ch == null) return;

        SendBankData(peer, player.Gold, ch.BankGold, ch.Id);
        SendGoldUpdate(peer, player.Gold);
    }

    private void HandleBankDepositItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        int invSlot = reader.GetInt();
        int bankSlot = reader.GetInt();
        if (!IsValidBankSlot(bankSlot))
        {
            SendBankResult(peer, false, "Slot do banco invalido.");
            return;
        }

        var item = player.Items.FirstOrDefault(i => i.Slot == invSlot);
        if (item == null)
        {
            SendBankResult(peer, false, "Item nao encontrado no inventario.");
            return;
        }

        if (player.Equipment.Values.Any(e => ReferenceEquals(e, item) || (item.DbId > 0 && e.DbId == item.DbId)))
        {
            SendBankResult(peer, false, "Desequipe o item antes de guardar no banco.");
            return;
        }

        var bankItems = _db.LoadBankItems(session.SelectedCharacter.Id);
        var target = bankItems.FirstOrDefault(i => i.Slot == bankSlot);
        if (target != null)
        {
            SendBankResult(peer, false, "Este slot do banco ja esta ocupado.");
            return;
        }

        player.Items.Remove(item);
        item.Slot = BankSlotBase + bankSlot;
        _db.MoveItemToSlot(session.SelectedCharacter.Id, item, item.Slot);

        SendInventoryData(peer, player);
        SendBankData(peer, player.Gold, session.SelectedCharacter.BankGold, session.SelectedCharacter.Id);
        SendBankResult(peer, true, "Item depositado no banco.");
    }

    private void HandleBankWithdrawItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        int bankSlot = reader.GetInt();
        int invSlot = reader.GetInt();
        if (!IsValidBankSlot(bankSlot) || invSlot < 0 || invSlot >= 40)
        {
            SendBankResult(peer, false, "Slot invalido.");
            return;
        }

        if (player.Items.Any(i => i.Slot == invSlot))
        {
            SendBankResult(peer, false, "Escolha um slot vazio do inventario.");
            return;
        }

        var bankItem = _db.LoadBankItems(session.SelectedCharacter.Id).FirstOrDefault(i => i.Slot == bankSlot);
        if (bankItem == null)
        {
            SendBankResult(peer, false, "Item nao encontrado no banco.");
            return;
        }

        bankItem.Slot = invSlot;
        _db.MoveItemToSlot(session.SelectedCharacter.Id, bankItem, invSlot);
        player.Items.Add(bankItem);

        SendInventoryData(peer, player);
        SendBankData(peer, player.Gold, session.SelectedCharacter.BankGold, session.SelectedCharacter.Id);
        SendBankResult(peer, true, "Item retirado do banco.");
    }

    private void HandleBankMoveItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out _)) return;
        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null) return;

        int fromSlot = reader.GetInt();
        int toSlot = reader.GetInt();
        if (!IsValidBankSlot(fromSlot) || !IsValidBankSlot(toSlot) || fromSlot == toSlot)
            return;

        var bankItems = _db.LoadBankItems(session.SelectedCharacter.Id);
        var fromItem = bankItems.FirstOrDefault(i => i.Slot == fromSlot);
        var toItem = bankItems.FirstOrDefault(i => i.Slot == toSlot);
        if (fromItem == null) return;

        if (toItem != null)
        {
            fromItem.Slot = BankSlotBase + toSlot;
            toItem.Slot = BankSlotBase + fromSlot;
            _db.SwapItemSlots(session.SelectedCharacter.Id, fromItem, BankSlotBase + toSlot, toItem, BankSlotBase + fromSlot);
        }
        else
        {
            fromItem.Slot = BankSlotBase + toSlot;
            _db.MoveItemToSlot(session.SelectedCharacter.Id, fromItem, BankSlotBase + toSlot);
        }

        SendBankData(peer, player.Gold, session.SelectedCharacter.BankGold, session.SelectedCharacter.Id);
    }

    private void SendBankData(NetPeer peer, int onHandGold, int bankGold, int characterId)
    {
        var bankItems = _db.LoadBankItems(characterId);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_BankData);
        writer.Put(onHandGold);
        writer.Put(bankGold);
        writer.Put(bankItems.Count);
        foreach (var item in bankItems)
        {
            writer.Put(item.Slot);
            writer.Put(item.ItemId);
            writer.Put(item.Quantity);
            writer.Put(item.RefineLevel);
            writer.Put(System.Text.Json.JsonSerializer.Serialize(item.Roll));
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private static bool IsValidBankSlot(int slot)
    {
        return slot >= 0 && slot < BankSlotCount;
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
