using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void SendInventoryData(NetPeer peer, PlayerEntity player)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_InventoryData);
        writer.Put(player.Items.Count);
        foreach (var item in player.Items)
        {
            writer.Put(item.Slot);
            writer.Put(item.ItemId);
            writer.Put(item.Quantity);
            writer.Put(item.RefineLevel);
        }
        writer.Put(player.Equipment.Count);
        foreach (var kv in player.Equipment)
        {
            writer.Put(kv.Key);
            writer.Put(kv.Value.ItemId);
            writer.Put(kv.Value.Quantity);
            writer.Put(kv.Value.RefineLevel);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private bool TryAddItemToInventory(PlayerEntity player, int characterId, int itemId, int quantity)
    {
        if (quantity <= 0) return false;

        var def = ItemDefinitions.Get(itemId);
        bool stackable = def?.IsStackable == true && def.MaxStack > 1;
        int maxStack = stackable ? Math.Max(1, def!.MaxStack) : 1;
        int remaining = quantity;

        var stackAdds = new List<(ItemInstance item, int amount)>();
        if (stackable)
        {
            foreach (var existing in player.Items.Where(i => i.ItemId == itemId && i.Quantity < maxStack).OrderBy(i => i.Slot))
            {
                int add = Math.Min(remaining, maxStack - existing.Quantity);
                if (add <= 0) continue;
                stackAdds.Add((existing, add));
                remaining -= add;
                if (remaining <= 0) break;
            }
        }

        var usedSlots = new HashSet<int>(player.Items.Select(i => i.Slot));
        var newSlots = new List<int>();
        int newStacksNeeded = stackable
            ? (int)Math.Ceiling(remaining / (double)maxStack)
            : remaining;

        for (int slot = 0; slot < 40 && newSlots.Count < newStacksNeeded; slot++)
        {
            if (usedSlots.Contains(slot)) continue;
            usedSlots.Add(slot);
            newSlots.Add(slot);
        }

        if (newSlots.Count < newStacksNeeded)
            return false;

        foreach (var (item, amount) in stackAdds)
        {
            item.Quantity += amount;
            _db.SaveItem(characterId, item);
        }

        remaining = quantity - stackAdds.Sum(x => x.amount);
        foreach (int slot in newSlots)
        {
            if (remaining <= 0) break;

            int amount = stackable ? Math.Min(remaining, maxStack) : 1;
            var newItem = new ItemInstance { Slot = slot, ItemId = itemId, Quantity = amount };
            player.Items.Add(newItem);
            _db.SaveItem(characterId, newItem);
            remaining -= amount;
        }

        return true;
    }

    private void HandleInventoryRequest(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        SendInventoryData(peer, player);
    }

    private void HandleEquipItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int invSlot = reader.GetInt();
        int equipSlot = reader.GetInt();

        var sourceItem = player.Items.FirstOrDefault(i => i.Slot == invSlot);
        if (sourceItem == null) return;
        var def = sourceItem.Definition;
        if (def == null) return;

        int eqType = def.Type switch
        {
            ItemType.Helmet => 0,
            ItemType.Chestplate => 1,
            ItemType.Belt => 3,
            ItemType.Gloves => 3,
            ItemType.Pants => 4,
            ItemType.Boots => 5,
            ItemType.Weapon => 6,
            ItemType.Shield => 7,
            ItemType.Necklace => 8,
            ItemType.Ring => 9,
            ItemType.Earring => 10,
            ItemType.Rune => 11,
            ItemType.Wing => 12,
            ItemType.Mount => 13,
            ItemType.Pet => 14,
            ItemType.Skin => 15,
            _ => -1,
        };
        if (eqType != equipSlot) return;

        player.Equipment.TryGetValue(equipSlot, out var currentEquipped);

        player.Items.Remove(sourceItem);
        sourceItem.Slot = 100 + equipSlot;
        player.Equipment[equipSlot] = sourceItem;

        if (currentEquipped != null)
        {
            currentEquipped.Slot = invSlot;
            player.Items.Add(currentEquipped);
            _db.DeleteItemBySlot(session.SelectedCharacter!.Id, 100 + equipSlot);
            _db.SaveItem(session.SelectedCharacter.Id, currentEquipped);
        }
        else
        {
            _db.DeleteItemBySlot(session.SelectedCharacter!.Id, invSlot);
        }
        _db.SaveItem(session.SelectedCharacter.Id, sourceItem);

        RecalculatePlayerStats(player);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_EquipUpdate);
        writer.Put(equipSlot);
        writer.Put(sourceItem.ItemId);
        writer.Put(sourceItem.Quantity);
        writer.Put(sourceItem.RefineLevel);
        if (currentEquipped != null)
        {
            writer.Put(true);
            writer.Put(currentEquipped.Slot);
            writer.Put(currentEquipped.ItemId);
            writer.Put(currentEquipped.Quantity);
            writer.Put(currentEquipped.RefineLevel);
        }
        else
        {
            writer.Put(false);
            writer.Put(invSlot);
            writer.Put(0);
            writer.Put(0);
            writer.Put(0);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        var aoi = channel.GetEntitiesInAoi(player.X, player.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null && eid != session.EntityId)
            {
                var visWriter = PacketSerializer.WritePacket(PacketId.S2C_EntityUpdate);
                visWriter.Put(1);
                visWriter.Put(session.EntityId);
                visWriter.Put(player.X);
                visWriter.Put(player.Y);
                visWriter.Put(player.DirX);
                visWriter.Put(player.DirY);
                visWriter.Put(player.Moving);
                visWriter.Put(player.Health);
                visWriter.Put(player.MaxHealth);
                visWriter.Put(player.Mana);
                visWriter.Put(player.MaxMana);
                visWriter.Put(player.Level);
                visWriter.Put(player.Name);
                visWriter.Put(player.FactionId);
                p.Send(visWriter, DeliveryMethod.Unreliable);
            }
        }
    }

    private void HandleUnequipItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int equipSlot = reader.GetInt();
        int targetInvSlot = reader.GetInt();

        if (!player.Equipment.TryGetValue(equipSlot, out var equipped)) return;

        var existing = player.Items.FirstOrDefault(i => i.Slot == targetInvSlot);
        if (existing != null && existing.ItemId != 0) return;

        player.Equipment.Remove(equipSlot);
        equipped.Slot = targetInvSlot;
        player.Items.Add(equipped);

        _db.DeleteItemBySlot(session.SelectedCharacter!.Id, 100 + equipSlot);
        _db.SaveItem(session.SelectedCharacter.Id, equipped);

        RecalculatePlayerStats(player);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_EquipUpdate);
        writer.Put(equipSlot);
        writer.Put(0);
        writer.Put(0);
        writer.Put(true);
        writer.Put(targetInvSlot);
        writer.Put(equipped.ItemId);
        writer.Put(equipped.Quantity);
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void HandleMoveItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int fromSlot = reader.GetInt();
        int toSlot = reader.GetInt();

        var fromItem = player.Items.FirstOrDefault(i => i.Slot == fromSlot);
        var toItem = player.Items.FirstOrDefault(i => i.Slot == toSlot);

        if (fromItem == null) return;

        if (toItem != null && fromItem.ItemId == toItem.ItemId && fromItem.IsStackable)
        {
            int totalQty = fromItem.Quantity + toItem.Quantity;
            int maxStack = fromItem.MaxStack;
            if (totalQty <= maxStack)
            {
                toItem.Quantity = totalQty;
                player.Items.Remove(fromItem);
                _db.DeleteItem(session.SelectedCharacter!.Id, fromItem.DbId);
                _db.SaveItem(session.SelectedCharacter!.Id, toItem);
            }
            else
            {
                toItem.Quantity = maxStack;
                fromItem.Quantity = totalQty - maxStack;
                _db.SaveItem(session.SelectedCharacter!.Id, toItem);
                _db.SaveItem(session.SelectedCharacter!.Id, fromItem);
            }
        }
        {
            fromItem.Slot = toSlot;
            if (toItem != null)
                toItem.Slot = fromSlot;
            _db.SaveItem(session.SelectedCharacter!.Id, fromItem);
            if (toItem != null)
                _db.SaveItem(session.SelectedCharacter!.Id, toItem);
        }

        SendInventoryData(peer, player);
    }

    private void HandleDropItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity is not PlayerEntity player) return;

        int slot = reader.GetInt();
        int quantity = reader.GetInt();

        if (quantity <= 0) return;

        var item = player.Items.FirstOrDefault(i => i.Slot == slot);
        if (item == null) return;

        int dropQty;
        if (quantity >= item.Quantity)
        {
            dropQty = item.Quantity;
            player.Items.Remove(item);
            _db.DeleteItem(session.SelectedCharacter!.Id, item.DbId);
        }
        else
        {
            dropQty = quantity;
            item.Quantity -= quantity;
            _db.SaveItem(session.SelectedCharacter!.Id, item);
        }

        SendInventoryData(peer, player);

        var offsetX = (float)(Random.Shared.NextDouble() - 0.5) * 20f;
        var offsetY = (float)(Random.Shared.NextDouble() - 0.5) * 20f;
        var loot = new LootEntity(entity.X + offsetX, entity.Y + offsetY, item.ItemId, dropQty, player.Id, _gameTime);
        channel.AddLoot(loot);

        var aoi = channel.GetEntitiesInAoi(loot.X, loot.Y);
        var w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
        w.Put(loot.Id);
        w.Put(loot.X);
        w.Put(loot.Y);
        w.Put(loot.ItemId);
        w.Put(loot.Quantity);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            p?.Send(w, DeliveryMethod.ReliableOrdered);
            w = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
            w.Put(loot.Id);
            w.Put(loot.X);
            w.Put(loot.Y);
            w.Put(loot.ItemId);
            w.Put(loot.Quantity);
        }
    }

    private static double GetRefineMultiplier(int refineLevel)
    {
        return refineLevel switch
        {
            1 => 1.02,
            2 => 1.04,
            3 => 1.06,
            4 => 1.08,
            5 => 1.10,
            6 => 1.13,
            7 => 1.16,
            8 => 1.20,
            9 => 1.25,
            10 => 1.30,
            _ => 1.0,
        };
    }

    private static void RecalculatePlayerStats(PlayerEntity player)
    {
        int bonusAtk = 0, bonusDef = 0, bonusForca = 0, bonusAgi = 0, bonusDes = 0, bonusInt = 0;
        foreach (var kv in player.Equipment)
        {
            var def = kv.Value.Definition;
            if (def == null) continue;
            double refineMult = GetRefineMultiplier(kv.Value.RefineLevel);
            bonusAtk += (int)(def.BaseAttack * refineMult);
            bonusDef += (int)(def.Defense * refineMult);
            bonusForca += (int)(def.Forca * refineMult);
            bonusAgi += (int)(def.Agilidade * refineMult);
            bonusDes += (int)(def.Destreza * refineMult);
            bonusInt += (int)(def.Inteligencia * refineMult);
        }
        int baseAttack = player.CharacterClass.ToLowerInvariant() switch
        {
            "guerreiro" => 10,
            "arqueiro" => 7,
            "mago" => 5,
            _ => 6,
        };
        int baseDefense = player.CharacterClass.ToLowerInvariant() switch
        {
            "guerreiro" => 8,
            "arqueiro" => 4,
            "mago" => 2,
            _ => 4,
        };

        player.BaseAttack = baseAttack + bonusAtk;
        player.Defense = baseDefense + bonusDef;
        player.Forca = player.BaseForca + bonusForca;
        player.Agilidade = player.BaseAgilidade + bonusAgi;
        player.Destreza = player.BaseDestreza + bonusDes;
        player.Inteligencia = player.BaseInteligencia + bonusInt;
        player.MaxHealth = 80 + player.Forca * 2 + player.Level * 10;
        player.MaxMana = 30 + player.Inteligencia * 3 + player.Level * 5;
    }

    private void HandleCollectLocalItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var onlineSession) || onlineSession.ChannelId < 0) return;
        var onlineCharacter = onlineSession.SelectedCharacter;
        if (onlineCharacter == null) return;

        int onlineItemId = reader.GetInt();
        int onlineQuantity = Math.Clamp(reader.GetInt(), 1, 99);
        float itemX = reader.AvailableBytes >= 8 ? reader.GetFloat() : 0f;
        float itemY = reader.AvailableBytes >= 4 ? reader.GetFloat() : 0f;

        if (ItemDefinitions.Get(onlineItemId) == null)
        {
            SendSystemMessage(peer, "Item invalido.");
            return;
        }

        var onlineChannel = _world.GetChannel(onlineSession.ChannelId);
        if (onlineChannel == null) return;

        var onlinePlayer = onlineChannel.GetEntity(onlineSession.EntityId) as PlayerEntity;
        if (onlinePlayer == null) return;

        if (itemX != 0f || itemY != 0f)
        {
            float dx = onlinePlayer.X - itemX;
            float dy = onlinePlayer.Y - itemY;
            if (MathF.Sqrt(dx * dx + dy * dy) > 100f)
            {
                SendSystemMessage(peer, "Item muito longe.");
                return;
            }
        }

        if (!TryAddItemToInventory(onlinePlayer, onlineCharacter.Id, onlineItemId, onlineQuantity))
        {
            SendSystemMessage(peer, "Inventario cheio!");
            SendInventoryData(peer, onlinePlayer);
            return;
        }

        SendInventoryData(peer, onlinePlayer);
        return;
/*
            SendSystemMessage(peer, "Inventário cheio!");
        }
*/
    }
}
