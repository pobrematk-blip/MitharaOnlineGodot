namespace Mithara.Server.Entities;

public class ItemInstance
{
    public int DbId { get; set; }
    public int ItemId { get; set; }
    public int Slot { get; set; }
    public int Quantity { get; set; }
    public int RefineLevel { get; set; }
    public ItemRoll Roll { get; set; } = new();

    public ItemDefinition? Definition => ItemDefinitions.Get(ItemId);
    public string Name => Definition?.Name ?? $"Item#{ItemId}";
    public ItemType Type => Definition?.Type ?? ItemType.None;
    public bool IsStackable => Definition?.IsStackable ?? false;
    public int MaxStack => Definition?.MaxStack ?? 1;
}

public enum ItemRarity : byte
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Mythic,
}

public sealed class ItemRoll
{
    public ItemRarity Rarity { get; set; }
    public int Forca { get; set; }
    public int Agilidade { get; set; }
    public int Destreza { get; set; }
    public int Inteligencia { get; set; }
    public int BaseAttack { get; set; }
    public int Defense { get; set; }
    public int MagicDefense { get; set; }
    public int Hp { get; set; }
    public int Mana { get; set; }
    public float Evasion { get; set; }
    public Dictionary<string, float> Affixes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsRolled { get; set; }
}

