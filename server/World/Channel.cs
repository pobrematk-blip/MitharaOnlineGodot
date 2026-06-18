using Mithara.Server.Entities;
using Mithara.Server.World.Pathfinding;
using LiteNetLib;
using System.Linq;

namespace Mithara.Server.World;

public class Channel
{
    // Callback for monster attacks: (channel, monster, target, gameTime) -> true if target died
    public Func<Channel, MonsterEntity, Entity, double, bool>? OnMonsterAttack;
    public int Id { get; }
    public string Name { get; }

    public List<NoMobZone> NoMobZones { get; set; } = new();
    public PathfindingGrid? PathGrid { get; set; }

    private readonly Dictionary<ulong, Entity> _entities = new();
    private readonly SpatialGrid _grid;
    private readonly Dictionary<ulong, NetPeer> _playerPeers = new();
    private readonly SpawnerManager _spawner;
    private readonly List<SpawnPoint> _respawnQueue = new();
    private readonly List<(SpawnPoint point, double respawnAt)> _pendingRespawns = new();
    private readonly Dictionary<string, int> _spawnCounts = new();
    private readonly Dictionary<string, List<ulong>> _spawnedByPrefab = new();
    private readonly List<LootEntity> _lootItems = new();
    private readonly Dictionary<ulong, PathFollower> _pathFollowers = new();
    private readonly Dictionary<ulong, double> _lastPathfindTime = new();
    private ulong _nextEntityId = 1;
    private bool _initialSpawned = false;

    public const float AoiRadius = 1200f;
    private const float MonsterSeparationRadius = 52f;
    private const float MonsterSeparationStrength = 0.65f;

    public SpawnerManager Spawner => _spawner;
    public event Action<Entity>? EntitySpawned;

    public Channel(int id, string name)
    {
        Id = id;
        Name = name;
        _grid = new SpatialGrid();
        _spawner = new SpawnerManager();
    }

    public void SpawnNpcs(NpcManager npcManager)
    {
        foreach (var point in npcManager.GetSpawnPoints())
        {
            var npc = npcManager.CreateNpc(point);
            if (npc != null)
            {
                if (PathGrid != null)
                {
                    var (gx, gy) = PathGrid.WorldToGrid(npc.X, npc.Y);
                    PathGrid.SetBlocked(gx, gy, true);
                }
                AddEntity(npc);
            }
        }
    }

    public ulong NextEntityId() => _nextEntityId++;

    public ulong AddEntity(Entity entity, NetPeer? peer = null)
    {
        entity.Id = NextEntityId();
        _entities[entity.Id] = entity;
        _grid.AddEntity(entity.Id, entity.X, entity.Y);

        if (entity.Type == EntityType.Player && peer != null)
            _playerPeers[entity.Id] = peer;

        if (entity is MonsterEntity mob)
        {
            if (!_spawnedByPrefab.ContainsKey(mob.PrefabId))
                _spawnedByPrefab[mob.PrefabId] = new List<ulong>();
            _spawnedByPrefab[mob.PrefabId].Add(entity.Id);
        }

        EntitySpawned?.Invoke(entity);

        return entity.Id;
    }

    public void RemoveEntity(ulong entityId)
    {
        if (!_entities.TryGetValue(entityId, out var entity)) return;

        if (entity is MonsterEntity mob && _spawnedByPrefab.TryGetValue(mob.PrefabId, out var list))
        {
            list.Remove(entityId);
            if (list.Count == 0) _spawnedByPrefab.Remove(mob.PrefabId);
        }

        _grid.RemoveEntity(entityId);
        _playerPeers.Remove(entityId);
        _entities.Remove(entityId);
    }

    public Entity? GetEntity(ulong entityId)
    {
        _entities.TryGetValue(entityId, out var entity);
        return entity;
    }

    public NetPeer? GetPlayerPeer(ulong entityId)
    {
        _playerPeers.TryGetValue(entityId, out var peer);
        return peer;
    }

    public void MoveEntity(ulong entityId, float newX, float newY)
    {
        if (!_entities.TryGetValue(entityId, out var entity)) return;
        float oldX = entity.X, oldY = entity.Y;
        entity.X = newX;
        entity.Y = newY;
        _grid.MoveEntity(entityId, oldX, oldY, newX, newY);
    }

