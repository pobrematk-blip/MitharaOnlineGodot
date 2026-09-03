using System.Text.Json;

namespace Mithara.Server.Entities;

public enum ProfessionType : byte
{
    Botanico = 1,
    Lenhador = 2,
    Minerador = 3,
    Pescador = 4,
    Cozinheiro = 5,
    Alquimista = 6,
    Artesao = 7,
    Ferreiro = 8,
    Alfaiate = 9,
    Joalheiro = 10,
}

public enum AlchemistRecipeType : byte
{
    Potion = 0,
    Rune = 1,
}

public enum AlchemistCategory : byte
{
    Vida = 0,
    Mana = 1,
    Estamina = 2,
    Forca = 3,
    Destreza = 4,
    Inteligencia = 5,
    DefesaFisica = 6,
    DefesaMagica = 7,
    Agilidade = 8,
    Sorte = 9,
}

public class ProfessionData
{
    public ProfessionType Type { get; set; }
    public int Level { get; set; } = 1;
    public long Xp { get; set; } = 0;

    public int XpForNextLevel => GetXpForLevel(Level + 1);
    public int XpForCurrentLevel => GetXpForLevel(Level);
    public float XpProgress => Level >= 10 ? 1f : (float)(Xp - XpForCurrentLevel) / Math.Max(1, XpForNextLevel - XpForCurrentLevel);

    public bool AddXp(int amount)
    {
        if (Level >= 10) return false;
        Xp += amount;
        bool leveledUp = false;
        while (Level < 10 && Xp >= XpForNextLevel)
        {
            Level++;
            leveledUp = true;
        }
        return leveledUp;
    }

    public static int GetXpForLevel(int level)
    {
        return level switch
        {
            1 => 0,
            2 => 100,
            3 => 300,
            4 => 600,
            5 => 1000,
            6 => 1500,
            7 => 2200,
            8 => 3200,
            9 => 4500,
            10 => 6000,
            _ => 999999,
        };
    }
}

public class AlchemistRecipe
{
    public int RecipeId { get; set; }
    public string Name { get; set; } = "";
    public AlchemistRecipeType Type { get; set; }
    public AlchemistCategory Category { get; set; }
    public int RequiredLevel { get; set; } = 1;
    public int ProducesItemId { get; set; }
    public int ProducesQuantity { get; set; } = 1;
    public int XpReward { get; set; } = 10;
    public int GoldCost { get; set; } = 0;
    public int CritBonusQuantity { get; set; } = 1;
    public List<AlchemistIngredient> Ingredients { get; set; } = new();
}

