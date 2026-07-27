using System.Diagnostics.CodeAnalysis;
using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private bool TryGetPlayer(NetPeer peer, [NotNullWhen(true)] out PlayerEntity? player, [NotNullWhen(true)] out Channel? channel)
    {
        player = null;
        channel = null;
        if (!_sessions.TryGetValue(peer, out var session)) return false;
        channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return false;
        player = channel.GetEntity(session.EntityId) as PlayerEntity;
        return player != null;
    }

    private (string name, int hp, int maxHp, int mana, int maxMana, int level) GetEntityDisplayData(ulong entityId)
    {
        foreach (var ch in _world.GetAllChannels())
        {
            var e = ch.GetEntity(entityId);
            if (e != null)
                return (e.Name, e.Health, e.MaxHealth, e.Mana, e.MaxMana, e.Level);
        }
        return ("?", 0, 0, 0, 0, 1);
    }

    private PlayerEntity? FindPlayerByName(string name, out NetPeer? peer, out Channel? foundChannel)
    {
        peer = null;
        foundChannel = null;
        foreach (var ch in _world.GetAllChannels())
        {
            foreach (var kv in ch.GetAllEntities())
            {
                if (kv.Value.Type == EntityType.Player && kv.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    peer = ch.GetPlayerPeer(kv.Key);
                    foundChannel = ch;
                    return kv.Value as PlayerEntity;
                }
            }
        }
        return null;
    }

    private Entity? FindEntityById(ulong entityId, out NetPeer? peer, out Channel? foundChannel)
    {
        peer = null;
        foundChannel = null;
        foreach (var ch in _world.GetAllChannels())
        {
            var entity = ch.GetEntity(entityId);
            if (entity != null)
            {
                peer = ch.GetPlayerPeer(entityId);
                foundChannel = ch;
                return entity;
            }
        }
        return null;
    }

    private NetPeer? FindPeerByEntityId(ulong entityId)
    {
        foreach (var ch in _world.GetAllChannels())
        {
            var peer = ch.GetPlayerPeer(entityId);
            if (peer != null) return peer;
        }
        return null;
    }

    private void OnChannelEntitySpawned(Entity entity)
    {
        int sent = 0;
        foreach (var ch in _world.GetAllChannels())
        {
            if (!ReferenceEquals(ch.GetEntity(entity.Id), entity))
                continue;

            foreach (var kv in ch.GetAllEntities())
            {
                if (kv.Value.Type != EntityType.Player) continue;
                if (kv.Key == entity.Id) continue;

                float dx = kv.Value.X - entity.X;
                float dy = kv.Value.Y - entity.Y;
                float aoiRadius = ch.AoiRadius;
                if ((dx * dx) + (dy * dy) > aoiRadius * aoiRadius)
                    continue;

                var peer = ch.GetPlayerPeer(kv.Key);
                if (peer == null) continue;
                if (!_sessions.TryGetValue(peer, out var session)) continue;
                if (!IsEntityVisibleToSession(entity, session)) continue;
                if (session.SpawnedEntities.Contains(entity.Id)) continue;

                // Envia spawn apenas para clientes que realmente precisam desenhar a entidade.
                var writer = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                WriteEntityPacket(writer, entity);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
                session.SpawnedEntities.Add(entity.Id);
                sent++;
            }
        }
        if (entity.Type == EntityType.Player)
            Logger.Info($"OnChannelEntitySpawned: {entity.Name} broadcast para {sent} jogador(es) no canal");
    }

    private void BroadcastEntityUpdates()
    {
        const int maxPayload = 900;

        foreach (var channel in _world.GetAllChannels())
        {
            var entities = channel.GetAllEntities();
            UpdateServerPets(channel, entities);
            var players = entities.Where(kv => kv.Value.Type == EntityType.Player).ToList();

            foreach (var playerKv in players)
            {
                ulong eid = playerKv.Key;
                var playerEntity = playerKv.Value;
                var peer = channel.GetPlayerPeer(eid);
                if (peer == null) continue;

                if (!_sessions.TryGetValue(peer, out var session)) continue;

                var aoi = channel.GetEntitiesInAoi(playerEntity.X, playerEntity.Y);

                var aoiSet = new HashSet<ulong>();
                foreach (ulong aoiEntityId in aoi)
                {
                    if (!entities.TryGetValue(aoiEntityId, out var visibleCandidate))
                        continue;
                    if (IsEntityVisibleToSession(visibleCandidate, session))
                        aoiSet.Add(aoiEntityId);
                }

                // Send spawn packets for new entities entering AOI
                foreach (var aoiEid in aoiSet)
                {
                    if (aoiEid == eid) continue;
                    if (session.SpawnedEntities.Contains(aoiEid)) continue;
                    if (!entities.TryGetValue(aoiEid, out var newEntity)) continue;

                    var spawnWriter = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                    WriteEntityPacket(spawnWriter, newEntity);
                    peer.Send(spawnWriter, DeliveryMethod.ReliableOrdered);
                    session.SpawnedEntities.Add(aoiEid);
                }

                // Despawn entities that left the AOI
                var toDespawn = new List<ulong>();
                foreach (var spawnedId in session.SpawnedEntities)
                {
                    if (spawnedId == eid) continue;
                    if (!aoiSet.Contains(spawnedId))
                        toDespawn.Add(spawnedId);
                }
                foreach (var despawnId in toDespawn)
                {
                    var despawnWriter = PacketSerializer.WritePacket(PacketId.S2C_DespawnEntity);
                    despawnWriter.Put(despawnId);
                    peer.Send(despawnWriter, DeliveryMethod.ReliableOrdered);
                    session.SpawnedEntities.Remove(despawnId);
                }

                SyncLootVisibility(peer, session, channel, playerEntity.X, playerEntity.Y);
                FlushEntityUpdates(peer, entities, aoiSet, eid, maxPayload);
            }
        }
    }

    private bool IsEntityVisibleToSession(Entity entity, PlayerSession session)
    {
        if (entity is PetEntity pet && pet.OwnerEntityId == session.EntityId)
            return false;

        return string.Equals(GetEntityMapName(entity), GetSessionMapName(session), StringComparison.OrdinalIgnoreCase);
    }

    private string GetEntityMapName(Entity entity)
    {
        if (entity is NPCEntity npc)
            return NormalizeMapName(npc.Map);

        if (entity is PetEntity pet)
        {
            foreach (var kv in _sessions)
            {
                if (kv.Value.EntityId == pet.OwnerEntityId)
                    return GetSessionMapName(kv.Value);
            }
        }

        if (entity.Type == EntityType.Player)
        {
            foreach (var kv in _sessions)
            {
                if (kv.Value.EntityId == entity.Id)
                    return GetSessionMapName(kv.Value);
            }
        }

        return MainSceneName;
    }

    private static string GetSessionMapName(PlayerSession session)
    {
        return NormalizeMapName(session.CurrentMap);
    }

    private static string NormalizeMapName(string? map)
    {
        return string.IsNullOrWhiteSpace(map) ? MainSceneName : map.Trim().ToLowerInvariant();
    }

    private static bool IsLootInAoi(Channel channel, LootEntity loot, float x, float y)
    {
        float dx = loot.X - x;
        float dy = loot.Y - y;
        return (dx * dx) + (dy * dy) <= channel.AoiRadius * channel.AoiRadius;
    }

    private void SyncLootVisibility(NetPeer peer, PlayerSession session, Channel channel, float x, float y)
    {
        var visibleLoot = channel.GetAllLoot()
            .Where(loot => IsLootInAoi(channel, loot, x, y))
            .ToList();
        var visibleIds = visibleLoot.Select(loot => loot.Id).ToHashSet();

        foreach (var loot in visibleLoot)
        {
            if (session.SpawnedLoot.Contains(loot.Id))
                continue;

            var spawnWriter = PacketSerializer.WritePacket(PacketId.S2C_LootSpawn);
            spawnWriter.Put(loot.Id);
            spawnWriter.Put(loot.X);
            spawnWriter.Put(loot.Y);
            spawnWriter.Put(loot.ItemId);
            spawnWriter.Put(loot.Quantity);
            peer.Send(spawnWriter, DeliveryMethod.ReliableOrdered);
            session.SpawnedLoot.Add(loot.Id);
        }

        var lootToDespawn = session.SpawnedLoot
            .Where(lootId => !visibleIds.Contains(lootId) || channel.GetLoot(lootId) == null)
            .ToList();

        foreach (ulong lootId in lootToDespawn)
        {
            var despawnWriter = PacketSerializer.WritePacket(PacketId.S2C_LootDespawn);
            despawnWriter.Put(lootId);
            peer.Send(despawnWriter, DeliveryMethod.ReliableOrdered);
            session.SpawnedLoot.Remove(lootId);
        }
    }

    private void FlushEntityUpdates(NetPeer peer, Dictionary<ulong, Entity> entities, HashSet<ulong> aoi, ulong playerEntityId, int maxPayload)
    {
        var writer = new NetDataWriter();
        int batchFrom = 0;
        int index = 0;

		foreach (var aoiEid in aoi)
		{
			if (!entities.TryGetValue(aoiEid, out var aoiEntity)) continue;

            if (index > batchFrom && writer.Length + EstimateEntitySize(aoiEntity) > maxPayload)
            {
                SendEntityBatch(peer, writer, index - batchFrom);
                writer = new NetDataWriter();
                batchFrom = index;
            }

            writer.Put(aoiEid);
            writer.Put(aoiEntity.X);
            writer.Put(aoiEntity.Y);
            writer.Put(aoiEntity.DirX);
            writer.Put(aoiEntity.DirY);
            writer.Put(aoiEntity.Moving);
            writer.Put(aoiEntity.Sprinting);
            writer.Put(aoiEntity.Health);
            writer.Put(aoiEntity.MaxHealth);
            writer.Put(aoiEntity.Mana);
            writer.Put(aoiEntity.MaxMana);
            writer.Put(aoiEntity.Level);
            writer.Put(aoiEntity.Name);
            writer.Put(aoiEntity.FactionId);
            writer.Put(GetNetworkAiState(aoiEntity));
            if (aoiEntity is PlayerEntity remotePlayer)
            {
                writer.Put(remotePlayer.Experience);
                writer.Put(XpForNextLevel(remotePlayer.Level));
                var guild = remotePlayer.GuildId >= 0 ? _world.Guilds.GetGuild(remotePlayer.GuildId) : null;
                writer.Put(guild?.Name ?? "");
                writer.Put(guild?.Tag ?? "");
                writer.Put(guild?.Emblem ?? -1);
            }
            else
            {
                writer.Put(0L);
                writer.Put(1L);
                writer.Put("");
                writer.Put("");
                writer.Put(-1);
            }
            index++;
        }

        if (index > batchFrom)
            SendEntityBatch(peer, writer, index - batchFrom);
    }

    private static void SendEntityBatch(NetPeer peer, NetDataWriter writer, int count)
    {
        var packet = PacketSerializer.WritePacket(PacketId.S2C_EntityUpdate);
        packet.Put(count);
        packet.Put(writer.CopyData());
        peer.Send(packet, DeliveryMethod.Unreliable);
    }

    private void BroadcastSingleEntityUpdate(Channel channel, Entity entity)
    {
        var aoi = channel.GetEntitiesInAoi(entity.X, entity.Y);
        foreach (var eid in aoi)
        {
            var peer = channel.GetPlayerPeer(eid);
            if (peer == null)
                continue;

            var data = new NetDataWriter();
            WriteEntityUpdate(data, entity);

            var packet = PacketSerializer.WritePacket(PacketId.S2C_EntityUpdate);
            packet.Put(1);
            packet.Put(data.CopyData());
            peer.Send(packet, DeliveryMethod.ReliableOrdered);
        }
    }

    private void WriteEntityUpdate(NetDataWriter writer, Entity entity)
    {
        writer.Put(entity.Id);
        writer.Put(entity.X);
        writer.Put(entity.Y);
        writer.Put(entity.DirX);
        writer.Put(entity.DirY);
        writer.Put(entity.Moving);
        writer.Put(entity.Sprinting);
        writer.Put(entity.Health);
        writer.Put(entity.MaxHealth);
        writer.Put(entity.Mana);
        writer.Put(entity.MaxMana);
        writer.Put(entity.Level);
        writer.Put(entity.Name);
        writer.Put(entity.FactionId);
        writer.Put(GetNetworkAiState(entity));
        if (entity is PlayerEntity player)
        {
            writer.Put(player.Experience);
            writer.Put(XpForNextLevel(player.Level));
            var guild = player.GuildId >= 0 ? _world.Guilds.GetGuild(player.GuildId) : null;
            writer.Put(guild?.Name ?? "");
            writer.Put(guild?.Tag ?? "");
            writer.Put(guild?.Emblem ?? -1);
        }
        else
        {
            writer.Put(0L);
            writer.Put(1L);
            writer.Put("");
            writer.Put("");
            writer.Put(-1);
        }
    }

    private static int EstimateEntitySize(Entity entity)
    {
        return 80 + (entity.Name.Length * 2) + (entity.FactionId.Length * 2);
    }

    private void BroadcastSpawnToNearby(Channel channel, Entity entity, float x, float y)
    {
        int sent = 0;
        int totalPlayers = 0;
        foreach (var kv in channel.GetAllEntities())
        {
            if (kv.Value.Type != EntityType.Player) continue;
            totalPlayers++;
            if (kv.Key == entity.Id) continue;
            if (entity is PetEntity pet && pet.OwnerEntityId == kv.Key) continue;

            var peer = channel.GetPlayerPeer(kv.Key);
            if (peer == null)
                continue;

            if (!_sessions.TryGetValue(peer, out var session))
                continue;

            if (!IsEntityVisibleToSession(entity, session))
                continue;

            float dx = kv.Value.X - x;
            float dy = kv.Value.Y - y;
            if ((dx * dx) + (dy * dy) > channel.AoiRadius * channel.AoiRadius)
                continue;

            if (session.SpawnedEntities.Contains(entity.Id))
                continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
            WriteEntityPacket(writer, entity);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            session.SpawnedEntities.Add(entity.Id);
            sent++;
        }
        if (entity.Type == EntityType.Player)
            Logger.Info($"BroadcastSpawnToNearby: {entity.Name} enviado para {sent} de {totalPlayers} jogador(es) no canal");
    }

    private void BroadcastDespawn(Channel channel, ulong entityId)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_DespawnEntity);
        writer.Put(entityId);

        foreach (var kv in channel.GetAllEntities())
        {
            if (kv.Value.Type != EntityType.Player) continue;
            var peer = channel.GetPlayerPeer(kv.Key);
            peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void WriteEntityPacket(NetDataWriter writer, Entity entity)
    {
        writer.Put(entity.Id);
        writer.Put((byte)entity.Type);
        writer.Put(entity.Name);
        writer.Put(entity.X);
        writer.Put(entity.Y);
        writer.Put(entity.Speed);
        writer.Put(entity.Level);
        writer.Put(entity.Health);
        writer.Put(entity.MaxHealth);
        writer.Put(entity.FactionId);

        if (entity is PlayerEntity player)
        {
            writer.Put(player.CharacterClass);
            writer.Put(player.Race);
            writer.Put(player.Experience);
            writer.Put(XpForNextLevel(player.Level));
            var guild = player.GuildId >= 0 ? _world.Guilds.GetGuild(player.GuildId) : null;
            writer.Put(guild?.Name ?? "");
            writer.Put(guild?.Tag ?? "");
            writer.Put(guild?.Emblem ?? -1);
            WriteEquipmentVisualPayload(writer, player);
            writer.Put(player.CabeloPath ?? "");
            writer.Put(player.BarbaPath ?? "");
            writer.Put(string.IsNullOrWhiteSpace(player.CabeloCor) ? "ffffff" : player.CabeloCor);
            writer.Put(string.IsNullOrWhiteSpace(player.BarbaCor) ? "ffffff" : player.BarbaCor);
        }
        else if (entity is MonsterEntity mob)
        {
            writer.Put(mob.IsBoss);
            writer.Put(mob.ExperienceReward);
            writer.Put(mob.PrefabId);
            writer.Put(mob.Passive);
        }
        else if (entity is NPCEntity npc)
        {
            writer.Put(npc.DialogId);
            writer.Put(npc.Race);
            writer.Put(npc.AnimPrefix);
        }
        else if (entity is PetEntity pet)
        {
            writer.Put(pet.OwnerEntityId);
            writer.Put(pet.PetId);
            writer.Put(pet.AnimPrefix);
            writer.Put(pet.OwnerName);
        }
    }

    private static void WriteEquipmentVisualPayload(NetDataWriter writer, PlayerEntity player)
    {
        var visualEquipment = player.Equipment
            .Where(kv => IsVisualEquipmentSlot(kv.Key) && kv.Value.ItemId > 0)
            .OrderBy(kv => kv.Key)
            .ToList();

        writer.Put(visualEquipment.Count);
        foreach (var kv in visualEquipment)
        {
            writer.Put(kv.Key);
            writer.Put(kv.Value.ItemId);
            writer.Put(kv.Value.RefineLevel);
        }
    }

    private static bool IsVisualEquipmentSlot(int slot)
    {
        return slot is 1 or 2 or 4 or 5 or 6 or 7 or 8 or 16;
    }

    private void BroadcastEquipmentVisualUpdate(Channel channel, PlayerEntity player)
    {
        var writer = PacketSerializer.WritePacket(PacketId.S2C_EquipmentVisualUpdate);
        writer.Put(player.Id);
        WriteEquipmentVisualPayload(writer, player);

        foreach (var kv in channel.GetAllEntities())
        {
            if (kv.Value.Type != EntityType.Player || kv.Key == player.Id)
                continue;

            var peer = channel.GetPlayerPeer(kv.Key);
            peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private static byte GetNetworkAiState(Entity entity)
    {
        return entity switch
        {
            MonsterEntity monster => (byte)monster.AIState,
            PetEntity pet when pet.IsAttacking => 2,
            PetEntity pet when pet.Moving => 1,
            _ => 0,
        };
    }

    private void UpdateServerPets(Channel channel, Dictionary<ulong, Entity> entities)
    {
        const float petLeashRange = 32f * 20f;
        const float petAttackVisualRange = 86f;
        const float petLootTouchRange = 34f;
        const float petFollowDistance = 72f;
        const float petStopDeadZone = 10f;

        foreach (var pet in entities.Values.OfType<PetEntity>().ToList())
        {
            if (!entities.TryGetValue(pet.OwnerEntityId, out var owner) || owner is not PlayerEntity ownerPlayer)
            {
                channel.RemoveEntity(pet.Id);
                BroadcastDespawn(channel, pet.Id);
                continue;
            }

            pet.IsAttacking = pet.AttackVisualUntil > _gameTime;
            if (pet.Mode == PetMode.Parado)
            {
                pet.Moving = false;
                pet.TargetEntityId = 0;
                continue;
            }

            if (pet.Mode == PetMode.Coletar)
            {
                if (TryUpdatePetCollection(channel, pet, ownerPlayer, petLeashRange, petLootTouchRange))
                    continue;

                FollowOwner(channel, pet, ownerPlayer, petFollowDistance, petStopDeadZone);
                continue;
            }

            Entity? target = null;
            if (pet.Mode == PetMode.Atacar)
            {
                target = ResolvePetAttackTarget(channel, entities, pet, ownerPlayer, petLeashRange);
            }
            else if (pet.Mode == PetMode.Guarda)
            {
                target = ResolvePetGuardTarget(channel, entities, pet, ownerPlayer, petLeashRange);
            }
            else
            {
                pet.TargetEntityId = 0;
            }

            if (target != null)
            {
                float tx = target.X - pet.X;
                float ty = target.Y - pet.Y;
                float tdist = MathF.Sqrt(tx * tx + ty * ty);
                if (tdist > petAttackVisualRange)
                {
                    MovePetTowards(channel, pet, tx, ty, tdist, pet.Speed);
                    continue;
                }

                pet.Moving = false;
                if (tdist > 0.001f)
                {
                    pet.DirX = tx / tdist;
                    pet.DirY = ty / tdist;
                }
                pet.IsAttacking = true;
                TryServerPetAttack(channel, ownerPlayer, pet, target);
                if (pet.AttackVisualUntil <= _gameTime)
                    pet.AttackVisualUntil = _gameTime + 0.35;
                continue;
            }

            FollowOwner(channel, pet, ownerPlayer, petFollowDistance, petStopDeadZone);
        }
    }

    private void HandlePetCommand(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session))
            return;

        var channel = _world.GetChannel(session.ChannelId);
        var pet = channel?.GetPetOwnedBy(session.EntityId);
        if (pet == null)
            return;

        int rawMode = reader.GetInt();
        if (!Enum.IsDefined(typeof(PetMode), (byte)rawMode))
            return;

        pet.Mode = (PetMode)(byte)rawMode;
        pet.TargetEntityId = 0;
        pet.Moving = false;
        BroadcastSingleEntityUpdate(channel!, pet);
    }

    private Entity? ResolvePetAttackTarget(Channel channel, Dictionary<ulong, Entity> entities, PetEntity pet, PlayerEntity owner, float petLeashRange)
    {
        if (pet.TargetEntityId != 0
            && entities.TryGetValue(pet.TargetEntityId, out var current)
            && IsValidPetTarget(channel, owner, current, petLeashRange))
            return current;

        pet.TargetEntityId = 0;
        var target = entities.Values
            .Where(e => IsValidPetTarget(channel, owner, e, petLeashRange))
            .OrderBy(e => DistanceSquared(pet.X, pet.Y, e.X, e.Y))
            .FirstOrDefault();

        pet.TargetEntityId = target?.Id ?? 0;
        return target;
    }

    private Entity? ResolvePetGuardTarget(Channel channel, Dictionary<ulong, Entity> entities, PetEntity pet, PlayerEntity owner, float petLeashRange)
    {
        if (owner.LastAttackedAt + 4.0 < _gameTime)
        {
            pet.TargetEntityId = 0;
            return null;
        }

        if (owner.LastAttackerEntityId != 0
            && entities.TryGetValue(owner.LastAttackerEntityId, out var attacker)
            && IsValidPetTarget(channel, owner, attacker, petLeashRange))
        {
            pet.TargetEntityId = attacker.Id;
            return attacker;
        }

        var mobTargetingOwner = entities.Values
            .OfType<MonsterEntity>()
            .Where(m => m.TargetEntityId == owner.Id && IsValidPetTarget(channel, owner, m, petLeashRange))
            .OrderBy(m => DistanceSquared(pet.X, pet.Y, m.X, m.Y))
            .FirstOrDefault();

        pet.TargetEntityId = mobTargetingOwner?.Id ?? 0;
        return mobTargetingOwner;
    }

    private bool IsValidPetTarget(Channel channel, PlayerEntity owner, Entity target, float petLeashRange)
    {
        if (target.Id == owner.Id || target.Health <= 0 || target.Type == EntityType.NPC || target.Type == EntityType.Pet)
            return false;
        if (DistanceSquared(owner.X, owner.Y, target.X, target.Y) > petLeashRange * petLeashRange)
            return false;
        return CanDamageEntity(owner, target, out _);
    }

    private bool TryUpdatePetCollection(Channel channel, PetEntity pet, PlayerEntity owner, float collectRange, float touchRange)
    {
        var ownerSession = _sessions.Values.FirstOrDefault(s => s.EntityId == owner.Id);
        if (ownerSession?.SelectedCharacter == null)
            return false;

        var collarExpiry = _db.LoadPetCollarExpiry(ownerSession.SelectedCharacter.Id);
        if (collarExpiry <= DateTime.UtcNow)
            return false;

        var loot = channel.GetLootInRadius(owner.X, owner.Y, collectRange)
            .Where(l => CanPickupLoot(owner, l))
            .OrderBy(l => DistanceSquared(pet.X, pet.Y, l.X, l.Y))
            .FirstOrDefault();
        if (loot == null)
            return false;

        float dx = loot.X - pet.X;
        float dy = loot.Y - pet.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > touchRange)
        {
            MovePetTowards(channel, pet, dx, dy, dist, pet.Speed);
            return true;
        }

        pet.Moving = false;
        if (_gameTime >= pet.NextLootPickupTime)
        {
            pet.NextLootPickupTime = _gameTime + 0.25;
            TryPickupLootForPlayer(ownerSession.Peer, ownerSession, channel, owner, loot, collectRange);
        }
        return true;
    }

    private static void FollowOwner(Channel channel, PetEntity pet, Entity owner, float followDistance, float deadZone)
    {
        float dirX = owner.DirX;
        float dirY = owner.DirY;
        float lenSq = dirX * dirX + dirY * dirY;
        if (lenSq < 0.001f)
        {
            dirX = -1f;
            dirY = 0.35f;
        }
        else
        {
            float len = MathF.Sqrt(lenSq);
            dirX /= len;
            dirY /= len;
        }

        float desiredX = owner.X - dirX * followDistance;
        float desiredY = owner.Y - dirY * followDistance + 18f;
        float dx = desiredX - pet.X;
        float dy = desiredY - pet.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist > deadZone)
            MovePetTowards(channel, pet, dx, dy, dist, pet.Speed);
        else
            pet.Moving = false;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return dx * dx + dy * dy;
    }

    private static void MovePetTowards(Channel channel, PetEntity pet, float dx, float dy, float dist, float speed)
    {
        if (dist <= 0.001f)
        {
            pet.Moving = false;
            return;
        }

        float step = MathF.Min(dist, MathF.Max(10f, speed / 20f));
        channel.MoveEntity(pet.Id, pet.X + dx / dist * step, pet.Y + dy / dist * step);
        pet.DirX = dx / dist;
        pet.DirY = dy / dist;
        pet.Moving = true;
    }

    private static string GetFactionForRace(string race)
    {
        return race.ToLowerInvariant() switch
        {
            "humano" or "elfo" or "troll" => "solari",
            "dark elfo" or "morto vivo" or "orc" => "noctori",
            _ => "solari",
        };
    }
}
