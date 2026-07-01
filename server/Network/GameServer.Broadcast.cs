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
            foreach (var kv in ch.GetAllEntities())
            {
                if (kv.Value.Type != EntityType.Player) continue;
                if (kv.Key == entity.Id) continue;

                float dx = kv.Value.X - entity.X;
                float dy = kv.Value.Y - entity.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                var peer = ch.GetPlayerPeer(kv.Key);
                if (peer == null) continue;

                // Send spawn regardless of distance — client handles visual culling
                var writer = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                WriteEntityPacket(writer, entity);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
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
            var players = entities.Where(kv => kv.Value.Type == EntityType.Player).ToList();

            foreach (var playerKv in players)
            {
                ulong eid = playerKv.Key;
                var playerEntity = playerKv.Value;
                var peer = channel.GetPlayerPeer(eid);
                if (peer == null) continue;

                if (!_sessions.TryGetValue(peer, out var session)) continue;

                var aoi = channel.GetEntitiesInAoi(playerEntity.X, playerEntity.Y);

                var aoiSet = new HashSet<ulong>(aoi);

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

    private static bool IsLootInAoi(LootEntity loot, float x, float y)
    {
        float dx = loot.X - x;
        float dy = loot.Y - y;
        return (dx * dx) + (dy * dy) <= Channel.AoiRadius * Channel.AoiRadius;
    }

    private void SyncLootVisibility(NetPeer peer, PlayerSession session, Channel channel, float x, float y)
    {
        var visibleLoot = channel.GetAllLoot()
            .Where(loot => IsLootInAoi(loot, x, y))
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
        peer.Send(packet, DeliveryMethod.ReliableOrdered);
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
        var writer = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
        WriteEntityPacket(writer, entity);

        int sent = 0;
        int totalPlayers = 0;
        foreach (var kv in channel.GetAllEntities())
        {
            if (kv.Value.Type != EntityType.Player) continue;
            totalPlayers++;
            if (kv.Key == entity.Id) continue;
            var peer = channel.GetPlayerPeer(kv.Key);
            if (peer != null)
            {
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
                if (_sessions.TryGetValue(peer, out var session))
                    session.SpawnedEntities.Add(entity.Id);
                sent++;
            }
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
