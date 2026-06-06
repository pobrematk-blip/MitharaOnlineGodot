using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private void HandleAttack(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        ulong targetId = reader.GetULong();
        int skillId = reader.GetInt();

        var attacker = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (attacker == null || attacker.Health <= 0) return;

        var target = channel.GetEntity(targetId);
        if (target == null || target.Health <= 0) return;

        float dx = target.X - attacker.X;
        float dy = target.Y - attacker.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float attackRange = 50f;

        if (dist > attackRange) return;

        if (target is MonsterEntity mob && mob.FactionId == attacker.FactionId) return;

        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        bool isCrit = Random.Shared.Next(100) < attacker.Destreza;
        int damage = Math.Max(1, attacker.CalculateAttackDamage() - targetDefense);
        if (isCrit) damage = (int)(damage * 1.5f);

        target.Health -= damage;
        if (target.Health < 0) target.Health = 0;

        var writerCombat = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
        writerCombat.Put(session.EntityId);
        writerCombat.Put(targetId);
        writerCombat.Put(damage);
        writerCombat.Put(isCrit);
        writerCombat.Put(target.Health);
        writerCombat.Put(target.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(attacker.X, attacker.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writerCombat, DeliveryMethod.ReliableOrdered);
                writerCombat = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
                writerCombat.Put(session.EntityId);
                writerCombat.Put(targetId);
                writerCombat.Put(damage);
                writerCombat.Put(isCrit);
                writerCombat.Put(target.Health);
                writerCombat.Put(target.MaxHealth);
            }
        }

        if (target is MonsterEntity killedMob && target.Health <= 0)
        {
            HandleMonsterDeath(channel, killedMob, attacker, session, targetId);
        }
    }

    private void HandleMonsterDeath(Channel channel, MonsterEntity mob, PlayerEntity killer, PlayerSession killerSession, ulong mobId)
    {
        int xpReward = mob.ExperienceReward;

        var writerDied = PacketSerializer.WritePacket(PacketId.S2C_EntityDied);
        writerDied.Put(mobId);
        writerDied.Put(killer.Id);

        var writerGainExp = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
        writerGainExp.Put(killer.Id);
        writerGainExp.Put(xpReward);
        writerGainExp.Put(killer.Experience);

        var aoi = channel.GetEntitiesInAoi(mob.X, mob.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writerDied, DeliveryMethod.ReliableOrdered);
                writerDied = PacketSerializer.WritePacket(PacketId.S2C_EntityDied);
                writerDied.Put(mobId);
                writerDied.Put(killer.Id);
            }
        }

        killer.Experience += xpReward;
        var writerExp = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
        writerExp.Put(killer.Id);
        writerExp.Put(xpReward);
        writerExp.Put(killer.Experience);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writerExp, DeliveryMethod.ReliableOrdered);
                writerExp = PacketSerializer.WritePacket(PacketId.S2C_GainExp);
                writerExp.Put(killer.Id);
                writerExp.Put(xpReward);
                writerExp.Put(killer.Experience);
            }
        }

        long xpForNextLevel = killer.Level * 100L;
        while (killer.Experience >= xpForNextLevel)
        {
            killer.Experience -= xpForNextLevel;
            killer.Level++;
            xpForNextLevel = killer.Level * 100L;

            killer.MaxHealth = 80 + killer.Forca * 5 + killer.Level * 10;
            killer.Health = killer.MaxHealth;

            var writerLevelUp = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
            writerLevelUp.Put(killer.Id);
            writerLevelUp.Put(killer.Level);
            foreach (var eid in aoi)
            {
                var p = channel.GetPlayerPeer(eid);
                if (p != null)
                {
                    p.Send(writerLevelUp, DeliveryMethod.ReliableOrdered);
                    writerLevelUp = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
                    writerLevelUp.Put(killer.Id);
                    writerLevelUp.Put(killer.Level);
                }
            }

            var peer = channel.GetPlayerPeer(killer.Id);
            if (peer != null)
                SendSystemMessage(peer, $"Parabéns! Você alcançou o nível {killer.Level}!");
        }

        _db.SaveCharacterXp(killerSession.SelectedCharacter!.Id, killer.Experience);
        _db.SaveCharacterLevel(killerSession.SelectedCharacter.Id, killer.Level);

        UpdateQuestKillProgress(killer, mob.PrefabId);

        SpawnMonsterLoot(channel, mob, killer, aoi);

        var spawnPoint = channel.Spawner.GetSpawnPoints()
            .FirstOrDefault(sp => sp.PrefabId == mob.PrefabId);
        if (spawnPoint != null)
        {
            channel.ScheduleRespawn(spawnPoint, _gameTime);
        }

        channel.RemoveEntity(mobId);
    }

    private void SpawnMonsterLoot(Channel channel, MonsterEntity mob, PlayerEntity killer, HashSet<ulong> aoi)
    {
        var template = channel.Spawner.GetTemplate(mob.PrefabId);
        if (template == null) return;

        var rng = Random.Shared;

        int goldAmount = 0;
        if (template.GoldMax > 0)
            goldAmount = rng.Next(template.GoldMin, template.GoldMax + 1);

        var spawnedLoot = new List<LootEntity>();

        foreach (var entry in template.LootTable)
        {
            if (rng.NextDouble() >= entry.DropChance) continue;
            int qty = entry.MinQuantity == entry.MaxQuantity
                ? entry.MinQuantity
                : rng.Next(entry.MinQuantity, entry.MaxQuantity + 1);

            var offsetX = (float)(rng.NextDouble() - 0.5) * 40f;
            var offsetY = (float)(rng.NextDouble() - 0.5) * 40f;

            var loot = new LootEntity(mob.X + offsetX, mob.Y + offsetY, entry.ItemId, qty, killer.Id, _gameTime);
            spawnedLoot.Add(loot);
            channel.AddLoot(loot);
        }

        if (goldAmount > 0)
        {
            var goldLoot = new LootEntity(mob.X, mob.Y, 0, goldAmount, killer.Id, _gameTime);
            spawnedLoot.Add(goldLoot);
            channel.AddLoot(goldLoot);
        }

        foreach (var loot in spawnedLoot)
        {
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
    }

    private void HandleLootPickup(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.ChannelId < 0) return;
        var character = session.SelectedCharacter;
        if (character == null) return;

        ulong lootId = reader.GetULong();
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var player = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null) return;

        var loot = channel.GetLoot(lootId);
        if (loot == null || loot.PickedUp) return;

        float dx = player.X - loot.X;
        float dy = player.Y - loot.Y;
        if (MathF.Sqrt(dx * dx + dy * dy) > 80f) return;

        if (loot.ItemId == 0)
        {
            player.Gold += loot.Quantity;
            if (session.SelectedCharacter != null)
                _db.SaveCharacterGold(session.SelectedCharacter.Id, player.Gold);
            SendGoldUpdate(peer, player.Gold);
        }
        else
        {
            int slot = player.FindEmptyInventorySlot();
            var existingItem = player.Items.FirstOrDefault(i => i.ItemId == loot.ItemId && i.Quantity + loot.Quantity <= 999);

            if (existingItem != null)
            {
                existingItem.Quantity += loot.Quantity;
                var wUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
                wUpdate.Put(existingItem.Slot);
                wUpdate.Put(existingItem.ItemId);
                wUpdate.Put(existingItem.Quantity);
                peer.Send(wUpdate, DeliveryMethod.ReliableOrdered);
            }
            else if (slot >= 0)
            {
                var newItem = new ItemInstance { Slot = slot, ItemId = loot.ItemId, Quantity = loot.Quantity };
                player.Items.Add(newItem);
                _db.SaveItem(character.Id, newItem);
                var wUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
                wUpdate.Put(slot);
                wUpdate.Put(loot.ItemId);
                wUpdate.Put(loot.Quantity);
                peer.Send(wUpdate, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                SendSystemMessage(peer, "Inventário cheio!");
                return;
            }
        }

        loot.PickedUp = true;
        channel.RemoveLoot(lootId);

        var aoi = channel.GetEntitiesInAoi(loot.X, loot.Y);
        var wDespawn = PacketSerializer.WritePacket(PacketId.S2C_LootDespawn);
        wDespawn.Put(lootId);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(wDespawn, DeliveryMethod.ReliableOrdered);
                wDespawn = PacketSerializer.WritePacket(PacketId.S2C_LootDespawn);
                wDespawn.Put(lootId);
            }
        }
    }

    private void SpawnTestPotion(NetPeer peer, Channel channel, Entity sender)
    {
        var offsetX = (float)(Random.Shared.NextDouble() - 0.5) * 60f;
        var offsetY = (float)(Random.Shared.NextDouble() - 0.5) * 60f;
        var loot = new LootEntity(sender.X + offsetX, sender.Y + offsetY, 1, 1, sender.Id, _gameTime);
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

        SendSystemMessage(peer, "Poção de vida spawnada! Aproxime e aperte F para pegar.");
    }
}