    public HashSet<ulong> GetEntitiesInAoi(float x, float y)
    {
        return _grid.GetEntitiesInRadius(x, y, AoiRadius);
    }

    public Dictionary<ulong, Entity> GetAllEntities() => _entities;

    public int CountMonstersByPrefab(string prefabId)
    {
        return _spawnedByPrefab.TryGetValue(prefabId, out var list) ? list.Count : 0;
    }

    public void AddLoot(LootEntity loot)
    {
        _lootItems.Add(loot);
    }

    public void RemoveLoot(ulong lootId)
    {
        _lootItems.RemoveAll(l => l.Id == lootId);
    }

    public LootEntity? GetLoot(ulong lootId)
    {
        return _lootItems.FirstOrDefault(l => l.Id == lootId);
    }

    public List<LootEntity> GetLootInRadius(float x, float y, float radius)
    {
        return _lootItems.Where(l => !l.PickedUp && MathF.Sqrt(MathF.Pow(l.X - x, 2) + MathF.Pow(l.Y - y, 2)) <= radius).ToList();
    }

    public void Update(float dt, double gameTime)
    {
        UpdateMonsterAI(dt, gameTime);
        UpdateSpawner(gameTime);
        UpdateLootCleanup(gameTime);
    }

    private void UpdateLootCleanup(double gameTime)
    {
        const double lootDuration = 30.0;
        for (int i = _lootItems.Count - 1; i >= 0; i--)
        {
            if (gameTime - _lootItems[i].SpawnTime >= lootDuration)
                _lootItems.RemoveAt(i);
        }
    }

    private bool IsInNoMobZone(float x, float y)
    {
        foreach (var zone in NoMobZones)
        {
            if (zone.Contains(x, y))
                return true;
        }
        return false;
    }

    private PathFollower GetOrCreatePathFollower(ulong entityId)
    {
        if (!_pathFollowers.TryGetValue(entityId, out var follower))
        {
            follower = new PathFollower(PathGrid ?? new PathfindingGrid(200, 200, 32f));
            _pathFollowers[entityId] = follower;
        }
        return follower;
    }

    private (float x, float y) ApplyMonsterSeparation(MonsterEntity mob, float desiredX, float desiredY, float dt, bool usePathfinding)
    {
        float pushX = 0f;
        float pushY = 0f;

        foreach (var other in _entities.Values)
        {
            if (other.Id == mob.Id || other is not MonsterEntity otherMob || otherMob.Health <= 0)
                continue;

            float dx = desiredX - otherMob.X;
            float dy = desiredY - otherMob.Y;
            float distSq = dx * dx + dy * dy;
            if (distSq <= 0.001f || distSq >= MonsterSeparationRadius * MonsterSeparationRadius)
                continue;

            float dist = MathF.Sqrt(distSq);
            float weight = (MonsterSeparationRadius - dist) / MonsterSeparationRadius;
            pushX += dx / dist * weight;
            pushY += dy / dist * weight;
        }

        float pushLen = MathF.Sqrt(pushX * pushX + pushY * pushY);
        if (pushLen <= 0.001f)
            return (desiredX, desiredY);

        float maxPush = mob.Speed * dt * MonsterSeparationStrength;
        float adjustedX = desiredX + pushX / pushLen * maxPush;
        float adjustedY = desiredY + pushY / pushLen * maxPush;

        if (IsInNoMobZone(adjustedX, adjustedY))
            return (desiredX, desiredY);

        if (usePathfinding && PathGrid != null && !PathGrid.IsWalkableWorld(adjustedX, adjustedY))
            return (desiredX, desiredY);

        return (adjustedX, adjustedY);
    }

