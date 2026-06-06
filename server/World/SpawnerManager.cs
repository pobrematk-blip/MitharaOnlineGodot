using Mithara.Server.Entities;

namespace Mithara.Server.World;

public class LootEntry
{
    public int ItemId { get; set; }
    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
    public double DropChance { get; set; } = 1.0;
}

public class MonsterTemplate
{
    public string PrefabId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Health { get; set; } = 50;
    public int MaxHealth { get; set; } = 50;
    public int Mana { get; set; } = 10;
    public int MaxMana { get; set; } = 10;
    public int AttackDamage { get; set; } = 5;
    public int Forca { get; set; } = 1;
    public int Agilidade { get; set; } = 1;
    public int Destreza { get; set; } = 1;
    public int Inteligencia { get; set; } = 0;
    public float Speed { get; set; } = 100f;
    public float AttackRange { get; set; } = 40f;
    public float AggroRange { get; set; } = 300f;
    public float AttackCooldown { get; set; } = 1.5f;
    public int ExperienceReward { get; set; } = 10;
    public bool IsBoss { get; set; }
    public string FactionId { get; set; } = "monster";
    public List<LootEntry> LootTable { get; set; } = new();
    public int GoldMin { get; set; }
    public int GoldMax { get; set; }
}

public class SpawnPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; } = 100f;
    public string PrefabId { get; set; } = "";
    public int MaxCount { get; set; } = 5;
    public float RespawnDelay { get; set; } = 10f;
}

public class SpawnerManager
{
    private readonly Dictionary<string, MonsterTemplate> _templates = new();
    private readonly List<SpawnPoint> _spawnPoints = new();

    public SpawnerManager()
    {
        RegisterDefaultTemplates();
        RegisterDefaultSpawnPoints();
    }

