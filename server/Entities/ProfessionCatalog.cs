using System.Text.Json;

namespace Mithara.Server.Entities;

public class CatalogIngredient
{
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CatalogRecipe
{
    public int RecipeItemId { get; set; }
    public string Nome { get; set; } = "";
    public int ProducedItemId { get; set; }
    public int ProducedQuantity { get; set; } = 1;
    public int CritBonusQuantity { get; set; } = 1;
    public int RequiredProfLevel { get; set; } = 1;
    public int UnlockLevel { get; set; } = 1;
    public int XpReward { get; set; } = 10;
    public int GoldCost { get; set; } = 0;
    public int SuccessRate { get; set; } = 100;
    public bool Runa { get; set; }
    public string Category { get; set; } = "Vida";
    public List<CatalogIngredient> Ingredientes { get; set; } = new();
}

public class CatalogProfession
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public byte TipoByte { get; set; } = 6;
    public string NpcPrefab { get; set; } = "";
    public List<CatalogRecipe> Receitas { get; set; } = new();
}

public class CatalogFile
{
    public int Version { get; set; } = 1;
    public List<CatalogProfession> Profissoes { get; set; } = new();
}

/// <summary>
/// Catalogo de profissoes/mesas carregado de data/professions.json.
/// Se o arquivo nao existir, um catalogo padrao (alquimista) e gerado a
/// partir do AlchemistRecipeRegistry e gravado no disco, para que o editor
/// sempre tenha um ponto de partida real.
/// </summary>
public static class ProfessionCatalog
{
    private static readonly object _lock = new();

    public const string FileName = "professions.json";

    public static List<CatalogProfession> Professions { get; private set; } = new();

    public static void Load()
    {
        lock (_lock)
        {
            Professions.Clear();
            try
            {
                foreach (string path in GetCandidatePaths())
                {
                    if (!File.Exists(path)) continue;

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), options);
                    if (file?.Profissoes == null) continue;

                    Professions.AddRange(file.Profissoes);
                    break;
                }

                if (Professions.Count == 0)
                {
                    Professions.AddRange(BuildDefault());
                    Save(GetCandidatePaths().First());
                }

                RegisterIntoAlchemistRegistry();

                int receitas = Professions.Sum(p => p.Receitas.Count);
                Logger.Info($"[PROFISSOES] Catalogo carregado: {Professions.Count} professoes, {receitas} receitas.");
            }
            catch (Exception ex)
            {
                Logger.Error("ProfessionCatalog.Load", ex);
            }
        }
    }

    public static void Save(string path)
    {
        var file = new CatalogFile { Version = 1, Profissoes = Professions };
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        Logger.Info($"[PROFISSOES] Catalogo salvo em {path}");
    }

    public static CatalogProfession? Get(string id)
    {
        return Professions.FirstOrDefault(p =>
            p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    public static CatalogProfession? GetByNpcPrefab(string prefabId)
    {
        if (string.IsNullOrWhiteSpace(prefabId)) return null;
        return Professions.FirstOrDefault(p =>
            !string.IsNullOrWhiteSpace(p.NpcPrefab)
            && p.NpcPrefab.Equals(prefabId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static CatalogRecipe? GetRecipe(CatalogProfession prof, int recipeItemId)
    {
        return prof.Receitas.FirstOrDefault(r => r.RecipeItemId == recipeItemId);
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        // Mesmos candidatos usados pelo QuestManager para data/quests.json.
        yield return Path.Combine(AppContext.BaseDirectory, "data", FileName);
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data", FileName));
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "server", "data", FileName));
        yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "server", "data", FileName));
    }

    private static List<CatalogProfession> BuildDefault()
    {
        var receitas = AlchemistRecipeRegistry.GetAll().Select(r => new CatalogRecipe
        {
            RecipeItemId = r.RecipeId,
            Nome = r.Name,
            ProducedItemId = r.ProducesItemId,
            ProducedQuantity = r.ProducesQuantity,
            CritBonusQuantity = r.CritBonusQuantity,
            RequiredProfLevel = r.RequiredLevel,
            UnlockLevel = r.RequiredLevel,
            XpReward = r.XpReward,
            GoldCost = r.GoldCost,
            SuccessRate = 100,
            Runa = r.Type == AlchemistRecipeType.Rune,
            Category = r.Category.ToString(),
            Ingredientes = r.Ingredients
                .Select(i => new CatalogIngredient { ItemId = i.ItemId, Quantity = i.Quantity })
                .ToList(),
        }).ToList();

        return new List<CatalogProfession>
        {
            new()
            {
                Id = "alquimista",
                Nome = "Alquimista",
                TipoByte = (byte)ProfessionType.Alquimista,
                NpcPrefab = "alquimista_herbert",
                Receitas = receitas,
            },
        };
    }

    /// <summary>
    /// Registra todas as receitas do catalogo no registry usado pelos handlers
    /// existentes (aprender/craftar). Receitas do arquivo sobrescrevem as fixas.
    /// </summary>
    private static void RegisterIntoAlchemistRegistry()
    {
        foreach (var prof in Professions)
        {
            foreach (var r in prof.Receitas)
            {
                AlchemistCategory category = AlchemistCategory.Vida;
                if (!string.IsNullOrWhiteSpace(r.Category))
                    Enum.TryParse(r.Category, true, out category);

                int unlockLevel = r.UnlockLevel > 0 ? r.UnlockLevel : r.RequiredProfLevel;

                AlchemistRecipeRegistry.RegisterExternal(new AlchemistRecipe
                {
                    RecipeId = r.RecipeItemId,
                    Name = string.IsNullOrWhiteSpace(r.Nome) ? $"Receita #{r.RecipeItemId}" : r.Nome,
                    Type = r.Runa ? AlchemistRecipeType.Rune : AlchemistRecipeType.Potion,
                    Category = category,
                    RequiredLevel = Math.Max(1, unlockLevel),
                    ProducesItemId = r.ProducedItemId,
                    ProducesQuantity = Math.Max(1, r.ProducedQuantity),
                    XpReward = r.XpReward,
                    GoldCost = r.GoldCost,
                    CritBonusQuantity = Math.Max(0, r.CritBonusQuantity),
                    Ingredients = r.Ingredientes
                        .Select(i => new AlchemistIngredient(i.ItemId, Math.Max(1, i.Quantity)))
                        .ToList(),
                });
            }
        }
    }
}
