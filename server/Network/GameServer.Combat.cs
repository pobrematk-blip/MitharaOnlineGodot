using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private static long XpForNextLevel(int level)
    {
        if (level < 1) level = 1;
        return 20L + (level - 1) * 12L;
    }

    private bool HandleMonsterAIAttack(Channel channel, MonsterEntity mob, Entity target, double gameTime)
    {
        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        if (target is PlayerEntity playerTarget
            && Random.Shared.NextDouble() * 100.0 < Math.Min(40f, playerTarget.EquipmentEvasion))
            return true;
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        bool isCrit = Random.Shared.Next(100) < mob.Destreza / 4;
        int rawDamage = mob.CalculateAttackDamage();
        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit) damage = (int)(damage * 1.5f);

        target.Health -= damage;
        if (target.Health < 0) target.Health = 0;

        if (target is PlayerEntity)
            BroadcastPartyMemberUpdateForEntity(target.Id);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
        writer.Put(mob.Id);
        writer.Put(target.Id);
        writer.Put(damage);
        writer.Put(isCrit);
        writer.Put(target.Health);
        writer.Put(target.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(mob.X, mob.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
                writer.Put(mob.Id);
                writer.Put(target.Id);
                writer.Put(damage);
                writer.Put(isCrit);
                writer.Put(target.Health);
                writer.Put(target.MaxHealth);
            }
        }

        if (target.Health <= 0 && target is PlayerEntity player)
        {
            var session = _sessions.Values.FirstOrDefault(s => s.EntityId == target.Id);
            if (session != null)
                SendSystemMessage(session.Peer, "Você morreu!");
        }

        return target.Health <= 0;
    }

    private void HandleRespawn(NetPeer peer)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var player = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (player == null || player.Health > 0) return;

        player.Health = player.MaxHealth;

        var character = session.SelectedCharacter;
        float spawnX = character?.PosX ?? 1000f;
        float spawnY = character?.PosY ?? 1000f;

        float oldX = player.X;
        float oldY = player.Y;
        channel.MoveEntity(player.Id, spawnX, spawnY);

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
        writer.Put(player.Id);
        writer.Put(spawnX);
        writer.Put(spawnY);
        writer.Put(player.Health);
        writer.Put(player.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(spawnX, spawnY);
        aoi.UnionWith(channel.GetEntitiesInAoi(oldX, oldY));
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
                writer.Put(player.Id);
                writer.Put(spawnX);
                writer.Put(spawnY);
                writer.Put(player.Health);
                writer.Put(player.MaxHealth);
            }
        }
    }

    private void HandleRevivePlayer(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var healer = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (healer == null || healer.Health <= 0) return;

        ulong targetId = reader.GetULong();
        var target = channel.GetEntity(targetId) as PlayerEntity;
        if (target == null || target.Health > 0) return;

        float dx = healer.X - target.X;
        float dy = healer.Y - target.Y;
        if (MathF.Sqrt(dx * dx + dy * dy) > 80f) return;

        var scroll = healer.Items.FirstOrDefault(i => i.ItemId == 101 && i.Quantity > 0);
        if (scroll == null)
        {
            SendSystemMessage(peer, "Voc? precisa de um Pergaminho de Ressurrei??o para reviver algu?m!");
            return;
        }

        scroll.Quantity--;
        if (scroll.Quantity <= 0)
        {
            healer.Items.Remove(scroll);
            var character = session.SelectedCharacter;
            if (character != null)
                _db.DeleteItem(character.Id, scroll.DbId);
        }
        else
        {
            var character = session.SelectedCharacter;
            if (character != null)
                _db.SaveItem(character.Id, scroll);
        }

        var wItemUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
        wItemUpdate.Put(scroll.Slot);
        wItemUpdate.Put(scroll.Quantity > 0 ? scroll.ItemId : 0);
        wItemUpdate.Put(scroll.Quantity > 0 ? scroll.Quantity : 0);
        wItemUpdate.Put(0);
        wItemUpdate.Put("");
        peer.Send(wItemUpdate, DeliveryMethod.ReliableOrdered);

        target.Health = target.MaxHealth;

        var writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
        writer.Put(target.Id);
        writer.Put(target.X);
        writer.Put(target.Y);
        writer.Put(target.Health);
        writer.Put(target.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(target.X, target.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_Respawn);
                writer.Put(target.Id);
                writer.Put(target.X);
                writer.Put(target.Y);
                writer.Put(target.Health);
                writer.Put(target.MaxHealth);
            }
        }
    }

    private void HandleSkillUse(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId);
        if (entity == null || entity.Health <= 0) return;

        int skillSlot = reader.GetInt();
        float targetX = reader.GetFloat();
        float targetY = reader.GetFloat();

        // For now, just validate and broadcast the skill use
        // Full skill system implementation would look up skill data,
        // validate cooldowns, mana costs, apply effects, etc.
        var writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
        writer.Put(session.EntityId);
        writer.Put(0uL);
        writer.Put(0);
        writer.Put(false);
        writer.Put(entity.Health);
        writer.Put(entity.MaxHealth);

        var aoi = channel.GetEntitiesInAoi(entity.X, entity.Y);
        foreach (var eid in aoi)
        {
            var p = channel.GetPlayerPeer(eid);
            if (p != null)
            {
                p.Send(writer, DeliveryMethod.ReliableOrdered);
                writer = PacketSerializer.WritePacket(PacketId.S2C_CombatResult);
                writer.Put(session.EntityId);
                writer.Put(0uL);
                writer.Put(0);
                writer.Put(false);
                writer.Put(entity.Health);
                writer.Put(entity.MaxHealth);
            }
        }
    }

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
        float attackRange = 640f;

        if (dist > attackRange) return;

        if (target is MonsterEntity mob && mob.FactionId == attacker.FactionId) return;

        int targetDefense = target switch
        {
            PlayerEntity p => p.CalculateDefense(),
            MonsterEntity m => m.CalculateDefense(),
            _ => 0,
        };
        float defReduction = MathF.Min(0.80f, targetDefense / (targetDefense + 400f));
        bool isCrit = Random.Shared.Next(100) < attacker.Destreza / 4;
        int rawDamage = attacker.CalculateAttackDamage();
        int damage = Math.Max(1, (int)(rawDamage * (1f - defReduction)));
        if (isCrit) damage = (int)(damage * 1.5f);

        target.Health -= damage;
        if (target.Health < 0) target.Health = 0;

        if (target is PlayerEntity)
            BroadcastPartyMemberUpdateForEntity(target.Id);

        if (target is MonsterEntity hitMob)
        {
            hitMob.TargetEntityId = session.EntityId;
        }

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
        if (IsPlayerVip(killer))
            xpReward *= 2;

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

        long xpForNextLevel = XpForNextLevel(killer.Level);
        while (killer.Experience >= xpForNextLevel)
        {
            killer.Experience -= xpForNextLevel;
            killer.Level++;
            xpForNextLevel = XpForNextLevel(killer.Level);

            killer.MaxHealth = 80 + killer.Forca * 2 + killer.Level * 10;
            killer.Health = killer.MaxHealth;
            killer.StatPoints += 5;

            var writerLevelUp = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
            writerLevelUp.Put(killer.Id);
            writerLevelUp.Put(killer.Level);
            writerLevelUp.Put((int)Math.Min(int.MaxValue, killer.Experience));
            foreach (var eid in aoi)
            {
                var p = channel.GetPlayerPeer(eid);
                if (p != null)
                {
                    p.Send(writerLevelUp, DeliveryMethod.ReliableOrdered);
                    writerLevelUp = PacketSerializer.WritePacket(PacketId.S2C_LevelUp);
                    writerLevelUp.Put(killer.Id);
                    writerLevelUp.Put(killer.Level);
                    writerLevelUp.Put((int)Math.Min(int.MaxValue, killer.Experience));
                }
            }

            var peer = channel.GetPlayerPeer(killer.Id);
            if (peer != null)
                SendSystemMessage(peer, $"Parabéns! Você alcançou o nível {killer.Level}!");
        }

        _db.SaveCharacterXp(killerSession.SelectedCharacter!.Id, killer.Experience);
        _db.SaveCharacterLevel(killerSession.SelectedCharacter.Id, killer.Level);
        _db.SaveCharacterStats(killerSession.SelectedCharacter.Id, killer.BaseForca, killer.BaseAgilidade, killer.BaseDestreza, killer.BaseInteligencia, killer.StatPoints);
        killerSession.SelectedCharacter.Xp = killer.Experience;
        killerSession.SelectedCharacter.Level = killer.Level;
        killerSession.SelectedCharacter.StatPoints = killer.StatPoints;

        UpdateQuestKillProgress(killer, mob.PrefabId);

        SpawnMonsterLoot(channel, mob, killer, aoi);

        // Libera primeiro a vaga no spot. Assim um elite gerado pelo contador
        // de mortes ocupa essa vaga sem ultrapassar o MaxCount do spot.
        channel.RemoveEntity(mobId);
        channel.HandleMonsterKilled(mob, _gameTime);

        if (mob.IsBoss && string.Equals(mob.PrefabId, "slimeBoss", StringComparison.OrdinalIgnoreCase))
        {
            SendGlobalChat(
                "Sistema",
                $"{killer.Name} derrotou o Boss Slime! Ele nascerá novamente em 1 hora.");
        }

    }

    private void SpawnMonsterLoot(Channel channel, MonsterEntity mob, PlayerEntity killer, HashSet<ulong> aoi)
    {
        var template = channel.Spawner.GetTemplate(mob.PrefabId);
        if (template == null) return;

        var rng = Random.Shared;
        bool vipDrop = IsPlayerVip(killer);

        int goldAmount = 0;
        if (template.GoldMax > 0)
            goldAmount = rng.Next(template.GoldMin, template.GoldMax + 1);

        var spawnedLoot = new List<LootEntity>();

        foreach (var entry in template.LootTable)
        {
            double chance = vipDrop ? Math.Min(1.0, entry.DropChance * 2.0) : entry.DropChance;
            if (rng.NextDouble() >= chance) continue;
            int qty = entry.MinQuantity == entry.MaxQuantity
                ? entry.MinQuantity
                : rng.Next(entry.MinQuantity, entry.MaxQuantity + 1);

            var offsetX = (float)(rng.NextDouble() - 0.5) * 40f;
            var offsetY = (float)(rng.NextDouble() - 0.5) * 40f;

            var loot = new LootEntity(mob.X + offsetX, mob.Y + offsetY, entry.ItemId, qty, killer.Id, _gameTime);
            spawnedLoot.Add(loot);
            channel.AddLoot(loot);
        }

        bool dropsEquipment = template.DropsNormalEquipment || template.DropsEliteEquipment;
        if (dropsEquipment)
        {
            int equipmentLevel = mob.Level < 10 ? 1 : Math.Min(100, (mob.Level / 10) * 10);
            bool eliteItem = template.DropsEliteEquipment;
            var equipmentPool = ItemDefinitions.GetAll()
                .Where(def => def.IsElite == eliteItem
                    && def.RequiredLevel == equipmentLevel
                    && def.Type is >= ItemType.Helmet and <= ItemType.Shield)
                .ToArray();

            if (equipmentPool.Length > 0)
            {
                var equipment = equipmentPool[rng.Next(equipmentPool.Length)];
                var loot = new LootEntity(
                    mob.X + (float)(rng.NextDouble() - 0.5) * 40f,
                    mob.Y + (float)(rng.NextDouble() - 0.5) * 40f,
                    equipment.Id,
                    1,
                    killer.Id,
                    _gameTime);
                spawnedLoot.Add(loot);
                channel.AddLoot(loot);
                Logger.Info($"Drop: {mob.Name} gerou {equipment.Name} ({(eliteItem ? "Elite" : "Normal")}, nivel {equipmentLevel}).");
            }
            else
            {
                Logger.Info($"Drop: nenhum equipamento {(eliteItem ? "Elite" : "Normal")} de nivel {equipmentLevel} encontrado para {mob.Name}.");
            }
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
            bool itemAdded = TryAddItemToInventory(player, character.Id, loot.ItemId, loot.Quantity);

            if (!itemAdded)
            {
                SendSystemMessage(peer, "Invent?rio cheio!");
                SendInventoryData(peer, player);
                return;
            }
            /* else if (false)
            {
                var newItem = new ItemInstance { Slot = slot, ItemId = loot.ItemId, Quantity = loot.Quantity };
                player.Items.Add(newItem);
                _db.SaveItem(character.Id, newItem);
                var wUpdate = PacketSerializer.WritePacket(PacketId.S2C_ItemUpdate);
                wUpdate.Put(slot);
                wUpdate.Put(loot.ItemId);
                wUpdate.Put(loot.Quantity);
                wUpdate.Put(0);
                peer.Send(wUpdate, DeliveryMethod.ReliableOrdered);
            }
            else if (false)
            {
                SendSystemMessage(peer, "Inventário cheio!");
                return;
            }
            */
        }

        SendInventoryData(peer, player);

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
