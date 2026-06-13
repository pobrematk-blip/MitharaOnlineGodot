using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const float MaxPlayerSpeed = 333f;
    private const float MaxPlayerSpeedSq = MaxPlayerSpeed * MaxPlayerSpeed;

    private void HandlePlayerMove(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        float targetX = reader.GetFloat();
        float targetY = reader.GetFloat();
        float dirX = reader.GetFloat();
        float dirY = reader.GetFloat();

        var entity = channel.GetEntity(session.EntityId);
        if (entity == null) return;

        float dx = targetX - entity.X;
        float dy = targetY - entity.Y;
        float distSq = dx * dx + dy * dy;

        float dt = MathF.Max((float)(_gameTime - entity.LastMoveTime), 0.001f);
        float maxDistSq = MaxPlayerSpeedSq * dt * dt;

        if (distSq > maxDistSq * 1.5f && distSq > 100f)
        {
            float scale = MathF.Sqrt(maxDistSq / distSq);
            targetX = entity.X + dx * scale;
            targetY = entity.Y + dy * scale;
            Logger.Info($"Move validation: {entity.Name} speed {MathF.Sqrt(distSq)/dt:F0}px/s (max {MaxPlayerSpeed})");
        }

        entity.Moving = true;
        entity.DirX = dirX;
        entity.DirY = dirY;
        entity.LastMoveTime = _gameTime;
        channel.MoveEntity(session.EntityId, targetX, targetY);

        var aoi = channel.GetEntitiesInAoi(targetX, targetY);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_EntityMove);
        writer.Put(session.EntityId);
        writer.Put(targetX);
        writer.Put(targetY);
        writer.Put(dirX);
        writer.Put(dirY);
        writer.Put(entity.Moving);

        foreach (var eid in aoi)
        {
            if (eid == session.EntityId) continue;
            var otherPeer = channel.GetPlayerPeer(eid);
            otherPeer?.Send(writer, DeliveryMethod.Unreliable);
        }
    }

    private void HandlePlayerStop(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var entity = channel.GetEntity(session.EntityId);
        if (entity == null) return;

        entity.Moving = false;
        if (reader.AvailableBytes >= 8)
        {
            entity.X = reader.GetFloat();
            entity.Y = reader.GetFloat();
        }

        if (session.SelectedCharacter != null)
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, entity.X, entity.Y);
    }

    private void HandleChannelSwitch(NetPeer peer, NetDataReader reader)
    {
        int newChannelId = reader.GetInt();
        if (!_sessions.TryGetValue(peer, out var session)) return;
        if (session.SelectedCharacter == null) return;

        var oldChannel = _world.GetChannel(session.ChannelId);
        oldChannel?.RemoveEntity(session.EntityId);
        if (oldChannel != null)
            BroadcastDespawn(oldChannel, session.EntityId);

        session.ChannelId = newChannelId;
        var newChannel = _world.GetOrCreateChannel(newChannelId);
        var entity = oldChannel?.GetEntity(session.EntityId);

        if (entity != null)
        {
            newChannel.AddEntity(entity, peer);

            var writer = PacketSerializer.WritePacket(PacketId.S2C_EnterWorld);
            writer.Put(session.EntityId);
            writer.Put(newChannelId);
            writer.Put(entity.X);
            writer.Put(entity.Y);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }
}