    private void UpdateMonsterAI(float dt, double gameTime)
    {
        foreach (var kv in _entities.ToList())
        {
            if (kv.Value is not MonsterEntity mob) continue;
            if (mob.Health <= 0) continue;

            bool usePathfinding = PathGrid != null;
            var pathFollower = usePathfinding ? GetOrCreatePathFollower(mob.Id) : null;

            if (mob.TargetEntityId.HasValue)
            {
                if (!_entities.TryGetValue(mob.TargetEntityId.Value, out var target) || target.Health <= 0)
                {
                    mob.TargetEntityId = null;
                    pathFollower?.Stop();
                    continue;
                }

                float dx = target.X - mob.X;
                float dy = target.Y - mob.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist <= mob.AttackRange)
                {
                    mob.Moving = false;
                    pathFollower?.Stop();
                    if (dist > 0.001f)
                    {
                        mob.DirX = dx / dist;
                        mob.DirY = dy / dist;
                    }
                    if (gameTime - mob.LastAttackTime >= mob.AttackCooldown)
                    {
                        mob.LastAttackTime = gameTime;
                        bool targetDied = OnMonsterAttack?.Invoke(this, mob, target, gameTime) ?? false;
                        if (targetDied)
                            mob.TargetEntityId = null;
                    }
                }
                else
                {
                    if (usePathfinding && pathFollower != null)
                    {
                        if (!_lastPathfindTime.TryGetValue(mob.Id, out var lastPf))
                            lastPf = double.MinValue;

                        if (!pathFollower.HasPath || gameTime - lastPf > 1.0)
                        {
                            if (!pathFollower.HasPath && gameTime - lastPf < 0.3)
                            {
                                // Skip recalculation if path just ended recently
                            }
                            else
                            {
                                pathFollower.SetDestination(mob.X, mob.Y, target.X, target.Y);
                                _lastPathfindTime[mob.Id] = gameTime;
                            }
                        }

                        float oldX = mob.X;
                        float oldY = mob.Y;

                        var (newX, newY, dirX, dirY, moving) = pathFollower.MoveToward(mob.X, mob.Y, mob.Speed, dt);

                        if (moving)
                        {
                            if (IsInNoMobZone(newX, newY))
                            {
                                mob.TargetEntityId = null;
                                pathFollower.Stop();
                                mob.Moving = false;
                                continue;
                            }

                            (newX, newY) = ApplyMonsterSeparation(mob, newX, newY, dt, usePathfinding);
                            mob.X = newX;
                            mob.Y = newY;
                            mob.DirX = dirX;
                            mob.DirY = dirY;
                            mob.Moving = true;
                            if (oldX != newX || oldY != newY)
                                _grid.MoveEntity(mob.Id, oldX, oldY, newX, newY);
                        }
                        else if (!pathFollower.HasPath)
                        {
                            float moveDist = mob.Speed * dt;
                            float ratio = Math.Min(moveDist / dist, 1f);
                            float newFx = mob.X + dx * ratio;
                            float newFy = mob.Y + dy * ratio;

                            bool currentWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(mob.X, mob.Y);
                            bool newWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(newFx, newFy);
                            if (!IsInNoMobZone(newFx, newFy) && (newWalkable || !currentWalkable))
                            {
                                (newFx, newFy) = ApplyMonsterSeparation(mob, newFx, newFy, dt, usePathfinding);
                                mob.X = newFx;
                                mob.Y = newFy;
                                mob.DirX = dx / dist;
                                mob.DirY = dy / dist;
                                mob.Moving = true;
                                _grid.MoveEntity(mob.Id, mob.X - dx * ratio, mob.Y - dy * ratio, mob.X, mob.Y);
                            }
                            else
                            {
                                _lastPathfindTime[mob.Id] = double.MinValue;
                                mob.Moving = false;
                            }
                        }
                    }
                    else
                    {
                        float moveDist = mob.Speed * dt;
                        float ratio = Math.Min(moveDist / dist, 1f);
                        float newFx = mob.X + dx * ratio;
                        float newFy = mob.Y + dy * ratio;

                        if (IsInNoMobZone(newFx, newFy))
                        {
                            mob.TargetEntityId = null;
                            mob.Moving = false;
                            continue;
                        }

                        bool currentWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(mob.X, mob.Y);
                        bool newWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(newFx, newFy);
                        if (usePathfinding && !newWalkable && currentWalkable)
                        {
                            mob.TargetEntityId = null;
                            mob.Moving = false;
                            continue;
                        }

                        (newFx, newFy) = ApplyMonsterSeparation(mob, newFx, newFy, dt, usePathfinding);
                        mob.X = newFx;
                        mob.Y = newFy;
                        mob.DirX = dx / dist;
                        mob.DirY = dy / dist;
                        mob.Moving = true;
                        _grid.MoveEntity(mob.Id, mob.X - dx * ratio, mob.Y - dy * ratio, mob.X, mob.Y);
                    }
                }
            }
            else
            {
                // Leash: if too far from spawn, force return
                float spawnDx = mob.X - mob.SpawnX;
                float spawnDy = mob.Y - mob.SpawnY;
                float distFromSpawn = MathF.Sqrt(spawnDx * spawnDx + spawnDy * spawnDy);

                if (distFromSpawn > MonsterEntity.MaxWanderRange)
                {
                    float returnAngle = MathF.Atan2(-spawnDy, -spawnDx);
                    float returnDist = 50f + MathF.Min(distFromSpawn - MonsterEntity.ReturnRange, 300f);
                    mob.PatrolTargetX = mob.X + MathF.Cos(returnAngle) * returnDist;
                    mob.PatrolTargetY = mob.Y + MathF.Sin(returnAngle) * returnDist;
                    mob.PatrolTimer = gameTime + 1.0;
                    pathFollower?.Stop();
                }

                if (mob.PatrolTargetX.HasValue && mob.PatrolTargetY.HasValue)
                {
                    float dx = mob.PatrolTargetX.Value - mob.X;
                    float dy = mob.PatrolTargetY.Value - mob.Y;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    if (dist < 10f)
                    {
                        mob.PatrolTargetX = null;
                        mob.PatrolTargetY = null;
                        mob.PatrolTimer = gameTime + 3.0;
                        mob.Moving = false;
                        pathFollower?.Stop();
                    }
                    else
                    {
                        if (usePathfinding && pathFollower != null)
                        {
                            if (!pathFollower.HasPath)
                            {
                                pathFollower.SetDestination(mob.X, mob.Y, mob.PatrolTargetX.Value, mob.PatrolTargetY.Value);
                            }

                            float oldX = mob.X;
                            float oldY = mob.Y;

                            var (newX, newY, dirX, dirY, moving) = pathFollower.MoveToward(mob.X, mob.Y, mob.Speed * 0.5f, dt);

                            if (moving)
                            {
                            if (IsInNoMobZone(newX, newY))
                            {
                                mob.PatrolTargetX = null;
                                mob.PatrolTargetY = null;
                                mob.PatrolTimer = gameTime + 2.0;
                                pathFollower.Stop();
                                mob.Moving = false;
                                continue;
                            }

                                (newX, newY) = ApplyMonsterSeparation(mob, newX, newY, dt, usePathfinding);
                                mob.X = newX;
                                mob.Y = newY;
                                mob.DirX = dirX;
                                mob.DirY = dirY;
                                mob.Moving = true;
                                _grid.MoveEntity(mob.Id, oldX, oldY, newX, newY);
                            }
                            else if (!pathFollower.HasPath)
                            {
                                float moveDist = mob.Speed * dt * 0.5f;
                                float ratio = Math.Min(moveDist / dist, 1f);
                                float newFx = mob.X + dx * ratio;
                                float newFy = mob.Y + dy * ratio;

                                bool currentWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(mob.X, mob.Y);
                                bool newWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(newFx, newFy);
                                if (!IsInNoMobZone(newFx, newFy) && (newWalkable || !currentWalkable))
                                {
                                    (newFx, newFy) = ApplyMonsterSeparation(mob, newFx, newFy, dt, usePathfinding);
                                    mob.X = newFx;
                                    mob.Y = newFy;
                                    mob.DirX = dx / dist;
                                    mob.DirY = dy / dist;
                                    mob.Moving = true;
                                    _grid.MoveEntity(mob.Id, mob.X - dx * ratio, mob.Y - dy * ratio, mob.X, mob.Y);
                                }
                                else
                                {
                                    mob.PatrolTargetX = null;
                                    mob.PatrolTargetY = null;
                                    mob.PatrolTimer = gameTime + 2.0;
                                    mob.Moving = false;
                                }
                            }
                        }
                        else
                        {
                            float moveDist = mob.Speed * dt * 0.5f;
                            float ratio = Math.Min(moveDist / dist, 1f);
                            float newFx = mob.X + dx * ratio;
                            float newFy = mob.Y + dy * ratio;

                        if (IsInNoMobZone(newFx, newFy))
                        {
                            mob.PatrolTargetX = null;
                            mob.PatrolTargetY = null;
                            mob.PatrolTimer = gameTime + 2.0;
                            mob.Moving = false;
                            continue;
                        }

                        (newFx, newFy) = ApplyMonsterSeparation(mob, newFx, newFy, dt, usePathfinding);
                        mob.X = newFx;
                        mob.Y = newFy;
                        mob.DirX = dx / dist;
                        mob.DirY = dy / dist;
                        mob.Moving = true;
                            _grid.MoveEntity(mob.Id, mob.X - dx * ratio, mob.Y - dy * ratio, mob.X, mob.Y);
                        }
                    }
                }
                else if (gameTime >= mob.PatrolTimer)
                {
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        float angle = Random.Shared.NextSingle() * MathF.PI * 2;
                        float range = 50f + Random.Shared.NextSingle() * mob.PatrolRadius;
                        if (distFromSpawn > MonsterEntity.ReturnRange)
                            range = MathF.Min(range, distFromSpawn * 0.5f);
                        float tx = mob.SpawnX + MathF.Cos(angle) * range;
                        float ty = mob.SpawnY + MathF.Sin(angle) * range;
                        if (!IsInNoMobZone(tx, ty) && (!usePathfinding || PathGrid!.IsWalkableWorld(tx, ty)))
                        {
                            mob.PatrolTargetX = tx;
                            mob.PatrolTargetY = ty;
                            break;
                        }
                    }
                    if (!mob.PatrolTargetX.HasValue)
                        mob.PatrolTimer = gameTime + 2.0;
                    pathFollower?.Stop();
                }
                else
                {
                    mob.Moving = false;
                    pathFollower?.Stop();
                }

                if (!mob.Passive)
                {
                    var nearby = GetEntitiesInAoi(mob.X, mob.Y);
                    ulong? closestPlayer = null;
                    float closestDist = mob.AggroRange;

                    foreach (var eid in nearby)
                    {
                        if (!_entities.TryGetValue(eid, out var e) || e.Type != EntityType.Player) continue;
                        if (e.Health <= 0) continue;

                        float dx = e.X - mob.X;
                        float dy = e.Y - mob.Y;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestPlayer = eid;
                        }
                    }

                    if (closestPlayer.HasValue)
                    {
                        if (_entities.TryGetValue(closestPlayer.Value, out var targetEntity) && IsInNoMobZone(targetEntity.X, targetEntity.Y))
                        {
                            closestPlayer = null;
                        }
                        else
                        {
                            mob.TargetEntityId = closestPlayer;
                            mob.PatrolTargetX = null;
                            mob.PatrolTargetY = null;
                            pathFollower?.Stop();
                        }
                    }
                }
            }
        }
    }

    private void UpdateSpawner(double gameTime)
    {
        if (!_initialSpawned)
        {
            _initialSpawned = true;
            foreach (var point in _spawner.GetSpawnPoints())
            {
                int current = CountMonstersByPrefab(point.PrefabId);
                while (current < point.MaxCount)
                {
                    TrySpawnMonster(point, gameTime);
                    current++;
                }
            }
            return;
        }

        for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
        {
            var (point, respawnAt) = _pendingRespawns[i];
            if (gameTime >= respawnAt)
            {
                TrySpawnMonster(point, gameTime);
                _pendingRespawns.RemoveAt(i);
            }
        }
    }

    private void TrySpawnMonster(SpawnPoint point, double gameTime)
    {
        int current = CountMonstersByPrefab(point.PrefabId);
        if (current >= point.MaxCount) return;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var monster = _spawner.CreateMonster(point);
            if (monster == null) return;

            if (!IsInNoMobZone(monster.X, monster.Y))
            {
                AddEntity(monster);
                return;
            }
        }
    }

    public void ScheduleRespawn(SpawnPoint point, double gameTime)
    {
        _pendingRespawns.Add((point, gameTime + point.RespawnDelay));
    }
}
