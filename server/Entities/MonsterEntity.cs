namespace Mithara.Server.Entities;

public class MonsterEntity : Entity
{
    public bool IsBoss { get; set; }
    public int ExperienceReward { get; set; }
    public string PrefabId { get; set; } = "";
    public int AttackDamage { get; set; }
    public float AttackRange { get; set; } = 40f;
    public float AggroRange { get; set; } = 300f;
    public float AttackCooldown { get; set; } = 1.5f;
    public double LastAttackTime { get; set; }
    public ulong? TargetEntityId { get; set; }

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
