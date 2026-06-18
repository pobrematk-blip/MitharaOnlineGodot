namespace Mithara.Server.Entities;

public class MonsterEntity : Entity
{
    public bool IsBoss { get; set; }
    public int ExperienceReward { get; set; }
    public string PrefabId { get; set; } = "";
    public int AttackDamage { get; set; }
    public float AttackRange { get; set; } = 40f;
    public float AggroRange { get; set; } = 768f;
    public bool Passive { get; set; } = true;
    public float AttackCooldown { get; set; } = 1.5f;
    public double LastAttackTime { get; set; }
    public ulong? TargetEntityId { get; set; }

    // Patrol
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
    public float PatrolRadius { get; set; } = 960f;
    public float? PatrolTargetX { get; set; }
    public float? PatrolTargetY { get; set; }
    public double PatrolTimer { get; set; }

    public const float MaxWanderRange = 1440f;
    public const float ReturnRange = 1280f;

    public MonsterEntity()
    {
        Type = EntityType.Monster;
    }

    public int CalculateAttackDamage()
    {
        return Math.Max(1, AttackDamage + Forca);
    }

    public int CalculateDefense()
    {
        return Agilidade;
    }
}
