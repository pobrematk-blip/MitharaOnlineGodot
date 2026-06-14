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
        }
        writer.Put(player.Equipment.Count);
        foreach (var kv in player.Equipment)
        {
            writer.Put(kv.Key);
            writer.Put(kv.Value.ItemId);
            writer.Put(kv.Value.Quantity);
        }
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
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
        if (currentEquipped != null)
        {
            writer.Put(true);
            writer.Put(currentEquipped.Slot);
            writer.Put(currentEquipped.ItemId);
            writer.Put(currentEquipped.Quantity);
        }
        else
        {
            writer.Put(false);
            writer.Put(invSlot);
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
        else
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

    private static void RecalculatePlayerStats(PlayerEntity player)
    {
        int bonusAtk = 0, bonusDef = 0, bonusForca = 0, bonusAgi = 0, bonusDes = 0, bonusInt = 0;
        foreach (var kv in player.Equipment)
        {
            var def = kv.Value.Definition;
            if (def == null) continue;
            bonusAtk += def.BaseAttack;
            bonusDef += def.Defense;
            bonusForca += def.Forca;
            bonusAgi += def.Agilidade;
            bonusDes += def.Destreza;
            bonusInt += def.Inteligencia;
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
        player.MaxHealth = 80 + player.Forca * 5 + player.Level * 10;
        player.MaxMana = 30 + player.Inteligencia * 5 + player.Level * 5;
    }

    private void HandleCollectLocalItem(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.ChannelId < 0) return;
        var character = session.SelectedCharacter;
        if (character == null) return;

        int itemId = reader.GetInt();
        int quantity = reader.GetInt();

        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var player = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null) return;

        int slot = player.FindEmptyInventorySlot();
        var existingItem = player.Items.FirstOrDefault(i => i.ItemId == itemId && i.Quantity + quantity <= 999);

        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
            _db.SaveItem(character.Id, existingItem);
            var wUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
            wUpdate.Put(existingItem.Slot);
            wUpdate.Put(existingItem.ItemId);
            wUpdate.Put(existingItem.Quantity);
            peer.Send(wUpdate, DeliveryMethod.ReliableOrdered);
        }
        else if (slot >= 0)
        {
            var newItem = new ItemInstance { Slot = slot, ItemId = itemId, Quantity = quantity };
            player.Items.Add(newItem);
            _db.SaveItem(character.Id, newItem);
            var wUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
            wUpdate.Put(slot);
            wUpdate.Put(itemId);
            wUpdate.Put(quantity);
            peer.Send(wUpdate, DeliveryMethod.ReliableOrdered);
        }
        else
        {
            SendSystemMessage(peer, "Inventário cheio!");
        }
    }
}
