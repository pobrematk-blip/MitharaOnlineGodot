namespace Mithara.Web.Models;

public class WikiMob
{
    public int Id { get; set; }
    public string PrefabId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Health { get; set; }
    public int AttackDamage { get; set; }
    public int ExperienceReward { get; set; }
    public int GoldMin { get; set; }
    public int GoldMax { get; set; }
    public bool IsBoss { get; set; }
    public bool IsElite { get; set; }
    public string ImageUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public string SpawnMaps { get; set; } = "";
}

public class WikiDrop
{
    public int Id { get; set; }
    public int MobId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemIcon { get; set; } = "";
    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
    public double DropChance { get; set; }
    public string DropChanceDisplay { get; set; } = "";
    public bool IsEquipmentDrop { get; set; }
    public string EquipmentTier { get; set; } = ""; // "normal" ou "elite"
}
