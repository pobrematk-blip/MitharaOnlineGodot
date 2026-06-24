using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleLojinhaOpen(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong lojinhaEntityId = reader.GetULong();

        if (!channel.Lojinhas.TryGetValue(lojinhaEntityId, out var lojinha))
        {
            SendSystemMessage(peer, "Esta lojinha não existe mais.");
            return;
        }

        float dx = lojinha.X - player.X;
        float dy = lojinha.Y - player.Y;
        if (dx * dx + dy * dy > 300 * 300)
        {
            SendSystemMessage(peer, "Você está muito longe da lojinha.");
            return;
        }

        bool isOwner = lojinha.OwnerEntityId == player.Id;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_OpenLojinha);
        writer.Put(lojinha.Id);
        writer.Put(isOwner);
        writer.Put(lojinha.OwnerName);
        writer.Put(lojinha.Items.Count);
        foreach (var item in lojinha.Items)
        {
            writer.Put(item.Slot);
            writer.Put(item.ItemId);
            writer.Put(item.Quantity);
            writer.Put(item.PricePerUnit);
            writer.Put(item.RollData);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleLojinhaAddItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong lojinhaEntityId = reader.GetULong();
        int invSlot = reader.GetInt();
        int quantity = reader.GetInt();
        int pricePerUnit = reader.GetInt();

        if (!channel.Lojinhas.TryGetValue(lojinhaEntityId, out var lojinha))
        {
            SendSystemMessage(peer, "Esta lojinha não existe mais.");
            return;
        }

        if (lojinha.OwnerEntityId != player.Id)
        {
            SendSystemMessage(peer, "Você não é o dono desta lojinha.");
            return;
        }

        if (quantity <= 0 || pricePerUnit < 1)
        {
            SendSystemMessage(peer, "Quantidade ou preço inválido.");
            return;
        }

        if (lojinha.Items.Count >= lojinha.MaxSlots)
        {
            SendSystemMessage(peer, $"Sua lojinha está cheia (máx. {lojinha.MaxSlots} itens).");
            return;
        }

        var item = player.Items.FirstOrDefault(i => i.Slot == invSlot);
        if (item == null)
        {
            SendSystemMessage(peer, "Item não encontrado no inventário.");
            return;
        }

        if (player.Equipment.Values.Any(e => ReferenceEquals(e, item) || (item.DbId > 0 && e.DbId == item.DbId)))
        {
            SendSystemMessage(peer, "Desequipe o item antes de colocar na lojinha.");
            return;
        }

        var def = item.Definition;
        if (def == null || def.BuyPrice <= 0)
        {
            SendSystemMessage(peer, "Este item não pode ser vendido.");
            return;
        }

        int removeQty = Math.Min(quantity, item.Quantity);

        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        var lojinhaItem = new LojinhaItem
        {
            Slot = lojinha.Items.Count > 0 ? lojinha.Items.Max(x => x.Slot) + 1 : 0,
            ItemId = item.ItemId,
            Quantity = removeQty,
            PricePerUnit = pricePerUnit,
            RollData = item.Roll != null ? System.Text.Json.JsonSerializer.Serialize(item.Roll) : "",
        };

        lojinha.Items.Add(lojinhaItem);

        item.Quantity -= removeQty;
        if (item.Quantity <= 0)
        {
            player.Items.Remove(item);
            _db.DeleteItem(session.SelectedCharacter.Id, item.DbId);
        }
        else
        {
            _db.SaveItem(session.SelectedCharacter.Id, item);
        }

        _db.SaveLojinhaItems(lojinha.DbId, lojinha.Items);

        SendInventoryData(peer, player);
        SendLojinhaData(peer, lojinha, true);
        SendSystemMessage(peer, $"Item adicionado à lojinha por {pricePerUnit} gold cada.");
    }

    private void HandleLojinhaRemoveItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong lojinhaEntityId = reader.GetULong();
        int slot = reader.GetInt();

        if (!channel.Lojinhas.TryGetValue(lojinhaEntityId, out var lojinha))
        {
            SendSystemMessage(peer, "Esta lojinha não existe mais.");
            return;
        }

        if (lojinha.OwnerEntityId != player.Id)
        {
            SendSystemMessage(peer, "Você não é o dono desta lojinha.");
            return;
        }

        var lojinhaItem = lojinha.Items.FirstOrDefault(i => i.Slot == slot);
        if (lojinhaItem == null)
        {
            SendSystemMessage(peer, "Item não encontrado na lojinha.");
            return;
        }

        lojinha.Items.Remove(lojinhaItem);

        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        if (!TryAddItemToInventory(player, session.SelectedCharacter.Id, lojinhaItem.ItemId, lojinhaItem.Quantity))
        {
            SendSystemMessage(peer, "Inventário cheio! Libere espaço antes de remover.");
            lojinha.Items.Add(lojinhaItem);
            return;
        }

        _db.SaveLojinhaItems(lojinha.DbId, lojinha.Items);
        SendLojinhaData(peer, lojinha, true);
        SendInventoryData(peer, player);
        SendSystemMessage(peer, "Item removido da lojinha.");
    }

    private void HandleLojinhaBuyItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong lojinhaEntityId = reader.GetULong();
        int slot = reader.GetInt();
        int quantity = reader.GetInt();

        if (!channel.Lojinhas.TryGetValue(lojinhaEntityId, out var lojinha))
        {
            SendSystemMessage(peer, "Esta lojinha não existe mais.");
            return;
        }

        if (lojinha.OwnerEntityId == player.Id)
        {
            SendSystemMessage(peer, "Você não pode comprar da sua própria lojinha.");
            return;
        }

        if (quantity <= 0) return;

        var lojinhaItem = lojinha.Items.FirstOrDefault(i => i.Slot == slot);
        if (lojinhaItem == null)
        {
            SendSystemMessage(peer, "Item não encontrado na lojinha.");
            return;
        }

        int buyQty = Math.Min(quantity, lojinhaItem.Quantity);
        int totalCost = buyQty * lojinhaItem.PricePerUnit;

        if (player.Gold < totalCost)
        {
            SendSystemMessage(peer, $"Gold insuficiente. Precisa de {totalCost} gold.");
            return;
        }

        if (!_sessions.TryGetValue(peer, out var buySession) || buySession.SelectedCharacter == null)
            return;

        if (!TryAddItemToInventory(player, buySession.SelectedCharacter.Id, lojinhaItem.ItemId, buyQty))
        {
            SendSystemMessage(peer, "Inventário cheio.");
            return;
        }

        player.Gold -= totalCost;
        _db.SaveCharacterGold(buySession.SelectedCharacter.Id, player.Gold);

        lojinha.GoldEarned += totalCost;
        _db.UpdateLojinhaGold(lojinha.DbId, lojinha.GoldEarned);

        lojinhaItem.Quantity -= buyQty;
        if (lojinhaItem.Quantity <= 0)
            lojinha.Items.Remove(lojinhaItem);

        _db.SaveLojinhaItems(lojinha.DbId, lojinha.Items);

        SendLojinhaData(peer, lojinha, false);

        var ownerSession = _sessions.Values.FirstOrDefault(s => s.EntityId == lojinha.OwnerEntityId);
        if (ownerSession != null)
        {
            SendLojinhaData(ownerSession.Peer, lojinha, true);
            SendSystemMessage(ownerSession.Peer, $"Sua lojinha vendeu {buyQty}x por {totalCost} gold!");
        }

        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
        SendLojinhaBuyResult(peer, true, $"Comprado por {totalCost} gold!");
    }

    private void HandleLojinhaCollect(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong lojinhaEntityId = reader.GetULong();

        if (!channel.Lojinhas.TryGetValue(lojinhaEntityId, out var lojinha))
        {
            SendSystemMessage(peer, "Esta lojinha não existe mais.");
            return;
        }

        if (lojinha.OwnerEntityId != player.Id)
        {
            SendSystemMessage(peer, "Você não é o dono desta lojinha.");
            return;
        }

        if (lojinha.GoldEarned <= 0)
        {
            SendSystemMessage(peer, "Não há gold para coletar.");
            return;
        }

        if (!_sessions.TryGetValue(peer, out var session) || session.SelectedCharacter == null)
            return;

        player.Gold += lojinha.GoldEarned;
        _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);

        int collected = lojinha.GoldEarned;
        lojinha.GoldEarned = 0;
        _db.UpdateLojinhaGold(lojinha.DbId, 0);

        SendGoldUpdate(peer, player.Gold);
        SendLojinhaData(peer, lojinha, true);
        SendSystemMessage(peer, $"Coletados {collected} gold da lojinha!");
    }

    private void HandleLojinhaClose(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong lojinhaEntityId = reader.GetULong();

        if (!channel.Lojinhas.TryGetValue(lojinhaEntityId, out var lojinha))
            return;

        if (lojinha.OwnerEntityId != player.Id)
        {
            SendSystemMessage(peer, "Você não é o dono desta lojinha.");
            return;
        }

        if (lojinha.Items.Count > 0)
        {
            SendSystemMessage(peer, "Retire todos os itens antes de fechar a lojinha.");
            return;
        }

        channel.RemoveLojinha(lojinha.Id);
        _db.DeleteLojinha(lojinha.DbId);

        var w = PacketSerializer.WritePacket(PacketId.S2C_LojinhaDespawn);
        w.Put(lojinha.Id);
        foreach (var eid in channel.GetEntitiesInAoi(lojinha.X, lojinha.Y))
        {
            var p = channel.GetPlayerPeer(eid);
            p?.Send(w, DeliveryMethod.ReliableOrdered);
            w = PacketSerializer.WritePacket(PacketId.S2C_LojinhaDespawn);
            w.Put(lojinha.Id);
        }

        SendSystemMessage(peer, "Lojinha fechada.");
    }

    private void HandleLojinhaListRequest(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var allLojinhas = channel.Lojinhas.Values.ToList();

        var writer = PacketSerializer.WritePacket(PacketId.S2C_LojinhaListResult);
        writer.Put(allLojinhas.Count);
        foreach (var loja in allLojinhas)
        {
            writer.Put(loja.Id);
            writer.Put(loja.OwnerName);
            writer.Put(loja.X);
            writer.Put(loja.Y);
            writer.Put(loja.ChannelId);
            writer.Put(loja.Items.Count);
            foreach (var item in loja.Items)
            {
                var def = ItemDefinitions.Get(item.ItemId);
                writer.Put(item.Slot);
                writer.Put(item.ItemId);
                writer.Put(def?.Name ?? "Desconhecido");
                writer.Put(item.Quantity);
                writer.Put(item.PricePerUnit);
                writer.Put(item.RollData);
            }
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendLojinhaData(NetPeer peer, LojinhaEntity lojinha, bool isOwner)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_LojinhaData);
        writer.Put(lojinha.Id);
        writer.Put(isOwner);
        writer.Put(lojinha.OwnerName);
        writer.Put(lojinha.GoldEarned);
        writer.Put(lojinha.Items.Count);
        foreach (var item in lojinha.Items)
        {
            writer.Put(item.Slot);
            writer.Put(item.ItemId);
            writer.Put(item.Quantity);
            writer.Put(item.PricePerUnit);
            writer.Put(item.RollData);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendLojinhaBuyResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_LojinhaBuyResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void BroadcastLojinhaSpawn(LojinhaEntity lojinha, Channel channel)
    {
        var w = PacketSerializer.WritePacket(PacketId.S2C_LojinhaSpawn);
        w.Put(lojinha.Id);
        w.Put(lojinha.OwnerName);
        w.Put(lojinha.X);
        w.Put(lojinha.Y);
        foreach (var eid in channel.GetEntitiesInAoi(lojinha.X, lojinha.Y))
        {
            var p = channel.GetPlayerPeer(eid);
            p?.Send(w, DeliveryMethod.ReliableOrdered);
            w = PacketSerializer.WritePacket(PacketId.S2C_LojinhaSpawn);
            w.Put(lojinha.Id);
            w.Put(lojinha.OwnerName);
            w.Put(lojinha.X);
            w.Put(lojinha.Y);
        }
    }
}
