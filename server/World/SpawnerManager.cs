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
    public float AggroRange { get; set; } = 384f;
    public float AttackCooldown { get; set; } = 1.5f;
    public int ExperienceReward { get; set; } = 10;
    public bool IsBoss { get; set; }
    public bool Passive { get; set; } = true;
    public string FactionId { get; set; } = "monster";
    public List<LootEntry> LootTable { get; set; } = new();
    public int GoldMin { get; set; }
    public int GoldMax { get; set; }
    public bool DropsNormalEquipment { get; set; }
    public bool DropsEliteEquipment { get; set; }
    public double EliteDropChance { get; set; } = 1.0;
    public double GoldDropChance { get; set; } = 1.0;
}

public class SpawnPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; } = 100f;
    public string PrefabId { get; set; } = "";
    public int MaxCount { get; set; } = 5;
    public float RespawnDelay { get; set; } = 10f;
    public string ElitePrefabId { get; set; } = "";
    public int EliteBaseCount { get; set; }
    public int EliteEveryKills { get; set; }
    public int KillsSinceElite { get; set; }
}

public class SpawnerManager
{
    private readonly Dictionary<string, MonsterTemplate> _templates = new();
    private readonly List<SpawnPoint> _spawnPoints = new();

    public SpawnerManager()
    {
        RegisterDefaultTemplates();
        RemoveInvalidLootEntries();
        RegisterDefaultSpawnPoints();
    }

    private void RemoveInvalidLootEntries()
    {
        foreach (var template in _templates.Values)
        {
            int removed = template.LootTable.RemoveAll(entry => !ItemDefinitions.Exists(entry.ItemId));
            if (removed > 0)
                Logger.Info($"Drop: {removed} referencia(s) invalida(s) removida(s) de {template.PrefabId}.");
        }
    }

