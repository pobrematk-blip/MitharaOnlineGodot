using System.Collections.Generic;
using System.Linq;
using Mithara.Server.Quests;

namespace Mithara.Server.Entities;

public class PlayerEntity : Entity
{
    public int AccountId { get; set; }
    public int SlotIndex { get; set; }
    public string CharacterClass { get; set; } = "";
    public string Race { get; set; } = "";
    public string CabeloPath { get; set; } = "";
    public string BarbaPath { get; set; } = "";
    public string CabeloCor { get; set; } = "ffffff";
    public string BarbaCor { get; set; } = "ffffff";
    public int PartyId { get; set; } = -1;
    public int GuildId { get; set; } = -1;
    public string GuildName { get; set; } = "";

    public int BaseAttack { get; set; }
    public int Defense { get; set; }
    public int MagicDefense { get; set; }
    public float EquipmentEvasion { get; set; }
    public float CritChanceBonus { get; set; }
    public float CritDamageBonus { get; set; }
    public float PrecisionBonus { get; set; }
    public float TenacityBonus { get; set; }
    public float AttackSpeedBonus { get; set; }
    public float MovementSpeedBonus { get; set; }
    public float ArmorPenetration { get; set; }
    public float HealthRegenBonus { get; set; }
    public float ManaRegenBonus { get; set; }
    public float LifeSteal { get; set; }
    public float ManaSteal { get; set; }
    public float CooldownReduction { get; set; }
    public int PvpDamageBonus { get; set; }
    public int PvpDefenseBonus { get; set; }
    public float BonusExperience { get; set; }
    public float DamageReflect { get; set; }
    public float ControlResistance { get; set; }
    public int ArcaneShield { get; set; }
    public int ArcaneShieldMax { get; set; }
    public double ArcaneShieldExpiresAt { get; set; }

    // Base stats (before equipment bonuses) - used by RecalculatePlayerStats
    public int BaseForca { get; set; }
    public int BaseAgilidade { get; set; }
    public int BaseDestreza { get; set; }
    public int BaseInteligencia { get; set; }
    public int StatPoints { get; set; } = 10;

    public List<ItemInstance> Items { get; set; } = new();
    public Dictionary<int, ItemInstance> Equipment { get; set; } = new();
    public int Gold { get; set; }
    public Dictionary<int, double> SkillCooldowns { get; } = new();
    public Dictionary<string, double> ItemCooldowns { get; } = new();
    public double NextBasicAttackTime { get; set; }
    public double NextPetAttackTime { get; set; }
    public Dictionary<string, double> ActiveServerBuffs { get; } = new();
    public bool IsInvisible(double gameTime)
    {
        return ActiveServerBuffs.Any(kv =>
            kv.Value > gameTime
            && (kv.Key.StartsWith("skill:12202", StringComparison.OrdinalIgnoreCase)
                || kv.Key.StartsWith("skill:12102", StringComparison.OrdinalIgnoreCase)
                || kv.Key.Equals("invisibility", StringComparison.OrdinalIgnoreCase)));
    }

    public float TemporaryPrecisionBonus { get; set; }
    public float TemporaryCritChanceBonus { get; set; }
    public float TemporaryAttackSpeedBonus { get; set; }
    public float TemporaryLifeStealBonus { get; set; }
    public float TemporaryDamageBonus { get; set; }
    public float TemporaryDefenseMultiplier { get; set; } = 1f;
    public double HealthRegenAccumulator { get; set; }
    public double ManaRegenAccumulator { get; set; }
    public HashSet<string> UnlockedTalents { get; set; } = new();
    public int[] SkillBarSlots { get; set; } = new int[20];

    public Dictionary<int, PlayerQuest> Quests { get; set; } = new();

    public DateTime VipExpiry { get; set; } = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public bool IsVipActive => VipExpiry > DateTime.UtcNow;

    public PlayerEntity()
    {
        Type = EntityType.Player;
    }

    public int CalculateAttackDamage()
    {
        int atributoOfensivo = CharacterClass.ToLowerInvariant() switch
        {
            "arqueiro" or "ladino" or "assassino" => Destreza,
            "mago" or "elementalista" or "prist" or "priest" or "clerigo" or "clérigo" or "sacerdote" => Inteligencia,
            _ => Forca,
        };
        return Math.Max(1, BaseAttack + atributoOfensivo / 2);
    }

    public int CalculateDefense()
    {
        return Math.Max(0, (int)MathF.Round((Defense + Agilidade / 3 + Forca / 5) * TemporaryDefenseMultiplier));
    }

    public int CalculateMagicDefense()
    {
        return Math.Max(0, (int)MathF.Round((MagicDefense + Inteligencia / 3) * TemporaryDefenseMultiplier));
    }

    public float CalculateEvasion()
    {
        return Math.Clamp(Agilidade * 0.20f + Destreza * 0.05f + EquipmentEvasion, 0f, 45f);
    }

    public float CalculatePrecision()
    {
        return Math.Clamp(75f + Destreza * 0.20f + Agilidade * 0.05f + PrecisionBonus + TemporaryPrecisionBonus, 5f, 98f);
    }

    public float CalculateCritChance()
    {
        return Math.Clamp(Destreza * 0.15f + CritChanceBonus + TemporaryCritChanceBonus, 0f, 60f);
    }

    public float CalculateCritMultiplier()
    {
        return Math.Clamp(1.5f + CritDamageBonus / 100f, 1.5f, 2.5f);
    }

    public float CalculateAttackSpeedMultiplier()
    {
        return Math.Clamp(
            Math.Clamp(1f + Agilidade * 0.015f + AttackSpeedBonus / 100f, 0.25f, 2.0f)
            * (1f + TemporaryAttackSpeedBonus),
            0.25f,
            2.5f);
    }

    public float CalculateMovementSpeedMultiplier()
    {
        return Math.Clamp(1f + MovementSpeedBonus / 100f, 1f, 1.3f);
    }

    public float CalculateTenacity()
    {
        return Math.Clamp(Forca * 0.05f + TenacityBonus, 0f, 75f);
    }

    public int FindEmptyInventorySlot()
    {
        int maxSlots = 30 + Items
            .Where(item => item.Definition?.IsBag == true && item.Slot <= -10 && item.Slot > -16)
            .Sum(item => Math.Clamp(item.Definition?.ExtraSlots ?? 0, 0, 100));
        for (int i = 0; i < maxSlots; i++)
        {
            if (!Items.Any(item => item.Slot == i))
                return i;
        }
        return -1;
    }
}
