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

    public Dictionary<int, PlayerQuest> Quests { get; set; } = new();

    public DateTime VipExpiry { get; set; } = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public bool IsVipActive => VipExpiry > DateTime.UtcNow;

    public PlayerEntity()
    {
        Type = EntityType.Player;
    }

    public int CalculateAttackDamage()
    {
        return Math.Max(1, BaseAttack + Forca / 2);
    }

    public int CalculateDefense()
    {
        return Defense + Agilidade / 2;
    }

    public int FindEmptyInventorySlot()
    {
        const int maxSlots = 40;
        for (int i = 0; i < maxSlots; i++)
        {
            if (!Items.Any(item => item.Slot == i))
                return i;
        }
        return -1;
    }
}
