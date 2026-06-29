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
    private readonly List<(SpawnPoint point, string prefabId, double respawnAt)> _pendingRespawns = new();
    private readonly Dictionary<string, int> _spawnCounts = new();
    private readonly Dictionary<string, List<ulong>> _spawnedByPrefab = new();
    private readonly List<LootEntity> _lootItems = new();
    private readonly List<ulong> _expiredLoot = new();
    private readonly Dictionary<ulong, LojinhaEntity> _lojinhas = new();
    private readonly Dictionary<ulong, PathFollower> _pathFollowers = new();
    private readonly Dictionary<ulong, double> _lastPathfindTime = new();
    private ulong _nextEntityId = 1;
    private bool _initialSpawned = false;

    public const float AoiRadius = 1200f;
    private const float MonsterSeparationRadius = 52f;
    private const float MonsterSeparationStrength = 0.65f;
    private const float SpawnMinDistance = 180f;

    public SpawnerManager Spawner => _spawner;
    public event Action<Entity>? EntitySpawned;

    public Channel(int id, string name)
    {
        Id = id;
        Name = name;
        _grid = new SpatialGrid();
        _spawner = new SpawnerManager();
    }

    public int SpawnNpcs(NpcManager npcManager)
    {
        int spawned = 0;
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
                spawned++;
            }
        }

        return spawned;
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

    private void SetMonsterDirection(MonsterEntity mob, float dx, float dy)
    {
        float lenSq = dx * dx + dy * dy;
        if (lenSq < 0.0025f)
            return;

        float absX = MathF.Abs(dx);
        float absY = MathF.Abs(dy);
        bool wasHorizontal = MathF.Abs(mob.DirX) > 0.5f;
        bool wasVertical = MathF.Abs(mob.DirY) > 0.5f;

        // Histerese de 25%: preserva o eixo atual perto das diagonais.
        // A direcao enviada pela rede fica cardinal e nao oscila entre dois sprites.
        bool useHorizontal;
        if (wasHorizontal)
            useHorizontal = absY <= absX * 1.25f;
        else if (wasVertical)
            useHorizontal = absX > absY * 1.25f;
        else
            useHorizontal = absX >= absY;

        if (useHorizontal && absX > 0.001f)
        {
            mob.DirX = MathF.Sign(dx);
            mob.DirY = 0f;
        }
        else if (absY > 0.001f)
        {
            mob.DirX = 0f;
            mob.DirY = MathF.Sign(dy);
        }
    }

    public HashSet<ulong> GetEntitiesInAoi(float x, float y)
    {
        return _grid.GetEntitiesInRadius(x, y, AoiRadius);
    }

    public Dictionary<ulong, Entity> GetAllEntities() => _entities;

    public IEnumerable<KeyValuePair<ulong, NetPeer>> GetAllPlayerPeers()
    {
        foreach (var kv in _playerPeers)
            yield return kv;
    }

    public record struct MapMarkerData
    {
        public int MarkerId;
        public float WorldX;
        public float WorldY;
        public string PlayerName;
        public int ChannelId;
    }

    private readonly List<MapMarkerData> _markers = new();

    public void AddMarker(MapMarkerData marker)
    {
        _markers.Add(marker);
    }

    public void RemoveMarker(int markerId)
    {
        _markers.RemoveAll(m => m.MarkerId == markerId);
    }

    public List<MapMarkerData> GetAllMarkers()
    {
        return new List<MapMarkerData>(_markers);
    }

    public int CountMonstersByPrefab(string prefabId)
    {
        return _spawnedByPrefab.TryGetValue(prefabId, out var list) ? list.Count : 0;
    }

    private int CountMonstersForSpawnPoint(SpawnPoint point, string prefabId)
    {
        if (!_spawnedByPrefab.TryGetValue(prefabId, out var list))
            return 0;

        int count = 0;
        float radiusSq = point.Radius * point.Radius;
        foreach (ulong entityId in list)
        {
            if (!_entities.TryGetValue(entityId, out var entity) || entity is not MonsterEntity mob)
                continue;

            float dx = mob.SpawnX - point.X;
            float dy = mob.SpawnY - point.Y;
            if ((dx * dx) + (dy * dy) <= radiusSq)
                count++;
        }

        return count;
    }

    private int CountAllMonstersForSpawnPoint(SpawnPoint point)
    {
        int total = CountMonstersForSpawnPoint(point, point.PrefabId);
        if (!string.IsNullOrWhiteSpace(point.ElitePrefabId)
            && !string.Equals(point.ElitePrefabId, point.PrefabId, StringComparison.OrdinalIgnoreCase))
        {
            total += CountMonstersForSpawnPoint(point, point.ElitePrefabId);
        }
        return total;
    }

    private SpawnPoint? FindSpawnPointForMonster(MonsterEntity mob)
    {
        return _spawner.GetSpawnPoints()
            .Where(sp => sp.PrefabId == mob.PrefabId || sp.ElitePrefabId == mob.PrefabId)
            .OrderBy(sp =>
            {
                float dx = mob.SpawnX - sp.X;
                float dy = mob.SpawnY - sp.Y;
                return (dx * dx) + (dy * dy);
            })
            .FirstOrDefault();
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

    public void AddLojinha(LojinhaEntity lojinha)
    {
        _lojinhas[lojinha.Id] = lojinha;
    }

    public void RemoveLojinha(ulong lojinhaId)
    {
        _lojinhas.Remove(lojinhaId);
    }

    public Dictionary<ulong, LojinhaEntity> Lojinhas => _lojinhas;

    public List<LootEntity> GetLootInRadius(float x, float y, float radius)
    {
        return _lootItems.Where(l => !l.PickedUp && MathF.Sqrt(MathF.Pow(l.X - x, 2) + MathF.Pow(l.Y - y, 2)) <= radius).ToList();
    }

    public List<LootEntity> GetAllLoot()
    {
        return _lootItems.Where(l => !l.PickedUp).ToList();
    }

    public void Update(float dt, double gameTime)
    {
        UpdateMonsterAI(dt, gameTime);
        UpdateSpawner(gameTime);
        UpdateLootCleanup(gameTime);
        UpdatePlayerRegen(dt, gameTime);
    }

    private void UpdatePlayerRegen(float dt, double gameTime)
    {
        const double outOfCombatDelay = 30.0;
        const float healthRegenPercentPerSecond = 0.0015f;
        const float manaRegenPercentPerSecond = 0.004f;

        foreach (var kv in _entities)
        {
            if (kv.Value is PlayerEntity player)
            {
                if (gameTime - player.LastCombatTime >= outOfCombatDelay)
                {
                    if (player.Health < player.MaxHealth)
                    {
                        player.HealthRegenAccumulator += player.MaxHealth * healthRegenPercentPerSecond * dt;
                        int hpRegen = (int)player.HealthRegenAccumulator;
                        if (hpRegen > 0)
                        {
                            player.Health = Math.Min(player.MaxHealth, player.Health + hpRegen);
                            player.HealthRegenAccumulator -= hpRegen;
                        }
                    }
                    else
                    {
                        player.HealthRegenAccumulator = 0;
                    }

                    if (player.Mana < player.MaxMana)
                    {
                        player.ManaRegenAccumulator += player.MaxMana * manaRegenPercentPerSecond * dt;
                        int manaRegen = (int)player.ManaRegenAccumulator;
                        if (manaRegen > 0)
                        {
                            player.Mana = Math.Min(player.MaxMana, player.Mana + manaRegen);
                            player.ManaRegenAccumulator -= manaRegen;
                        }
                    }
                    else
                    {
                        player.ManaRegenAccumulator = 0;
                    }
                }
                else
                {
                    player.HealthRegenAccumulator = 0;
                    player.ManaRegenAccumulator = 0;
                }
            }
        }
    }

    private void UpdateLootCleanup(double gameTime)
    {
        const double lootDuration = 60.0;
        for (int i = _lootItems.Count - 1; i >= 0; i--)
        {
            if (gameTime - _lootItems[i].SpawnTime >= lootDuration)
            {
                _expiredLoot.Add(_lootItems[i].Id);
                _lootItems.RemoveAt(i);
            }
        }
    }

    public List<ulong> DrainExpiredLoot()
    {
        var result = new List<ulong>(_expiredLoot);
        _expiredLoot.Clear();
        return result;
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

        float desiredMoveX = desiredX - mob.X;
        float desiredMoveY = desiredY - mob.Y;
        float desiredMove = MathF.Sqrt(desiredMoveX * desiredMoveX + desiredMoveY * desiredMoveY);
        float maxPush = desiredMove * MonsterSeparationStrength;
        float adjustedX = desiredX + pushX / pushLen * maxPush;
        float adjustedY = desiredY + pushY / pushLen * maxPush;

        if (IsInNoMobZone(adjustedX, adjustedY))
            return (desiredX, desiredY);

        if (usePathfinding && PathGrid != null && !PathGrid.IsWalkableWorld(adjustedX, adjustedY))
            return (desiredX, desiredY);

        return (adjustedX, adjustedY);
    }

    private void ApplyMonsterMovement(
        MonsterEntity mob,
        float oldX,
        float oldY,
        float newX,
        float newY,
        float intendedDirX,
        float intendedDirY)
    {
        float moveX = newX - oldX;
        float moveY = newY - oldY;
        float movedSq = moveX * moveX + moveY * moveY;

        if (movedSq < 0.04f)
        {
            mob.Moving = false;
            return;
        }

        mob.X = newX;
        mob.Y = newY;
        // A separacao evita sobreposicao, mas nao deve decidir para onde o sprite olha.
        // Usa a direcao do caminho/perseguicao e recorre ao deslocamento apenas como fallback.
        if (intendedDirX * intendedDirX + intendedDirY * intendedDirY > 0.0025f)
            SetMonsterDirection(mob, intendedDirX, intendedDirY);
        else
            SetMonsterDirection(mob, moveX, moveY);
        mob.Moving = true;
        _grid.MoveEntity(mob.Id, oldX, oldY, newX, newY);
    }

    private static void PruneExpiredMonsterBuffs(MonsterEntity mob, double gameTime)
    {
        if (mob.ActiveServerBuffs.Count == 0)
            return;

        foreach (var key in mob.ActiveServerBuffs
            .Where(kv => kv.Value <= gameTime)
            .Select(kv => kv.Key)
            .ToList())
        {
            mob.ActiveServerBuffs.Remove(key);
        }
    }

    private static float GetEffectiveMobSpeed(MonsterEntity mob, double gameTime)
    {
        if (HasMovementBlock(mob, gameTime))
            return 0f;
        if (HasActiveBuff(mob, "slow", gameTime))
            return mob.Speed * 0.5f;
        return mob.Speed;
    }

    private static bool HasHardDisable(MonsterEntity mob, double gameTime)
    {
        return HasActiveBuff(mob, "stun", gameTime)
            || HasActiveBuff(mob, "freeze", gameTime)
            || HasActiveBuff(mob, "sleep", gameTime)
            || HasActiveBuff(mob, "prison", gameTime);
    }

    private static bool HasMovementBlock(MonsterEntity mob, double gameTime)
    {
        return HasHardDisable(mob, gameTime)
            || HasActiveBuff(mob, "root", gameTime);
    }

    private static bool HasActiveBuff(MonsterEntity mob, string key, double gameTime)
    {
        return mob.ActiveServerBuffs.TryGetValue(key, out double until) && until > gameTime;
    }

    private void UpdateMonsterAI(float dt, double gameTime)
    {
        foreach (var kv in _entities.ToList())
        {
            if (kv.Value is not MonsterEntity mob) continue;
            if (mob.Health <= 0)
            {
                mob.AIState = MonsterAIState.Dead;
                continue;
            }

            mob.AIState = MonsterAIState.Idle;
            bool usePathfinding = PathGrid != null;
            var pathFollower = usePathfinding ? GetOrCreatePathFollower(mob.Id) : null;
            PruneExpiredMonsterBuffs(mob, gameTime);
            float mobSpeed = GetEffectiveMobSpeed(mob, gameTime);

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

                float effectiveAttackRange = mob.AttackRange + (mob.Moving ? 0f : 8f);
                if (dist <= effectiveAttackRange)
                {
                    mob.AIState = MonsterAIState.Attack;
                    mob.Moving = false;
                    pathFollower?.Stop();
                    SetMonsterDirection(mob, dx, dy);
                    if (!HasHardDisable(mob, gameTime) && gameTime - mob.LastAttackTime >= mob.AttackCooldown)
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

                        var (newX, newY, dirX, dirY, moving) = pathFollower.MoveToward(mob.X, mob.Y, mobSpeed, dt);

                        if (moving)
                        {
                            mob.AIState = MonsterAIState.Chase;
                            if (IsInNoMobZone(newX, newY))
                            {
                                mob.TargetEntityId = null;
                                pathFollower.Stop();
                                mob.Moving = false;
                                continue;
                            }

                            (newX, newY) = ApplyMonsterSeparation(mob, newX, newY, dt, usePathfinding);
                            ApplyMonsterMovement(mob, oldX, oldY, newX, newY, dirX, dirY);
                        }
                        else if (!pathFollower.HasPath)
                        {
                            mob.AIState = MonsterAIState.Chase;
                            float fallbackOldX = mob.X;
                            float fallbackOldY = mob.Y;
                            float moveDist = mobSpeed * dt;
                            float ratio = Math.Min(moveDist / dist, 1f);
                            float newFx = mob.X + dx * ratio;
                            float newFy = mob.Y + dy * ratio;

                            bool currentWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(mob.X, mob.Y);
                            bool newWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(newFx, newFy);
                            if (!IsInNoMobZone(newFx, newFy) && (newWalkable || !currentWalkable))
                            {
                                (newFx, newFy) = ApplyMonsterSeparation(mob, newFx, newFy, dt, usePathfinding);
                                ApplyMonsterMovement(mob, fallbackOldX, fallbackOldY, newFx, newFy, dx, dy);
                            }
                            else
                            {
                                _lastPathfindTime[mob.Id] = 0;
                                mob.Moving = false;
                            }
                        }
                    }
                    else
                    {
                        float oldX = mob.X;
                        float oldY = mob.Y;
                        mob.AIState = MonsterAIState.Chase;
                        float moveDist = mobSpeed * dt;
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
                        ApplyMonsterMovement(mob, oldX, oldY, newFx, newFy, dx, dy);
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
                    mob.AIState = MonsterAIState.Return;
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
                        mob.AIState = MonsterAIState.Idle;
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
                                if (!_lastPathfindTime.TryGetValue(mob.Id, out var lastPfPatrol))
                                    lastPfPatrol = double.MinValue;
                                if (gameTime - lastPfPatrol >= 0.3)
                                {
                                    pathFollower.SetDestination(mob.X, mob.Y, mob.PatrolTargetX.Value, mob.PatrolTargetY.Value);
                                    _lastPathfindTime[mob.Id] = gameTime;
                                }
                            }

                            float oldX = mob.X;
                            float oldY = mob.Y;

                            var (newX, newY, dirX, dirY, moving) = pathFollower.MoveToward(mob.X, mob.Y, mobSpeed * 0.5f, dt);

                            if (moving)
                            {
                                mob.AIState = MonsterAIState.Patrol;
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
                                ApplyMonsterMovement(mob, oldX, oldY, newX, newY, dirX, dirY);
                            }
                            else if (!pathFollower.HasPath)
                            {
                                mob.AIState = MonsterAIState.Patrol;
                                float fallbackOldX = mob.X;
                                float fallbackOldY = mob.Y;
                                float moveDist = mobSpeed * dt * 0.5f;
                                float ratio = Math.Min(moveDist / dist, 1f);
                                float newFx = mob.X + dx * ratio;
                                float newFy = mob.Y + dy * ratio;

                                bool currentWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(mob.X, mob.Y);
                                bool newWalkable = !usePathfinding || PathGrid!.IsWalkableWorld(newFx, newFy);
                                if (!IsInNoMobZone(newFx, newFy) && (newWalkable || !currentWalkable))
                                {
                                    (newFx, newFy) = ApplyMonsterSeparation(mob, newFx, newFy, dt, usePathfinding);
                                    ApplyMonsterMovement(mob, fallbackOldX, fallbackOldY, newFx, newFy, dx, dy);
                                }
                                else
                                {
                                    mob.AIState = MonsterAIState.Idle;
                                    mob.PatrolTargetX = null;
                                    mob.PatrolTargetY = null;
                                    mob.PatrolTimer = gameTime + 2.0;
                                    mob.Moving = false;
                                }
                            }
                        }
                        else
                        {
                            float oldX = mob.X;
                            float oldY = mob.Y;
                            mob.AIState = MonsterAIState.Patrol;
                            float moveDist = mobSpeed * dt * 0.5f;
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
                            ApplyMonsterMovement(mob, oldX, oldY, newFx, newFy, dx, dy);
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
                            mob.AIState = MonsterAIState.Patrol;
                            mob.PatrolTargetX = tx;
                            mob.PatrolTargetY = ty;
                            break;
                        }
                    }
                    if (!mob.PatrolTargetX.HasValue)
                    {
                        mob.AIState = MonsterAIState.Idle;
                        mob.PatrolTimer = gameTime + 2.0;
                    }
                    pathFollower?.Stop();
                }
                else
                {
                    mob.AIState = MonsterAIState.Idle;
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
                            mob.AIState = MonsterAIState.Chase;
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
            int totalSpawned = 0;
            foreach (var point in _spawner.GetSpawnPoints())
            {
                int eliteTarget = Math.Clamp(point.EliteBaseCount, 0, point.MaxCount);
                int normalTarget = point.MaxCount - eliteTarget;
                int currentElites = string.IsNullOrWhiteSpace(point.ElitePrefabId)
                    ? 0
                    : CountMonstersForSpawnPoint(point, point.ElitePrefabId);
                while (currentElites < eliteTarget)
                {
                    if (TrySpawnMonster(point, point.ElitePrefabId, true))
                    {
                        currentElites++;
                        totalSpawned++;
                    }
                    else break;
                }

                int currentNormals = CountMonstersForSpawnPoint(point, point.PrefabId);
                while (currentNormals < normalTarget)
                {
                    if (TrySpawnMonster(point, point.PrefabId, true))
                    {
                        currentNormals++;
                        totalSpawned++;
                    }
                    else break;
                }
            }
            Logger.Info($"Canal {Id}: spawn inicial criou {totalSpawned} mob(s) em {_spawner.GetSpawnPoints().Count} spot(s).");
            return;
        }

        for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
        {
            var (point, prefabId, respawnAt) = _pendingRespawns[i];
            if (gameTime >= respawnAt)
            {
                TrySpawnMonster(point, prefabId, true);
                _pendingRespawns.RemoveAt(i);
            }
        }

        foreach (var point in _spawner.GetSpawnPoints())
        {
            if (string.IsNullOrWhiteSpace(point.ElitePrefabId))
                continue;

            int eliteTarget = Math.Clamp(point.EliteBaseCount, 0, point.MaxCount);
            int currentElites = CountMonstersForSpawnPoint(point, point.ElitePrefabId);
            while (currentElites < eliteTarget)
            {
                if (!TrySpawnMonster(point, point.ElitePrefabId, true))
                    break;
                currentElites++;
            }
        }
    }

    private void TrySpawnMonster(SpawnPoint point, double gameTime)
    {
        int normalTarget = point.MaxCount - Math.Clamp(point.EliteBaseCount, 0, point.MaxCount);
        int current = CountMonstersForSpawnPoint(point, point.PrefabId);
        if (current >= normalTarget) return;

        TrySpawnMonster(point, point.PrefabId, false);
    }

    private bool TrySpawnMonster(SpawnPoint point, string prefabId, bool ignoreMaxCount)
    {
        // MaxCount inclui normais e elites. Nenhum respawn ou elite bônus pode
        // elevar o total do spot acima desse limite.
        if (CountAllMonstersForSpawnPoint(point) >= point.MaxCount)
            return false;

        if (!ignoreMaxCount)
        {
            int normalTarget = point.MaxCount - Math.Clamp(point.EliteBaseCount, 0, point.MaxCount);
            int current = CountMonstersForSpawnPoint(point, point.PrefabId);
            if (current >= normalTarget) return false;
        }

        var template = _spawner.GetTemplate(prefabId);
        if (template == null) return false;

        for (int attempt = 0; attempt < 32; attempt++)
        {
            var (spawnX, spawnY) = GetRandomSpawnPosition(point);
            var monster = _spawner.CriarMonstroEm(template, spawnX, spawnY);
            if (monster == null)
                return false;

            if (IsValidMonsterSpawn(point, monster.X, monster.Y))
            {
                AddEntity(monster);
                return true;
            }
        }

        return false;
    }

    private bool IsValidMonsterSpawn(SpawnPoint point, float x, float y)
    {
        if (IsInNoMobZone(x, y))
            return false;

        if (PathGrid != null && !PathGrid.IsWalkableWorld(x, y))
            return false;

        return !IsSpawnTooClose(point, x, y);
    }

    public void ScheduleRespawn(SpawnPoint point, string prefabId, double gameTime)
    {
        _pendingRespawns.Add((point, prefabId, gameTime + point.RespawnDelay));
    }

    public void HandleMonsterKilled(MonsterEntity mob, double gameTime)
    {
        var spawnPoint = FindSpawnPointForMonster(mob);
        if (spawnPoint == null)
            return;

        if (mob.PrefabId != spawnPoint.PrefabId)
            return;

        ScheduleRespawn(spawnPoint, mob.PrefabId, gameTime);

        if (
            string.IsNullOrWhiteSpace(spawnPoint.ElitePrefabId) ||
            spawnPoint.EliteEveryKills <= 0)
            return;

        spawnPoint.KillsSinceElite++;
        if (spawnPoint.KillsSinceElite < spawnPoint.EliteEveryKills)
            return;

        if (TrySpawnMonster(spawnPoint, spawnPoint.ElitePrefabId, true))
        {
            spawnPoint.KillsSinceElite = 0;
            spawnPoint.EliteEveryKills = SpawnerManager.RollEliteKillTarget();
            Logger.Info($"Elite: proximo {spawnPoint.ElitePrefabId} em {spawnPoint.EliteEveryKills} morte(s) de {spawnPoint.PrefabId}.");
        }
    }

    private static (float x, float y) GetRandomSpawnPosition(SpawnPoint point)
    {
        var rng = Random.Shared;
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float dist = MathF.Sqrt((float)rng.NextDouble()) * point.Radius;
        return (point.X + MathF.Cos(angle) * dist, point.Y + MathF.Sin(angle) * dist);
    }

    private bool IsSpawnTooClose(SpawnPoint point, float x, float y)
    {
        float minDistanceSq = SpawnMinDistance * SpawnMinDistance;
        float pointRadiusSq = point.Radius * point.Radius;

        foreach (var entity in _entities.Values)
        {
            if (entity is not MonsterEntity mob)
                continue;

            float spawnDx = mob.SpawnX - point.X;
            float spawnDy = mob.SpawnY - point.Y;
            if ((spawnDx * spawnDx) + (spawnDy * spawnDy) > pointRadiusSq)
                continue;

            float dx = mob.X - x;
            float dy = mob.Y - y;
            if ((dx * dx) + (dy * dy) < minDistanceSq)
                return true;
        }

        return false;
    }
}
