using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const float MaxPlayerSpeed = 333f;
    private const float MaxPlayerSpeedSq = MaxPlayerSpeed * MaxPlayerSpeed;

    private void HandlePlayerAction(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;
        var entity = channel.GetEntity(session.EntityId) as PlayerEntity;
        if (entity == null || entity.Health <= 0) return;

        byte actionType = reader.GetByte();
        float dirX = reader.GetFloat();
        float dirY = reader.GetFloat();
        if (actionType != 1) return;
        if (_gameTime - session.LastActionTime < 0.10) return;
        session.LastActionTime = _gameTime;

        float length = MathF.Sqrt(dirX * dirX + dirY * dirY);
        if (length > 0.001f)
        {
            dirX /= length;
            dirY /= length;
            entity.DirX = dirX;
            entity.DirY = dirY;
        }

        var nearby = channel.GetEntitiesInAoi(entity.X, entity.Y);
        foreach (var entityId in nearby)
        {
            if (entityId == entity.Id) continue;
            var targetPeer = channel.GetPlayerPeer(entityId);
            if (targetPeer == null) continue;

            var writer = PacketSerializer.WritePacket(PacketId.S2C_PlayerAction);
            writer.Put(entity.Id);
            writer.Put(actionType);
            writer.Put(dirX);
            writer.Put(dirY);
            targetPeer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    private void HandlePlayerMove(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session)) return;

        if (session.IsTransitioning)
        {
            if (_gameTime - session.TransitionStartTime < 3.0) return;
            session.IsTransitioning = false;
        }

        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        float targetX = reader.GetFloat();
        float targetY = reader.GetFloat();
        float dirX = reader.GetFloat();
        float dirY = reader.GetFloat();
        bool moving = reader.GetBool();
        bool sprinting = reader.GetBool();

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

        ResolveMapCollision(session.CurrentMap, entity.X, entity.Y, ref targetX, ref targetY);

        entity.Moving = moving;
        entity.Sprinting = moving && sprinting;
        entity.DirX = dirX;
        entity.DirY = dirY;
        entity.LastMoveTime = _gameTime;
        channel.MoveEntity(session.EntityId, targetX, targetY);

        if (CheckTeleportTile(peer, session, channel, entity, targetX, targetY))
            return;

        var aoi = channel.GetEntitiesInAoi(targetX, targetY);
        var writer = PacketSerializer.WritePacket(PacketId.S2C_EntityMove);
        writer.Put(session.EntityId);
        writer.Put(targetX);
        writer.Put(targetY);
        writer.Put(dirX);
        writer.Put(dirY);
        writer.Put(entity.Moving);
        writer.Put(entity.Sprinting);

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
        if (session.IsTransitioning) return;
        var channel = _world.GetChannel(session.ChannelId);
        if (channel == null) return;

        var entity = channel.GetEntity(session.EntityId);
        if (entity == null) return;

        entity.Moving = false;
        entity.Sprinting = false;
        if (reader.AvailableBytes >= 8)
        {
            float stopX = reader.GetFloat();
            float stopY = reader.GetFloat();
            channel.MoveEntity(session.EntityId, stopX, stopY);
        }

        if (session.SelectedCharacter != null)
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, entity.X, entity.Y, session.CurrentMap);
    }

    private void ResolveMapCollision(string sceneName, float currentX, float currentY, ref float targetX, ref float targetY)
    {
        if (!IsBlockedTile(sceneName, targetX, targetY))
            return;

        bool canMoveX = !IsBlockedTile(sceneName, targetX, currentY);
        bool canMoveY = !IsBlockedTile(sceneName, currentX, targetY);

        if (canMoveX && !canMoveY)
        {
            targetY = currentY;
            return;
        }

        if (!canMoveX && canMoveY)
        {
            targetX = currentX;
            return;
        }

        targetX = currentX;
        targetY = currentY;
    }

    private bool IsBlockedTile(string sceneName, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            sceneName = "main";

        sceneName = sceneName.ToLowerInvariant();
        if (!_tileData.TryGetValue(sceneName, out var tiles))
            return false;

        int tileX = (int)MathF.Floor(x / 32f);
        int tileY = (int)MathF.Floor(y / 32f);
        return tiles.TryGetValue((tileX, tileY), out byte type) && type == 0;
    }

    private bool CheckTeleportTile(NetPeer peer, PlayerSession session, Channel channel, Entity entity, float x, float y)
    {
        if (_gameTime - session.LastTeleportTime < 1.0) return false;
        string sceneName = string.IsNullOrWhiteSpace(session.CurrentMap)
            ? "main"
            : session.CurrentMap.Trim().ToLowerInvariant();

        if (!TryFindTeleportAtPosition(sceneName, x, y, out int tileX, out int tileY, out var info))
            return false;

        session.LastTeleportTime = _gameTime;
        session.IsTransitioning = true;
        session.TransitionStartTime = _gameTime;
        session.TeleportEntryX = x;
        session.TeleportEntryY = y;
        session.CurrentMap = info.TargetScene;

        if (session.SelectedCharacter != null)
        {
            session.SelectedCharacter.PosX = info.TargetX;
            session.SelectedCharacter.PosY = info.TargetY;
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, info.TargetX, info.TargetY, info.TargetScene);
        }

        channel.MoveEntity(entity.Id, info.TargetX, info.TargetY);
        SendSceneChange(peer, info.TargetScene, info.TargetX, info.TargetY);
        Logger.Info($"[TELEPORT] {entity.Name} tile ({tileX},{tileY}) -> {info.TargetScene} ({info.TargetX:F1},{info.TargetY:F1})");
        return true;
    }

    private static bool TryFindTeleportAtPosition(string sceneName, float x, float y, out int tileX, out int tileY, out TeleportTileInfo info)
    {
        tileX = (int)MathF.Floor(x / 32f);
        tileY = (int)MathF.Floor(y / 32f);
        info = null!;

        if (!_teleportTargets.TryGetValue(sceneName, out var teleports))
            return false;

        if (teleports.TryGetValue((tileX, tileY), out info!))
            return true;

        const float activationRadius = 46f;
        float bestDistanceSq = activationRadius * activationRadius;
        int bestTileX = tileX;
        int bestTileY = tileY;
        TeleportTileInfo? bestInfo = null;

        for (int ox = -1; ox <= 1; ox++)
        {
            for (int oy = -1; oy <= 1; oy++)
            {
                if (ox == 0 && oy == 0)
                    continue;

                int checkX = tileX + ox;
                int checkY = tileY + oy;
                if (!teleports.TryGetValue((checkX, checkY), out var candidate))
                    continue;

                float centerX = checkX * 32f + 16f;
                float centerY = checkY * 32f + 16f;
                float dx = x - centerX;
                float dy = y - centerY;
                float distSq = dx * dx + dy * dy;
                if (distSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distSq;
                bestTileX = checkX;
                bestTileY = checkY;
                bestInfo = candidate;
            }
        }

        if (bestInfo == null)
            return false;

        tileX = bestTileX;
        tileY = bestTileY;
        info = bestInfo;
        return true;
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