    private void RegisterDefaultTemplates()
    {
        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "slime",
            Name = "Slime",
            Level = 3,
            Health = 60,
            MaxHealth = 60,
            AttackDamage = 7,
            Forca = 1,
            Agilidade = 1,
            Speed = 72f,
            ExperienceReward = 8,
            GoldMin = 3,
            GoldMax = 10,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, DropChance = 0.12 },
                new() { ItemId = ItemDefinitions.PocaoMana, DropChance = 0.10 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "slimeElite",
            Name = "Slime Elite",
            Level = 5,
            Health = 250,
            MaxHealth = 250,
            AttackDamage = 18,
            Forca = 4,
            Agilidade = 2,
            Speed = 84f,
            ExperienceReward = 30,
            Passive = true,
            GoldMin = 15,
            GoldMax = 40,
            DropsNormalEquipment = true,
            EliteDropChance = 0.10,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.25 },
                new() { ItemId = ItemDefinitions.PocaoMana, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.25 },
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
            Speed = 96f,
            ExperienceReward = 5,
            GoldMin = 3,
            GoldMax = 10,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, DropChance = 0.12 },
                new() { ItemId = ItemDefinitions.PocaoMana, DropChance = 0.10 },
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
            Speed = 132f,
            ExperienceReward = 8,
            GoldMin = 5,
            GoldMax = 15,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, DropChance = 0.12 },
                new() { ItemId = ItemDefinitions.PocaoMana, DropChance = 0.10 },
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
            Speed = 84f,
            ExperienceReward = 14,
            GoldMin = 8,
            GoldMax = 25,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, DropChance = 0.12 },
                new() { ItemId = ItemDefinitions.PocaoMana, DropChance = 0.10 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "slimeBoss",
            Name = "Slime Boss",
            Level = 10,
            Health = 3000,
            MaxHealth = 3000,
            AttackDamage = 35,
            Forca = 12,
            Agilidade = 4,
            Speed = 84f,
            AttackRange = 55f,
            AggroRange = 500f,
            AttackCooldown = 2.0f,
            ExperienceReward = 150,
            IsBoss = true,
            Passive = false,
            GoldMin = 100,
            GoldMax = 500,
            GoldDropChance = 0.20,
            DropsEliteEquipment = true,
            EliteDropChance = 0.01,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PoeiraEstelar, MinQuantity = 5, MaxQuantity = 10, DropChance = 0.30 },
                new() { ItemId = ItemDefinitions.PergaminhoDoPet5, MinQuantity = 1, MaxQuantity = 1, DropChance = 0.01 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "cogumelo",
            Name = "Cogumelo",
            Level = 8,
            Health = 250,
            MaxHealth = 250,
            Mana = 10,
            MaxMana = 10,
            AttackDamage = 16,
            Forca = 2,
            Agilidade = 25,
            Speed = 60f,
            AttackRange = 35f,
            AggroRange = 250f,
            ExperienceReward = 35,
            GoldMin = 15,
            GoldMax = 40,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, DropChance = 0.15 },
                new() { ItemId = ItemDefinitions.PocaoMana, DropChance = 0.12 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "cogumeloElite",
            Name = "Cogumelo Elite",
            Level = 11,
            Health = 700,
            MaxHealth = 700,
            Mana = 20,
            MaxMana = 20,
            AttackDamage = 28,
            Forca = 4,
            Agilidade = 40,
            Speed = 60f,
            AttackRange = 40f,
            AggroRange = 300f,
            ExperienceReward = 90,
            GoldMin = 45,
            GoldMax = 120,
            DropsNormalEquipment = true,
            EliteDropChance = 0.08,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.30 },
                new() { ItemId = ItemDefinitions.PocaoMana, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.25 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "plantaCarnivora",
            Name = "Planta Carnivora",
            Level = 10,
            Health = 350,
            MaxHealth = 350,
            Mana = 10,
            MaxMana = 10,
            AttackDamage = 20,
            Forca = 2,
            Agilidade = 30,
            Speed = 72f,
            AttackRange = 40f,
            AggroRange = 300f,
            ExperienceReward = 45,
            GoldMin = 18,
            GoldMax = 50,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, DropChance = 0.15 },
                new() { ItemId = ItemDefinitions.PocaoMana, DropChance = 0.12 },
            },
        });

        RegisterTemplate(new MonsterTemplate
        {
            PrefabId = "plantaCarnivoraElite",
            Name = "Planta Carnivora Elite",
            Level = 13,
            Health = 900,
            MaxHealth = 900,
            Mana = 20,
            MaxMana = 20,
            AttackDamage = 35,
            Forca = 5,
            Agilidade = 50,
            Speed = 72f,
            AttackRange = 45f,
            AggroRange = 350f,
            ExperienceReward = 110,
            GoldMin = 60,
            GoldMax = 150,
            DropsNormalEquipment = true,
            EliteDropChance = 0.08,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.30 },
                new() { ItemId = ItemDefinitions.PocaoMana, MinQuantity = 1, MaxQuantity = 2, DropChance = 0.25 },
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
            Speed = 108f,
            AttackRange = 60f,
            AggroRange = 500f,
            ExperienceReward = 80,
            IsBoss = true,
            Passive = false,
            GoldMin = 50,
            GoldMax = 200,
            DropsEliteEquipment = true,
            LootTable = new List<LootEntry>
            {
                new() { ItemId = ItemDefinitions.PocaoVida, MinQuantity = 2, MaxQuantity = 4, DropChance = 0.75 },
                new() { ItemId = ItemDefinitions.PocaoMana, MinQuantity = 2, MaxQuantity = 4, DropChance = 0.75 },
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

    public void UpdateDropTable(string prefabId, List<LootEntry> drops)
    {
        if (_templates.TryGetValue(prefabId, out var template))
            template.LootTable = drops;
    }

    private void RegisterDefaultSpawnPoints()
    {
        _spawnPoints.Add(new SpawnPoint
        {
            X = 1200,
            Y = 1050,
            Radius = 600f,
            PrefabId = "slime",
            MaxCount = 15,
            RespawnDelay = 8f,
            ElitePrefabId = "slimeElite",
            EliteBaseCount = 0,
            EliteEveryKills = 8,
        });

        _spawnPoints.Add(new SpawnPoint
        {
            X = 1100,
            Y = 1050,
            Radius = 200f,
            PrefabId = "slimeBoss",
            MaxCount = 1,
            RespawnDelay = 3600f,
        });
    }

    public List<SpawnPoint> GetSpawnPoints() => _spawnPoints;

    public void ConfigureSpawnPoints(IEnumerable<SpawnPoint> spawnPoints)
    {
        _spawnPoints.Clear();
        foreach (var point in spawnPoints)
        {
            if (string.IsNullOrWhiteSpace(point.PrefabId) || point.MaxCount <= 0)
                continue;

            _spawnPoints.Add(new SpawnPoint
            {
                X = point.X,
                Y = point.Y,
                Radius = point.Radius,
                PrefabId = point.PrefabId,
                MaxCount = point.MaxCount,
                RespawnDelay = Math.Max(8f, point.RespawnDelay),
                ElitePrefabId = point.ElitePrefabId,
                EliteBaseCount = 0,
                EliteEveryKills = string.IsNullOrWhiteSpace(point.ElitePrefabId) ? 0 : 8,
            });
        }
    }

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
            AttackRange = Math.Max(70f, template.AttackRange),
            AggroRange = template.AggroRange,
            AttackCooldown = template.AttackCooldown,
            ExperienceReward = template.ExperienceReward,
            IsBoss = template.IsBoss,
            Passive = template.Passive,
            FactionId = template.FactionId,
            X = x,
            Y = y,
            SpawnX = x,
            SpawnY = y,
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
            AttackRange = Math.Max(70f, template.AttackRange),
            AggroRange = template.AggroRange,
            AttackCooldown = template.AttackCooldown,
            ExperienceReward = template.ExperienceReward,
            IsBoss = template.IsBoss,
            Passive = template.Passive,
            FactionId = template.FactionId,
            X = spawnX,
            Y = spawnY,
            SpawnX = spawnX,
            SpawnY = spawnY,
        };
    }
}
