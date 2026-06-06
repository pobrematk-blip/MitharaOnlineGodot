using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private bool TryGetPlayer(NetPeer peer, out PlayerEntity? player, out Channel? channel)
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
        foreach (var ch in _world.GetAllChannels())
        {
            foreach (var kv in ch.GetAllEntities())
            {
                if (kv.Value.Type != EntityType.Player) continue;
                if (kv.Key == entity.Id) continue;

                float dx = kv.Value.X - entity.X;
                float dy = kv.Value.Y - entity.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > _config.AoiRadius) continue;

                var peer = ch.GetPlayerPeer(kv.Key);
                if (peer == null) continue;

                var writer = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
                WriteEntityPacket(writer, entity);
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }
    }

    private void BroadcastEntityUpdates()
    {
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

                var aoi = channel.GetEntitiesInAoi(playerEntity.X, playerEntity.Y);
                var filteredWriter = PacketSerializer.WritePacket(PacketId.S2C_EntityUpdate);
                int count = 0;
                var tempWriter = new NetDataWriter();

                foreach (var aoiEid in aoi)
                {
                    if (!entities.TryGetValue(aoiEid, out var aoiEntity)) continue;
                    count++;
                    tempWriter.Put(aoiEid);
                    tempWriter.Put(aoiEntity.X);
                    tempWriter.Put(aoiEntity.Y);
                    tempWriter.Put(aoiEntity.DirX);
                    tempWriter.Put(aoiEntity.DirY);
                    tempWriter.Put(aoiEntity.Moving);
                    tempWriter.Put(aoiEntity.Health);
                    tempWriter.Put(aoiEntity.MaxHealth);
                    tempWriter.Put(aoiEntity.Mana);
                    tempWriter.Put(aoiEntity.MaxMana);
                    tempWriter.Put(aoiEntity.Level);
                    tempWriter.Put(aoiEntity.Name);
                    tempWriter.Put(aoiEntity.FactionId);
                }

                filteredWriter.Put(count);
                filteredWriter.Put(tempWriter.CopyData());
                peer.Send(filteredWriter, DeliveryMethod.Unreliable);
            }
        }
    }

    private void BroadcastSpawnToNearby(Channel channel, Entity entity, float x, float y)
    {
        var aoi = channel.GetEntitiesInAoi(x, y);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_SpawnEntity);
        WriteEntityPacket(writer, entity);

        foreach (var eid in aoi)
        {
            if (eid == entity.Id) continue;
            var peer = channel.GetPlayerPeer(eid);
            peer?.Send(writer, DeliveryMethod.ReliableOrdered);
        }
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
        }
        else if (entity is MonsterEntity mob)
        {
            writer.Put(mob.IsBoss);
            writer.Put(mob.ExperienceReward);
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