public static class ItemRoller
{
    private static readonly double[] RarityWeights = { 0.40, 0.25, 0.17, 0.11, 0.05, 0.02 };
    private static readonly float[] RarityMultipliers = { 1f, 1.05f, 1.10f, 1.15f, 1.20f, 1.25f };
    private static readonly (int min, int max)[] AffixCounts = { (0, 0), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6) };

    public static void EnsureRolled(ItemInstance instance, ItemRarity? forcedRarity = null)
    {
        if (instance.Roll.IsRolled || instance.Definition is not { } def
            || def.IsStackable || (int)def.Type is < 1 or > 16)
            return;

        ItemRarity[] allowedRarities = def.IsElite
            ? new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Mythic }
            : new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare };
        var rarity = forcedRarity.HasValue && allowedRarities.Contains(forcedRarity.Value)
            ? forcedRarity.Value
            : RollRarity(allowedRarities);
        float multiplier = RarityMultipliers[(int)rarity];
        instance.Roll = new ItemRoll
        {
            Rarity = rarity,
            Forca = RollScaled(def.ForcaMin, def.ForcaMax, def.Forca, multiplier),
            Agilidade = RollScaled(def.AgilidadeMin, def.AgilidadeMax, def.Agilidade, multiplier),
            Destreza = RollScaled(def.DestrezaMin, def.DestrezaMax, def.Destreza, multiplier),
            Inteligencia = RollScaled(def.InteligenciaMin, def.InteligenciaMax, def.Inteligencia, multiplier),
            BaseAttack = RollScaled(def.BaseAttackMin, def.BaseAttackMax, def.BaseAttack, multiplier),
            Defense = RollScaled(def.DefenseMin, def.DefenseMax, def.Defense, multiplier),
            MagicDefense = RollScaled(def.MagicDefenseMin, def.MagicDefenseMax, def.MagicDefense, multiplier),
            Hp = RollScaled(def.HpMin, def.HpMax, def.Hp, multiplier),
            Mana = RollScaled(def.ManaMin, def.ManaMax, def.Mana, multiplier),
            Evasion = RollScaled(def.EvasionMin, def.EvasionMax, def.Evasion, multiplier),
            IsRolled = true,
        };

        var pool = def.AffixPool
            .Where(affix => IsAffixAllowedForItem(def, affix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var countRange = AffixCounts[(int)rarity];
        int count = Math.Min(pool.Count, Random.Shared.Next(countRange.min, countRange.max + 1));
        foreach (string affix in pool.OrderBy(_ => Random.Shared.Next()).Take(count))
            instance.Roll.Affixes[affix] = RollAffixValue(affix, def.RequiredLevel);
    }

    private static ItemRarity RollRarity(IReadOnlyList<ItemRarity> allowedRarities)
    {
        double allowedWeight = allowedRarities.Sum(rarity => RarityWeights[(int)rarity]);
        double roll = Random.Shared.NextDouble() * allowedWeight;
        double cumulative = 0;
        foreach (ItemRarity rarity in allowedRarities)
        {
            cumulative += RarityWeights[(int)rarity];
            if (roll <= cumulative)
                return rarity;
        }
        return allowedRarities[^1];
    }

    private static int RollScaled(int min, int max, int fallback, float multiplier)
    {
        int value = RollRange(min, max, fallback);
        return (int)MathF.Round(value * multiplier);
    }

    private static float RollScaled(float min, float max, float fallback, float multiplier)
    {
        float value = min == 0 && max == 0 ? fallback : min + (float)Random.Shared.NextDouble() * (max - min);
        return MathF.Round(value * multiplier, 2);
    }

    private static int RollRange(int min, int max, int fallback)
    {
        if (min == 0 && max == 0) return fallback;
        if (max < min) (min, max) = (max, min);
        return Random.Shared.Next(min, max + 1);
    }

    private static float RollAffixValue(string name, int level)
    {
        int band = level <= 25 ? 0 : level <= 45 ? 1 : 2;
        (float min, float max)[] ranges = name.ToLowerInvariant() switch
        {
            "chancecritica" or "velocidadeataque" => new[] { (1f, 2f), (2f, 4f), (4f, 8f) },
            "danocriticobonus" => new[] { (2f, 4f), (4f, 8f), (8f, 15f) },
            "precisao" => new[] { (2f, 5f), (5f, 10f), (10f, 20f) },
            "penetracaoarmadura" => new[] { (1f, 3f), (3f, 6f), (6f, 12f) },
            "evasao" or "roubovida" or "roubomana" or "reducaocooldown" => new[] { (1f, 2f), (2f, 3f), (3f, 5f) },
            "hp" or "mana" => new[] { (10f, 25f), (25f, 50f), (50f, 100f) },
            _ => new[] { (1f, 2f), (2f, 4f), (4f, 8f) },
        };
        var range = ranges[band];
        return MathF.Round(range.min + (float)Random.Shared.NextDouble() * (range.max - range.min), 2);
    }

    public static bool IsAffixAllowedForItem(ItemDefinition def, string affix)
    {
        string key = NormalizeAffixName(affix);

        if (key is "roubovida" or "roubomana" or "tenacidade" or "penetracaoarmadura")
            return def.Type is ItemType.Necklace or ItemType.Ring or ItemType.Earring;

        if (key is "reflexao" or "reflexaodano")
            return IsTrueShield(def) || IsHeavyArmor(def);

        return true;
    }

    public static bool IsTrueShield(ItemDefinition def)
    {
        if (def.Type != ItemType.Shield)
            return false;

        return def.Name.Contains("Escudo", StringComparison.OrdinalIgnoreCase)
            || def.AllowedClasses.Contains("Guardiao", StringComparison.OrdinalIgnoreCase)
            || def.AllowedClasses.Contains("Guardião", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHeavyArmor(ItemDefinition def)
    {
        if (def.Type is not (ItemType.Helmet or ItemType.Chestplate or ItemType.Belt or ItemType.Gloves or ItemType.Pants or ItemType.Boots))
            return false;

        return def.AllowedClasses.Contains("Berseker", StringComparison.OrdinalIgnoreCase)
            || def.AllowedClasses.Contains("Berserker", StringComparison.OrdinalIgnoreCase)
            || def.AllowedClasses.Contains("Guardiao", StringComparison.OrdinalIgnoreCase)
            || def.AllowedClasses.Contains("Guardião", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeAffixName(string name)
    {
        return name.Trim()
            .Replace("ç", "c", StringComparison.OrdinalIgnoreCase)
            .Replace("ã", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("á", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("â", "a", StringComparison.OrdinalIgnoreCase)
            .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("ê", "e", StringComparison.OrdinalIgnoreCase)
            .Replace("í", "i", StringComparison.OrdinalIgnoreCase)
            .Replace("ó", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ô", "o", StringComparison.OrdinalIgnoreCase)
            .Replace("ú", "u", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }
}