    private void RegisterDefaultTemplates()
    {
        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "slime",
            Name = "Slime",
            Level = 1,
            Health = 40,
            MaxHealth = 40,
            AttackDamage = 4,
            Forca = 1,
            Agilidade = 1,
            Speed = 60f,
            ExperienceReward = 8,
            GoldMin = 1,
            GoldMax = 5,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = 1, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.5 },
                new() { ItemId = 2, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.3 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "goblin",
            Name = "Goblin",
            Level = 2,
            Health = 60,
            MaxHealth = 60,
            AttackDamage = 7,
            Forca = 2,
            Agilidade = 2,
            Speed = 80f,
            ExperienceReward = 15,
            GoldMin = 3,
            GoldMax = 10,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = 1, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.4 },
                new() { ItemId = 10, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.15 },
                new() { ItemId = 20, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.08 },
                new() { ItemId = 21, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.08 },
                new() { ItemId = 100, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.05 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "wolf",
            Name = "Lobo",
            Level = 3,
            Health = 80,
            MaxHealth = 80,
            AttackDamage = 10,
            Forca = 3,
            Agilidade = 3,
            Speed = 110f,
            ExperienceReward = 25,
            GoldMin = 5,
            GoldMax = 15,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = 2, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.5 },
                new() { ItemId = 13, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.12 },
                new() { ItemId = 21, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.1 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "skeleton",
            Name = "Esqueleto",
            Level = 5,
            Health = 120,
            MaxHealth = 120,
            AttackDamage = 15,
            Forca = 5,
            Agilidade = 2,
            Speed = 70f,
            ExperienceReward = 45,
            GoldMin = 8,
            GoldMax = 25,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = 1, MinQuantity = 2, MaxQuantity = 3, DropChance = 0.6 },
                new() { ItemId = 11, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.2 },
                new() { ItemId = 22, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.15 },
                new() { ItemId = 24, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.12 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "boss_demon",
            Name = "Demon Lord",
            Level = 10,
            Health = 2000,
            MaxHealth = 2000,
            AttackDamage = 30,
            Forca = 15,
            Agilidade = 5,
            Speed = 90f,
            AttackRange = 60f,
            AggroRange = 500f,
            ExperienceReward = 500,
            IsBoss = true,
            GoldMin = 50,
            GoldMax = 200,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = 1, MinQuantity = 5, MaxQuantity = 10, DropChance = 1.0 },
                new() { ItemId = 2, MinQuantity = 3, MaxQuantity = 8, DropChance = 1.0 },
                new() { ItemId = 11, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.8 },
                new() { ItemId = 21, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.7 },
                new() { ItemId = 23, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.6 },
                new() { ItemId = 30, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.3 },
                new() { ItemId = 31, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.25 },
            },
        });
    }

    public void RegisterTemplate(MonsterTemplate template)
    {
        _templates[template.PrefabId] = template;
    }

    public MonsterTemplate? GetTemplate(string prefabId)
    {
        _templates.TryGetValue(prefabId, out var template);
        return template;
    }

    private void RegisterDefaultSpawnPoints()
    {
        _spawnPoints.Add(new SpawnPoint { X = 1200, Y = 1100, Radius = 150f, PrefabId = "slime", MaxCount = 5 });
        _spawnPoints.Add(new SpawnPoint { X = 1400, Y = 1300, Radius = 150f, PrefabId = "slime", MaxCount = 4 });
        _spawnPoints.Add(new SpawnPoint { X = 800, Y = 900, Radius = 120f, PrefabId = "goblin", MaxCount = 4 });
        _spawnPoints.Add(new SpawnPoint { X = 1600, Y = 800, Radius = 200f, PrefabId = "goblin", MaxCount = 5 });
        _spawnPoints.Add(new SpawnPoint { X = 1800, Y = 1200, Radius = 180f, PrefabId = "wolf", MaxCount = 3 });
        _spawnPoints.Add(new SpawnPoint { X = 600, Y = 1400, Radius = 150f, PrefabId = "skeleton", MaxCount = 3 });
        _spawnPoints.Add(new SpawnPoint { X = 2000, Y = 600, Radius = 100f, PrefabId = "boss_demon", MaxCount = 1, RespawnDelay = 60f });
    }

    public List<SpawnPoint> GetSpawnPoints() => _spawnPoints;

    public MonsterEntity? CriarMonstroEm(MonsterTemplate template, float x, float y)
    {
        return new MonsterEntity
        {
            Name = template.Name,
            PrefabId = template.PrefabId,
            Level = template.Level,
            Health = template.Health,
            MaxHealth = template.MaxHealth,
            Mana = template.Mana,
            MaxMana = template.MaxMana,
            AttackDamage = template.AttackDamage,
            Forca = template.Forca,
            Agilidade = template.Agilidade,
            Destreza = template.Destreza,
            Inteligencia = template.Inteligencia,
            Speed = template.Speed,
            AttackRange = template.AttackRange,
            AggroRange = template.AggroRange,
            AttackCooldown = template.AttackCooldown,
            ExperienceReward = template.ExperienceReward,
            IsBoss = template.IsBoss,
            FactionId = template.FactionId,
            X = x,
            Y = y,
        };
    }

    public MonsterEntity? CreateMonster(SpawnPoint point)
    {
        var template = GetTemplate(point.PrefabId);
        if (template == null) return null;

        var rng = Random.Shared;
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float dist = (float)(rng.NextDouble() * point.Radius);
        float spawnX = point.X + (float)Math.Cos(angle) * dist;
        float spawnY = point.Y + (float)Math.Sin(angle) * dist;

        return new MonsterEntity
        {
            Name = template.Name,
            PrefabId = template.PrefabId,
            Level = template.Level,
            Health = template.Health,
            MaxHealth = template.MaxHealth,
            Mana = template.Mana,
            MaxMana = template.MaxMana,
            AttackDamage = template.AttackDamage,
            Forca = template.Forca,
            Agilidade = template.Agilidade,
            Destreza = template.Destreza,
            Inteligencia = template.Inteligencia,
            Speed = template.Speed,
            AttackRange = template.AttackRange,
            AggroRange = template.AggroRange,
            AttackCooldown = template.AttackCooldown,
            ExperienceReward = template.ExperienceReward,
            IsBoss = template.IsBoss,
            FactionId = template.FactionId,
            X = spawnX,
            Y = spawnY,
        };
    }
}
