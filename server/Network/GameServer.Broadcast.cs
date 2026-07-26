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
            writer.Put(aoiEntity is MonsterEntity monster ? (byte)monster.AIState : (byte)0);
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
        writer.Put(entity is MonsterEntity monster ? (byte)monster.AIState : (byte)0);
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

    private void UpdateServerPets(Channel channel, Dictionary<ulong, Entity> entities)
    {
        foreach (var pet in entities.Values.OfType<PetEntity>().ToList())
        {
            if (!entities.TryGetValue(pet.OwnerEntityId, out var owner) || owner.Type != EntityType.Player)
            {
                channel.RemoveEntity(pet.Id);
                BroadcastDespawn(channel, pet.Id);
                continue;
            }

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

            float desiredX = owner.X - dirX * 54f;
            float desiredY = owner.Y - dirY * 54f + 18f;
            float dx = desiredX - pet.X;
            float dy = desiredY - pet.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist > 4f)
            {
                float step = MathF.Min(dist, MathF.Max(16f, pet.Speed / 20f));
                channel.MoveEntity(pet.Id, pet.X + dx / dist * step, pet.Y + dy / dist * step);
                pet.DirX = dx / dist;
                pet.DirY = dy / dist;
                pet.Moving = true;
            }
            else
            {
                pet.Moving = false;
            }
        }
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
