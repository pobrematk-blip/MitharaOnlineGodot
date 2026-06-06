using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private static ItemDefinition? GetItemDef(int id) => ItemDefinitions.Get(id);
    private const float NpcInteractionRange = 100f;

    private void HandleNpcInteract(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        ulong npcEntityId = reader.GetULong();
        var npcEntity = channel.GetEntity(npcEntityId) as NPCEntity;
        if (npcEntity == null || npcEntity.Health <= 0) return;

        float dx = npcEntity.X - player.X;
        float dy = npcEntity.Y - player.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > NpcInteractionRange) return;

        // Find NPC template and dialog
        var template = _world.Npcs.GetTemplate(npcEntity.PrefabId);
        string dialogId = npcEntity.DialogId;
        if (!string.IsNullOrEmpty(template?.DialogId))
            dialogId = template.DialogId;

        var dialog = _world.Npcs.GetDialog(dialogId);
        if (dialog == null)
        {
            SendNpcDialog(peer, "O NPC não responde...", new List<(string text, string action, string data)>());
            return;
        }

        var options = dialog.Options.Select(o => (o.Text, o.Action, o.ActionData)).ToList();
        SendNpcDialog(peer, dialog.Text, options);
    }

    private void HandleNpcSelectOption(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        string action = reader.GetString();
        string actionData = reader.GetString();

        switch (action)
        {
            case "goto":
                var dialog = _world.Npcs.GetDialog(actionData);
                if (dialog == null)
                {
                    SendNpcDialog(peer, "...", new List<(string, string, string)>());
                    return;
                }
                var options = dialog.Options.Select(o => (o.Text, o.Action, o.ActionData)).ToList();
                SendNpcDialog(peer, dialog.Text, options);
                break;

            case "shop":
                // Find the NPC first to get shopId
                string shopId = actionData;
                // Try to find shop by the shop data passed as action data
                var shop = _world.Npcs.GetShop(shopId);
                if (shop == null)
                {
                    SendNpcDialog(peer, "A loja está fechada.", new List<(string, string, string)>());
                    return;
                }
                SendNpcShopItems(peer, shopId, shop);
                break;

            case "bank":
                if (!_sessions.TryGetValue(peer, out var session)) return;
                var ch = session.SelectedCharacter;
                if (ch == null) return;
                int bankGold = ch.BankGold;
                SendBankData(peer, player.Gold, bankGold);
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                break;

            case "close":
                SendNpcDialog(peer, "", new List<(string, string, string)>());
                break;
        }
    }

    private void HandleNpcBuyItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        string shopId = reader.GetString();
        int itemId = reader.GetInt();
        int quantity = reader.GetInt();

        var shop = _world.Npcs.GetShop(shopId);
        if (shop == null)
        {
            SendNpcBuyResult(peer, false, "Loja não encontrada.");
            return;
        }

        var entry = shop.FirstOrDefault(e => e.ItemId == itemId);
        if (entry == null)
        {
            SendNpcBuyResult(peer, false, "Item não encontrado na loja.");
            return;
        }

        if (entry.Stock > 0 && entry.Stock < quantity)
        {
            SendNpcBuyResult(peer, false, "Estoque insuficiente.");
            return;
        }

        int totalCost = entry.Price * quantity;
        if (player.Gold < totalCost)
        {
            SendNpcBuyResult(peer, false, "Gold insuficiente.");
            return;
        }

        int slot = player.FindEmptyInventorySlot();
        if (slot < 0)
        {
            SendNpcBuyResult(peer, false, "Inventário cheio.");
            return;
        }

        player.Gold -= totalCost;
        if (_sessions.TryGetValue(peer, out var buySession) && buySession.SelectedCharacter != null)
            _db.SaveCharacterGold(buySession.SelectedCharacter.Id, player.Gold);
        player.Items.Add(new ItemInstance { Slot = slot, ItemId = itemId, Quantity = quantity });

        if (entry.Stock > 0)
            entry.Stock -= quantity;

        SendNpcBuyResult(peer, true, "Compra realizada!");
        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
    }

    private void HandleNpcSellItem(NetPeer peer, NetDataReader reader)
    {
        if (!TryGetPlayer(peer, out var player, out var channel)) return;

        int slot = reader.GetInt();
        int quantity = reader.GetInt();

        var item = player.Items.FirstOrDefault(i => i.Slot == slot);
        if (item == null)
        {
            SendNpcSellResult(peer, false, "Item não encontrado.");
            return;
        }

        var itemDef = GetItemDef(item.ItemId);
        int sellPrice = (itemDef?.BuyPrice ?? 20) / 4;
        if (sellPrice < 1) sellPrice = 1;

        int totalGold = sellPrice * quantity;
        player.Gold += totalGold;
        if (_sessions.TryGetValue(peer, out var sellSession) && sellSession.SelectedCharacter != null)
            _db.SaveCharacterGold(sellSession.SelectedCharacter.Id, player.Gold);

        if (quantity >= item.Quantity)
            player.Items.Remove(item);
        else
            item.Quantity -= quantity;

        SendNpcSellResult(peer, true, $"Vendido por {totalGold} gold!");
        SendInventoryData(peer, player);
        SendGoldUpdate(peer, player.Gold);
    }

    private void SendNpcDialog(NetPeer peer, string text, List<(string text, string action, string data)> options)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcDialog);
        writer.Put(text);
        writer.Put(options.Count);
        foreach (var opt in options)
        {
            writer.Put(opt.text);
            writer.Put(opt.action);
            writer.Put(opt.data);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendNpcShopItems(NetPeer peer, string shopId, List<ShopEntry> items)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcShopItems);
        writer.Put(shopId);
        writer.Put(items.Count);
        foreach (var entry in items)
        {
            writer.Put(entry.ItemId);
            writer.Put(entry.Price);
            writer.Put(entry.Stock);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendNpcBuyResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcBuyResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void SendNpcSellResult(NetPeer peer, bool success, string message)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_NpcSellResult);
        writer.Put(success);
        writer.Put(message);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
}