public class AlchemistIngredient
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;

    public AlchemistIngredient() { }

    public AlchemistIngredient(int itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

public static class AlchemistRecipeRegistry
{
    private static readonly Dictionary<int, AlchemistRecipe> _recipes = new();
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        RegisterAllRecipes();
        ProfessionCatalog.Load();
    }

    public static AlchemistRecipe? Get(int recipeId)
    {
        Initialize();
        _recipes.TryGetValue(recipeId, out var recipe);
        return recipe;
    }

    public static IEnumerable<AlchemistRecipe> GetAll()
    {
        Initialize();
        return _recipes.Values;
    }

    public static IEnumerable<AlchemistRecipe> GetByCategory(AlchemistCategory category)
    {
        Initialize();
        return _recipes.Values.Where(r => r.Category == category);
    }

    public static IEnumerable<AlchemistRecipe> GetByType(AlchemistRecipeType type)
    {
        Initialize();
        return _recipes.Values.Where(r => r.Type == type);
    }

    public static IEnumerable<AlchemistRecipe> GetAvailableForLevel(int level)
    {
        Initialize();
        return _recipes.Values.Where(r => r.RequiredLevel <= level);
    }

    private static void Register(AlchemistRecipe recipe)
    {
        _recipes[recipe.RecipeId] = recipe;
    }

    /// <summary>
    /// Registra/atualiza uma receita vinda de fora (catalogo professions.json).
    /// </summary>
    public static void RegisterExternal(AlchemistRecipe recipe)
    {
        Initialize();
        _recipes[recipe.RecipeId] = recipe;
    }

    private static void RegisterAllRecipes()
    {
        // IDs de itens (definidos em ItemDefinitions)
        // Ingredientes: 300-309 (base), 350-352 (comuns), 310-413 (tier 2/3)
        // Poções: 110, 111 (existentes) + 122-151 (novas), Runas: 2201-2230
        // Receitas: 2001-2080 (poções), 2081-2160 (runas)

        int recipeBaseId = 2001;

        // === RECEITAS POR CATEGORIA ===
        // Cada categoria tem 3 tiers (I, II, III) com ingredientes progressivos
        // (cat, name, potionIds[3], runeIds[3], t1, t2, t3)

        var definitions = new (
            AlchemistCategory cat, string name,
            int[] potionIds, int[] runeIds,
            (int id, int qty)[] t1, (int id, int qty)[] t2, (int id, int qty)[] t3
        )[]
        {
            // --- Poções Básicas (T1 no NV.1) ---
            (AlchemistCategory.Vida, "Vida",
                new[] { 110, 122, 123 }, new[] { 2201, 2211, 2221 },
                new[] { (300, 2), (350, 1), (351, 1) },
                new[] { (300, 3), (310, 1), (350, 1), (351, 1) },
                new[] { (300, 3), (310, 2), (311, 1), (350, 1), (351, 1) }),

            (AlchemistCategory.Mana, "Mana",
                new[] { 111, 124, 125 }, new[] { 2202, 2212, 2222 },
                new[] { (301, 2), (350, 1), (351, 1) },
                new[] { (301, 3), (320, 1), (350, 1), (351, 1) },
                new[] { (301, 3), (320, 2), (321, 1), (350, 1), (351, 1) }),

            (AlchemistCategory.Estamina, "Estamina",
                new[] { 126, 127, 128 }, new[] { 2203, 2213, 2223 },
                new[] { (302, 2), (350, 1), (351, 1) },
                new[] { (302, 3), (330, 1), (350, 1), (351, 1) },
                new[] { (302, 3), (330, 2), (331, 1), (350, 1), (351, 1) }),

            // --- Poções de Atributos (T1 no NV.4) ---
            (AlchemistCategory.Forca, "Forca",
                new[] { 131, 132, 133 }, new[] { 2204, 2214, 2224 },
                new[] { (303, 2), (340, 1), (341, 1), (350, 1), (351, 1) },
                new[] { (303, 3), (340, 1), (342, 1), (341, 1), (351, 1) },
                new[] { (303, 3), (342, 2), (343, 1), (341, 1), (352, 1), (351, 1) }),

            (AlchemistCategory.Inteligencia, "Inteligencia",
                new[] { 134, 135, 136 }, new[] { 2205, 2215, 2225 },
                new[] { (304, 2), (320, 1), (360, 1), (350, 1), (351, 1) },
                new[] { (304, 3), (306, 1), (320, 1), (360, 1), (351, 1) },
                new[] { (304, 3), (306, 2), (361, 1), (360, 1), (352, 1), (351, 1) }),

            (AlchemistCategory.DefesaFisica, "Defesa Fisica",
                new[] { 137, 138, 139 }, new[] { 2206, 2216, 2226 },
                new[] { (305, 2), (370, 1), (371, 1), (350, 1), (351, 1) },
                new[] { (305, 3), (372, 1), (370, 1), (371, 1), (351, 1) },
                new[] { (305, 3), (372, 2), (373, 1), (371, 1), (352, 1), (351, 1) }),

            // --- Poções de Atributos (T1 no NV.5) ---
            (AlchemistCategory.DefesaMagica, "Defesa Magica",
                new[] { 140, 141, 142 }, new[] { 2207, 2217, 2227 },
                new[] { (306, 2), (380, 1), (381, 1), (350, 1), (351, 1) },
                new[] { (306, 3), (380, 1), (382, 1), (381, 1), (351, 1) },
                new[] { (306, 3), (382, 2), (383, 1), (381, 1), (352, 1), (351, 1) }),

            (AlchemistCategory.Agilidade, "Agilidade",
                new[] { 143, 144, 145 }, new[] { 2208, 2218, 2228 },
                new[] { (307, 2), (308, 1), (390, 1), (350, 1), (351, 1) },
                new[] { (307, 3), (308, 1), (391, 1), (390, 1), (351, 1) },
                new[] { (307, 3), (308, 2), (392, 1), (390, 1), (352, 1), (351, 1) }),

            (AlchemistCategory.Destreza, "Destreza",
                new[] { 146, 147, 148 }, new[] { 2209, 2219, 2229 },
                new[] { (308, 2), (400, 1), (401, 1), (350, 1), (351, 1) },
                new[] { (308, 3), (402, 1), (400, 1), (401, 1), (351, 1) },
                new[] { (308, 3), (402, 2), (403, 1), (401, 1), (352, 1), (351, 1) }),

            // --- Poção de Sorte (T1 no NV.6, mais difícil) ---
            (AlchemistCategory.Sorte, "Sorte",
                new[] { 149, 150, 151 }, new[] { 2210, 2220, 2230 },
                new[] { (309, 2), (410, 1), (411, 1), (350, 1), (351, 1) },
                new[] { (309, 3), (410, 1), (412, 1), (411, 1), (351, 1) },
                new[] { (309, 3), (410, 2), (412, 1), (413, 1), (411, 1), (351, 1) })
        };

        // === NÍVEIS DE DESBLOQUEIO POR TIER ===
        // Vida, Mana, Stamina: T1@1, T2@2, T3@3
        // Força, Int, DefFísica: T1@7, T2@7, T3@10
        // DefMágica, Agilidade, Destreza: T1@7, T2@7, T3@10
        // Sorte: T1@6, T2@7, T3@10
        int[] t1Unlock = { 1, 1, 1, 7, 7, 7, 7, 7, 7, 6 };
        int[] t2Unlock = { 2, 2, 2, 7, 7, 7, 7, 7, 7, 7 };
        int[] t3Unlock = { 3, 3, 3, 10, 10, 10, 10, 10, 10, 10 };

        for (int catIdx = 0; catIdx < definitions.Length; catIdx++)
        {
            var def = definitions[catIdx];

            for (int tier = 0; tier < 3; tier++)
            {
                int requiredLevel = tier switch
                {
                    0 => t1Unlock[catIdx],
                    1 => t2Unlock[catIdx],
                    _ => t3Unlock[catIdx],
                };

                int potionId = def.potionIds[tier];
                int runeId = def.runeIds[tier];
                int potionRecipeId = recipeBaseId + catIdx * 10 + tier;
                int runeRecipeId = recipeBaseId + 80 + catIdx * 10 + tier;

                var tierIngredients = tier switch
                {
                    0 => def.t1,
                    1 => def.t2,
                    _ => def.t3,
                };

                int goldCost = 50 * requiredLevel * requiredLevel;
                int xpReward = 5 + requiredLevel * 3;

                var ingredients = tierIngredients.Select(i => new AlchemistIngredient(i.id, i.qty)).ToList();

                Register(new AlchemistRecipe
                {
                    RecipeId = potionRecipeId,
                    Name = $"Receita: Poção de {def.name} NV.{requiredLevel}",
                    Type = AlchemistRecipeType.Potion,
                    Category = def.cat,
                    RequiredLevel = requiredLevel,
                    ProducesItemId = potionId,
                    ProducesQuantity = 1,
                    XpReward = xpReward,
                    GoldCost = goldCost,
                    CritBonusQuantity = 1 + requiredLevel / 3,
                    Ingredients = ingredients,
                });

                Register(new AlchemistRecipe
                {
                    RecipeId = runeRecipeId,
                    Name = $"Receita: Runa de {def.name} NV.{requiredLevel}",
                    Type = AlchemistRecipeType.Rune,
                    Category = def.cat,
                    RequiredLevel = requiredLevel,
                    ProducesItemId = runeId,
                    ProducesQuantity = 1,
                    XpReward = xpReward + 5,
                    GoldCost = goldCost + 100 * requiredLevel,
                    CritBonusQuantity = 0,
                    Ingredients = ingredients,
                });
            }
        }
    }
}

public class AlchemistItemData
{
    public int BonusValue { get; set; }
    public int DurationSeconds { get; set; }
    public string StatName { get; set; } = "";

    public string ToJson() => JsonSerializer.Serialize(this);
    public static AlchemistItemData? FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<AlchemistItemData>(json);
    }
}
