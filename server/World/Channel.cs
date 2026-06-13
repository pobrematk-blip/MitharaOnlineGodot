using Mithara.Server.Entities;
using LiteNetLib;
using System.Linq;

namespace Mithara.Server.World;

public class Channel
{
    // Callback for monster attacks: (channel, monster, target, gameTime) -> true if target died
    public Func<Channel, MonsterEntity, Entity, double, bool>? OnMonsterAttack;
    public int Id { get; }
    public string Name { get; }

    private readonly Dictionary<ulong, Entity> _entities = new();
    private readonly SpatialGrid _grid;
    private readonly Dictionary<ulong, NetPeer> _playerPeers = new();
    private readonly SpawnerManager _spawner;
    private readonly List<SpawnPoint> _respawnQueue = new();
    private readonly List<(SpawnPoint point, double respawnAt)> _pendingRespawns = new();
    private readonly Dictionary<string, int> _spawnCounts = new();
    private readonly Dictionary<string, List<ulong>> _spawnedByPrefab = new();
    private readonly List<LootEntity> _lootItems = new();
    private ulong _nextEntityId = 1;
    private bool _initialSpawned = false;

    public const float AoiRadius = 600f;

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

    private void UpdateMonsterAI(float dt, double gameTime)
    {
        foreach (var kv in _entities.ToList())
        {
            if (kv.Value is not MonsterEntity mob) continue;
            if (mob.Health <= 0) continue;

            if (mob.TargetEntityId.HasValue)
            {
                if (!_entities.TryGetValue(mob.TargetEntityId.Value, out var target) || target.Health <= 0)
                {
                    mob.TargetEntityId = null;
                    continue;
                }

                float dx = target.X - mob.X;
                float dy = target.Y - mob.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist <= mob.AttackRange)
                {
                    mob.Moving = false;
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
                    float moveDist = mob.Speed * dt;
                    float ratio = Math.Min(moveDist / dist, 1f);
                    mob.X += dx * ratio;
                    mob.Y += dy * ratio;
                    mob.DirX = dx / dist;
                    mob.DirY = dy / dist;
                    mob.Moving = true;
                    _grid.MoveEntity(mob.Id, mob.X - dx * ratio, mob.Y - dy * ratio, mob.X, mob.Y);
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
                    // Force patrol target toward spawn
                    float returnAngle = MathF.Atan2(-spawnDy, -spawnDx);
                    float returnDist = 50f + MathF.Min(distFromSpawn - MonsterEntity.ReturnRange, 300f);
                    mob.PatrolTargetX = mob.X + MathF.Cos(returnAngle) * returnDist;
                    mob.PatrolTargetY = mob.Y + MathF.Sin(returnAngle) * returnDist;
                    mob.PatrolTimer = gameTime + 1.0;
                }

                // Patrol: runs for ALL mobs (both passive and aggressive)
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
                    }
                    else
                    {
                        float moveDist = mob.Speed * dt * 0.5f;
                        float ratio = Math.Min(moveDist / dist, 1f);
                        mob.X += dx * ratio;
                        mob.Y += dy * ratio;
                        mob.DirX = dx / dist;
                        mob.DirY = dy / dist;
                        mob.Moving = true;
                        _grid.MoveEntity(mob.Id, mob.X - dx * ratio, mob.Y - dy * ratio, mob.X, mob.Y);
                    }
                }
                else if (gameTime >= mob.PatrolTimer)
                {
                    float angle = Random.Shared.NextSingle() * MathF.PI * 2;
                    float range = 50f + Random.Shared.NextSingle() * mob.PatrolRadius;
                    if (distFromSpawn > MonsterEntity.ReturnRange)
                        range = MathF.Min(range, distFromSpawn * 0.5f);
                    mob.PatrolTargetX = mob.SpawnX + MathF.Cos(angle) * range;
                    mob.PatrolTargetY = mob.SpawnY + MathF.Sin(angle) * range;
                }
                else
                {
                    mob.Moving = false;
                }

                // Aggro: only non-passive mobs look for nearby players
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
                        mob.TargetEntityId = closestPlayer;
                        mob.PatrolTargetX = null;
                        mob.PatrolTargetY = null;
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

        var monster = _spawner.CreateMonster(point);
        if (monster == null) return;

        AddEntity(monster);
    }

    public void ScheduleRespawn(SpawnPoint point, double gameTime)
    {
        _pendingRespawns.Add((point, gameTime + point.RespawnDelay));
    }
}
