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
    public int PartyId { get; set; } = -1;
    public int GuildId { get; set; } = -1;
    public string GuildName { get; set; } = "";

    public int BaseAttack { get; set; }
    public int Defense { get; set; }
    public int MagicDefense { get; set; }
    public float EquipmentEvasion { get; set; }

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
    public Dictionary<string, double> ActiveServerBuffs { get; } = new();
    public float TemporaryPrecisionBonus { get; set; }
    public float TemporaryCritChanceBonus { get; set; }
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
            "mago" or "prist" or "clerigo" or "clérigo" => Inteligencia,
            _ => Forca,
        };
        return Math.Max(1, BaseAttack + atributoOfensivo / 2);
    }

    public int CalculateDefense()
    {
        return Defense + Agilidade / 2;
    }

    public int FindEmptyInventorySlot()
    {
        const int maxSlots = 30;
        for (int i = 0; i < maxSlots; i++)
        {
            if (!Items.Any(item => item.Slot == i))
                return i;
        }
        return -1;
    }
}
