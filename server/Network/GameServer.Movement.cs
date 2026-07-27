using LiteNetLib;
using LiteNetLib.Utils;
using Mithara.Server.Entities;
using Mithara.Server.Packets;
using Mithara.Server.World;

namespace Mithara.Server.Network;

partial class GameServer
{
    private const float MaxPlayerSpeed = 360f;
    private const float MaxWorldCoordinate = 20000f;
    private const double PassiveTeleportCheckInterval = 0.10;

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
        if (actionType != 1 && actionType != 2) return;
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

        BroadcastPlayerAction(channel, entity, actionType, dirX, dirY, includeSelf: false);
    }

    private void BroadcastPlayerAction(Channel channel, PlayerEntity entity, byte actionType, float dirX, float dirY, bool includeSelf)
    {
        var nearby = channel.GetEntitiesInAoi(entity.X, entity.Y);
        foreach (var entityId in nearby)
        {
            if (!includeSelf && entityId == entity.Id) continue;
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
            if (_gameTime - session.TransitionStartTime < 0.35) return;
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

        if (entity is PlayerEntity playerEntity && HasMovementBlockingDebuff(playerEntity))
        {
            playerEntity.Moving = false;
            playerEntity.Sprinting = false;
            playerEntity.LastMoveTime = _gameTime;
            BroadcastAuthoritativeMove(channel, playerEntity, playerEntity.X, playerEntity.Y, playerEntity.DirX, playerEntity.DirY);
            return;
        }

        float dx = targetX - entity.X;
        float dy = targetY - entity.Y;
        float distSq = dx * dx + dy * dy;

        float dt = MathF.Max((float)(_gameTime - entity.LastMoveTime), 0.001f);
        float effectiveMaxSpeed = GetEffectivePlayerMoveSpeed(entity);
        float maxDistSq = effectiveMaxSpeed * effectiveMaxSpeed * dt * dt;

        if (distSq > maxDistSq * 1.5f && distSq > 100f)
        {
            float scale = MathF.Sqrt(maxDistSq / distSq);
            targetX = entity.X + dx * scale;
            targetY = entity.Y + dy * scale;
            if (!_lastMoveValidationLog.TryGetValue(entity.Id, out var lastLog) || _gameTime - lastLog >= 2.0)
            {
                _lastMoveValidationLog[entity.Id] = _gameTime;
                Logger.Info($"Move validation: {entity.Name} speed {MathF.Sqrt(distSq)/dt:F0}px/s (max {effectiveMaxSpeed:F0})");
            }
        }

        ResolveMapCollision(session.CurrentMap, entity.X, entity.Y, ref targetX, ref targetY);

        entity.Moving = moving;
        entity.Sprinting = moving && sprinting;
        entity.DirX = dirX;
        entity.DirY = dirY;
        entity.LastMoveTime = _gameTime;
        channel.MoveEntity(session.EntityId, targetX, targetY);
        if (entity is PlayerEntity movedPlayer)
            UpdateQuestReachLocationProgress(movedPlayer, targetX, targetY);

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

    private bool HasMovementBlockingDebuff(PlayerEntity player)
    {
        foreach (string key in new[] { "stun", "sleep", "root", "freeze", "prison" })
        {
            if (player.ActiveServerBuffs.TryGetValue(key, out double until) && until > _gameTime)
                return true;
        }

        return false;
    }

    private float GetEffectivePlayerMoveSpeed(Entity entity)
    {
        if (entity is not PlayerEntity player)
            return MaxPlayerSpeed;

        float multiplier = 1f;
        foreach (var kv in player.ActiveServerBuffs.ToList())
        {
            if (kv.Value <= _gameTime)
            {
                player.ActiveServerBuffs.Remove(kv.Key);
                continue;
            }

            if (kv.Key.StartsWith("slow:", StringComparison.OrdinalIgnoreCase))
                multiplier = MathF.Min(multiplier, 0.60f);
        }

        return MaxPlayerSpeed * multiplier * player.CalculateMovementSpeedMultiplier();
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
            if (!float.IsFinite(stopX) || !float.IsFinite(stopY)
                || MathF.Abs(stopX) > MaxWorldCoordinate
                || MathF.Abs(stopY) > MaxWorldCoordinate)
            {
                Logger.Info($"AVISO Stop validation: {entity.Name} enviou posição inválida ({stopX:F1}, {stopY:F1})");
            }
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
        return tiles.TryGetValue((tileX, tileY), out byte type) && IsFullBlockTileType(type);
    }

    private bool CheckTeleportTile(NetPeer peer, PlayerSession session, Channel channel, Entity entity, float x, float y)
    {
        if (_gameTime - session.LastTeleportTime < 0.20) return false;
        string sceneName = string.IsNullOrWhiteSpace(session.CurrentMap)
            ? "main"
            : session.CurrentMap.Trim().ToLowerInvariant();

        if (!TryFindTeleportAtPosition(sceneName, x, y, out int tileX, out int tileY, out var info))
            return false;

        if (IsTeleportSuppressed(session, sceneName, tileX, tileY, x, y))
            return false;

        return ExecuteTeleport(peer, session, channel, entity, sceneName, tileX, tileY, info, x, y);
    }

    private void HandleSceneTeleport(NetPeer peer, NetDataReader reader)
    {
        if (!_sessions.TryGetValue(peer, out var session) || session.IsTransitioning)
            return;

        int pairId = reader.GetInt();
        if (pairId <= 0 || _gameTime - session.LastTeleportTime < 0.20)
            return;

        var channel = _world.GetChannel(session.ChannelId);
        var entity = channel?.GetEntity(session.EntityId);
        if (channel == null || entity == null || entity.Health <= 0)
            return;

        string sceneName = string.IsNullOrWhiteSpace(session.CurrentMap)
            ? "main"
            : session.CurrentMap.Trim().ToLowerInvariant();
        if (!_teleportTargets.TryGetValue(sceneName, out var teleports))
            return;

        foreach (var teleport in teleports)
        {
            if (teleport.Value.PairId != pairId)
                continue;

            Logger.Info($"[TELEPORT PAIR] {entity.Name} tocou PairId={pairId} em {sceneName}.");
            ExecuteTeleport(
                peer,
                session,
                channel,
                entity,
                sceneName,
                teleport.Key.X,
                teleport.Key.Y,
                teleport.Value,
                entity.X,
                entity.Y);
            return;
        }
    }

    private bool CheckTeleportAlongMovement(
        NetPeer peer,
        PlayerSession session,
        Channel channel,
        Entity entity,
        float fromX,
        float fromY,
        float toX,
        float toY)
    {
        if (_gameTime - session.LastTeleportTime < 0.20)
            return false;

        string sceneName = string.IsNullOrWhiteSpace(session.CurrentMap)
            ? "main"
            : session.CurrentMap.Trim().ToLowerInvariant();

        if (!_teleportTargets.TryGetValue(sceneName, out var teleports))
            return false;

        const float playerRadius = 16f;
        float movementMinX = MathF.Min(fromX, toX) - playerRadius;
        float movementMaxX = MathF.Max(fromX, toX) + playerRadius;
        float movementMinY = MathF.Min(fromY, toY) - playerRadius;
        float movementMaxY = MathF.Max(fromY, toY) + playerRadius;

        foreach (var teleport in teleports)
        {
            int tileX = teleport.Key.X;
            int tileY = teleport.Key.Y;
            float tileMinX = tileX * 32f;
            float tileMaxX = tileMinX + 32f;
            float tileMinY = tileY * 32f;
            float tileMaxY = tileMinY + 32f;

            if (movementMaxX < tileMinX || movementMinX > tileMaxX
                || movementMaxY < tileMinY || movementMinY > tileMaxY)
                continue;

            float contactX = Math.Clamp(toX, tileMinX, tileMaxX);
            float contactY = Math.Clamp(toY, tileMinY, tileMaxY);
            if (IsTeleportSuppressed(session, sceneName, tileX, tileY, contactX, contactY))
                continue;

            return ExecuteTeleport(
                peer,
                session,
                channel,
                entity,
                sceneName,
                tileX,
                tileY,
                teleport.Value,
                contactX,
                contactY);
        }

        return false;
    }

    private bool ExecuteTeleport(
        NetPeer peer,
        PlayerSession session,
        Channel channel,
        Entity entity,
        string sceneName,
        int tileX,
        int tileY,
        TeleportTileInfo info,
        float entryX,
        float entryY)
    {
        session.LastTeleportTime = _gameTime;
        session.IsTransitioning = true;
        session.TransitionStartTime = _gameTime;
        session.TeleportEntryX = entryX;
        session.TeleportEntryY = entryY;
        session.CurrentMap = info.TargetScene;
        session.SpawnedEntities.Clear();
        session.SpawnedLoot.Clear();

        if (session.SelectedCharacter != null)
        {
            session.SelectedCharacter.PosX = info.TargetX;
            session.SelectedCharacter.PosY = info.TargetY;
            _db.SaveCharacterPosition(session.SelectedCharacter.Id, info.TargetX, info.TargetY, info.TargetScene);
        }

        channel.MoveEntity(entity.Id, info.TargetX, info.TargetY);
        SuppressDestinationTeleport(session, info.TargetScene, info.TargetX, info.TargetY);
        SendSceneChange(peer, info.TargetScene, info.TargetX, info.TargetY);
        Logger.Info($"[TELEPORT] {entity.Name} tile ({tileX},{tileY}) -> {info.TargetScene} ({info.TargetX:F1},{info.TargetY:F1})");
        return true;
    }

    private static bool IsTeleportSuppressed(PlayerSession session, string sceneName, int tileX, int tileY, float x, float y)
    {
        if (!string.Equals(session.SuppressedTeleportScene, sceneName, StringComparison.OrdinalIgnoreCase)
            || session.SuppressedTeleportTileX != tileX
            || session.SuppressedTeleportTileY != tileY)
            return false;

        float centerX = tileX * 32f + 16f;
        float centerY = tileY * 32f + 16f;
        float dx = x - centerX;
        float dy = y - centerY;
        const float clearRadius = 48f;
        if (dx * dx + dy * dy <= clearRadius * clearRadius)
            return true;

        ClearSuppressedTeleport(session);
        return false;
    }

    private static void ClearSuppressedTeleport(PlayerSession session)
    {
        session.SuppressedTeleportScene = "";
        session.SuppressedTeleportTileX = int.MinValue;
        session.SuppressedTeleportTileY = int.MinValue;
    }

    private static void SuppressDestinationTeleport(PlayerSession session, string targetScene, float targetX, float targetY)
    {
        ClearSuppressedTeleport(session);
        if (!_teleportTargets.TryGetValue(targetScene, out var teleports))
            return;

        const float adjacentDistance = 32f;
        float bestDistanceSq = adjacentDistance * adjacentDistance + 0.01f;
        (int X, int Y)? nearestTile = null;

        foreach (var teleport in teleports)
        {
            float centerX = teleport.Key.X * 32f + 16f;
            float centerY = teleport.Key.Y * 32f + 16f;
            float dx = targetX - centerX;
            float dy = targetY - centerY;
            float distanceSq = dx * dx + dy * dy;
            if (distanceSq > bestDistanceSq)
                continue;

            bestDistanceSq = distanceSq;
            nearestTile = teleport.Key;
        }

        if (nearestTile == null)
            return;

        session.SuppressedTeleportScene = targetScene;
        session.SuppressedTeleportTileX = nearestTile.Value.X;
        session.SuppressedTeleportTileY = nearestTile.Value.Y;
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

        return false;
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
