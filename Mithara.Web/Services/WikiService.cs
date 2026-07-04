using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mithara.Web.Data;
using Mithara.Web.Models;
using Mithara.Web.Models.ViewModels;
using Npgsql;

namespace Mithara.Web.Services;

public partial class WikiService
{
    private readonly WebDbContext _db;
    private readonly string _gameConnStr;
    private readonly string _itensBaseDir;

    private static readonly ConcurrentDictionary<int, string> _itemIconCache = new();
    private static bool _iconCacheBuilt = false;
    private static readonly object _iconCacheLock = new();

    private static readonly Dictionary<string, string> _manualIcons = new()
    {
        { "Poção de Vida", "Porcao de Vida T1.png" },
        { "Poção de Mana", "Porcao de Mana T1.png" },
        { "Pergaminho do Pet (7 Tentativas)", "Pergaminho de Captura de Pet.png" },
        { "Pergaminho do Pet (5 Tentativas)", "Pergaminho de Captura de Pet.png" },
        { "Pergaminho de Criacao de Cla", "Pergaminho de Criação de Guild.png" },
        { "Pergaminho de Ressurreicao", "Pergaminho de Criação de Guild.png" },
        { "Pergaminho VIP (7 Dias)", "Vip 1.png" },
        { "Pergaminho VIP (15 Dias)", "Vip 2.png" },
        { "Pergaminho VIP (30 Dias)", "vip 3.png" },
        { "Pergaminho VIP Trial (7 Dias)", "Vip 1.png" },
        { "Pergaminho de Reset de Talentos", "Pergaminho de Criação de Guild.png" },
    };

    public WikiService(WebDbContext db, string gameConnStr)
    {
        _db = db;
        _gameConnStr = gameConnStr;
        _itensBaseDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
    }

    private void EnsureIconCacheBuilt()
    {
        if (_iconCacheBuilt) return;
        lock (_iconCacheLock)
        {
            if (_iconCacheBuilt) return;
            BuildIconCache();
            _iconCacheBuilt = true;
        }
    }

    private void BuildIconCache()
    {
        var itensDir = Path.GetFullPath(Path.Combine(_itensBaseDir, "Itens"));
        if (!Directory.Exists(itensDir)) return;

        var tresFiles = Directory.EnumerateFiles(itensDir, "*.tres", SearchOption.AllDirectories);

        foreach (var file in tresFiles)
        {
            var fileName = Path.GetFileName(file);
            var idMatch = Regex.Match(fileName, @"^(\d+)-");
            if (!idMatch.Success) continue;
            var itemId = int.Parse(idMatch.Groups[1].Value);
            if (_itemIconCache.ContainsKey(itemId)) continue;

            try
            {
                var content = File.ReadAllText(file);
                var extResources = new Dictionary<string, string>();

                // Parse ext_resource lines: [ext_resource type="Texture2D" path="res://Itens/Incones/Nome.png" id="xxx"]
                var extMatches = Regex.Matches(content,
                    @"\[ext_resource type=""Texture2D"" path=""res://Itens/Incones/(.+?)"" id=""(.+?)""\]");
                foreach (Match m in extMatches)
                    extResources[m.Groups[2].Value] = m.Groups[1].Value;

                // Find Icone field: Icone = ExtResource("xxx")
                var iconeMatch = Regex.Match(content, @"Icone = ExtResource\(""(.+?)""\)");
                if (iconeMatch.Success && extResources.TryGetValue(iconeMatch.Groups[1].Value, out var iconFile))
                {
                    _itemIconCache[itemId] = iconFile;
                }
            }
            catch { }
        }
    }

    private string GetItemIconUrl(int itemId, string itemName)
    {
        if (_itemIconCache.TryGetValue(itemId, out var iconFile))
            return $"/images/items/{Uri.EscapeDataString(iconFile)}";

        // Manual override for known consumíveis/items without .tres
        if (_manualIcons.TryGetValue(itemName, out var manualIcon))
            return $"/images/items/{Uri.EscapeDataString(manualIcon)}";

        // Fallback: try to find by name (remove special chars, match file)
        var normalizedName = RemoveDiacritics(itemName).Replace(" ", "").ToLowerInvariant();
        var iconsDir = Path.GetFullPath(Path.Combine(_itensBaseDir, "Mithara.Web", "wwwroot", "images", "items"));
        if (Directory.Exists(iconsDir))
        {
            var match = Directory.EnumerateFiles(iconsDir, "*.png")
                .FirstOrDefault(f => RemoveDiacritics(Path.GetFileNameWithoutExtension(f))
                    .Replace(" ", "").Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return $"/images/items/{Uri.EscapeDataString(Path.GetFileName(match))}";
        }

        return "";
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private NpgsqlConnection CreateGameConnection()
    {
        var conn = new NpgsqlConnection(_gameConnStr);
        conn.Open();
        return conn;
    }

    public async Task<List<ItemSummary>> SearchItemsAsync(string search, string typeFilter, int page, int pageSize = 30)
    {
        EnsureIconCacheBuilt();

        var result = new List<ItemSummary>();

        using var conn = CreateGameConnection();
        using var cmd = conn.CreateCommand();

        var where = new List<string>();
        if (!string.IsNullOrEmpty(search))
        {
            where.Add("LOWER(name) LIKE @s");
            cmd.Parameters.AddWithValue("@s", $"%{search.ToLower()}%");
        }
        if (!string.IsNullOrEmpty(typeFilter))
        {
            where.Add("type = @t");
            cmd.Parameters.AddWithValue("@t", int.Parse(typeFilter));
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        cmd.CommandText = $"""
            SELECT id, name, type, definition_data
            FROM item_definitions
            {whereClause}
            ORDER BY id
            LIMIT @limit OFFSET @offset
            """;
        cmd.Parameters.AddWithValue("@limit", pageSize);
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var defData = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var summary = ParseItemSummary(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), defData);
            result.Add(summary);
        }

        return result;
    }

    public async Task<int> CountItemsAsync(string search, string typeFilter)
    {
        using var conn = CreateGameConnection();
        using var cmd = conn.CreateCommand();

        var where = new List<string>();
        if (!string.IsNullOrEmpty(search))
        {
            where.Add("LOWER(name) LIKE @s");
            cmd.Parameters.AddWithValue("@s", $"%{search.ToLower()}%");
        }
        if (!string.IsNullOrEmpty(typeFilter))
        {
            where.Add("type = @t");
            cmd.Parameters.AddWithValue("@t", int.Parse(typeFilter));
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        cmd.CommandText = $"SELECT COUNT(*) FROM item_definitions {whereClause}";

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<string>> GetItemTypesAsync()
    {
        using var conn = CreateGameConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT type FROM item_definitions ORDER BY type";

        var types = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            types.Add(reader.GetInt32(0).ToString());

        return types;
    }

    public async Task<int> CountItemsAsync()
    {
        using var conn = CreateGameConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM item_definitions";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<WikiItemDetailViewModel?> GetItemDetailAsync(int itemId)
    {
        using var conn = CreateGameConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, type, definition_data, affix_pool FROM item_definitions WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var id = reader.GetInt32(0);
        var name = reader.GetString(1);
        var type = reader.GetInt32(2);
        var defData = reader.IsDBNull(3) ? "" : reader.GetString(3);
        var affixPool = reader.IsDBNull(4) ? "" : reader.GetString(4);

        reader.Close();

        EnsureIconCacheBuilt();

        var detail = new WikiItemDetailViewModel
        {
            Id = id,
            Name = name,
            TypeName = GetItemTypeName(type),
            AffixPool = affixPool,
            IconUrl = GetItemIconUrl(id, name),
        };

        if (!string.IsNullOrEmpty(defData))
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(defData);
                var root = json.RootElement;

                if (root.TryGetProperty("RequiredLevel", out var rl))
                    detail.RequiredLevel = rl.GetInt32();
                if (root.TryGetProperty("IsElite", out var ie))
                    detail.IsElite = ie.GetBoolean();
                if (root.TryGetProperty("BuyPrice", out var bp))
                    detail.BuyPrice = bp.GetInt32();
                if (root.TryGetProperty("AllowedClasses", out var ac))
                {
                    // AllowedClasses is stored as a string like "Arqueiro" or "" in the server
                    var classStr = ac.GetString() ?? "";
                    if (!string.IsNullOrEmpty(classStr))
                    {
                        detail.AllowedClasses.Add(FormatClassName(classStr));
                    }
                }

                AddStat(detail.Stats, "Nível Requerido", detail.RequiredLevel > 0 ? detail.RequiredLevel.ToString() : "—");
                AddStat(detail.Stats, "Tipo", detail.TypeName);

                AddStatFromJson(detail.Stats, root, "Forca", "Força");
                AddStatFromJson(detail.Stats, root, "Agilidade", "Agilidade");
                AddStatFromJson(detail.Stats, root, "Destreza", "Destreza");
                AddStatFromJson(detail.Stats, root, "Inteligencia", "Inteligência");
                AddStatFromJson(detail.Stats, root, "BaseAttack", "Ataque Base");
                AddStatFromJson(detail.Stats, root, "Defense", "Defesa Física");
                AddStatFromJson(detail.Stats, root, "MagicDefense", "Defesa Mágica");
                AddStatFromJson(detail.Stats, root, "Hp", "HP");
                AddStatFromJson(detail.Stats, root, "Mana", "Mana");
                AddStatFromJson(detail.Stats, root, "Evasion", "Evasão");
                AddStatFromJson(detail.Stats, root, "MaxStack", "Empilhamento Máx");
                AddBoolStat(detail.Stats, "É Elite", detail.IsElite);

                if (detail.AllowedClasses.Count > 0)
                    AddStat(detail.Stats, "Classes", string.Join(", ", detail.AllowedClasses));

                if (detail.BuyPrice > 0)
                    AddStat(detail.Stats, "Preço de Compra", $"{detail.BuyPrice} gold");
            }
            catch { }
        }

        // Find which mobs drop this item
        var drops = await _db.WikiDrops
            .Where(d => d.ItemId == itemId)
            .ToListAsync();

        foreach (var drop in drops)
        {
            var mob = await _db.WikiMobs.FindAsync(drop.MobId);
            if (mob != null)
            {
                detail.DroppedBy.Add(new MobDropInfo
                {
                    MobId = mob.Id,
                    MobName = mob.Name,
                    MobLevel = mob.Level,
                    DropChance = drop.DropChance,
                    DropChanceDisplay = drop.DropChanceDisplay,
                });
            }
        }

        return detail;
    }

    public async Task<List<WikiMob>> GetMobsAsync(string search = "", int page = 1, int pageSize = 30)
    {
        var query = _db.WikiMobs.AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(m => EF.Functions.Like(m.Name.ToLower(), $"%{search.ToLower()}%"));

        return await query
            .OrderBy(m => m.Level)
            .ThenBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountMobsAsync(string search = "")
    {
        var query = _db.WikiMobs.AsQueryable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(m => EF.Functions.Like(m.Name.ToLower(), $"%{search.ToLower()}%"));
        return await query.CountAsync();
    }

    public async Task<WikiMobDetailViewModel?> GetMobDetailAsync(int mobId)
    {
        EnsureIconCacheBuilt();
        var mob = await _db.WikiMobs.FindAsync(mobId);
        if (mob == null) return null;

        var drops = await _db.WikiDrops
            .Where(d => d.MobId == mobId)
            .OrderByDescending(d => d.DropChance)
            .ToListAsync();

        foreach (var drop in drops)
        {
            if (drop.ItemId > 0 && string.IsNullOrEmpty(drop.ItemIcon))
            {
                drop.ItemIcon = GetItemIconUrl(drop.ItemId, drop.ItemName);
            }
        }

        return new WikiMobDetailViewModel { Mob = mob, Drops = drops };
    }

    public async Task<(List<SearchResult> Items, List<SearchResult> Mobs)> SearchAsync(string query)
    {
        var results = new List<SearchResult>();

        // Search items
        using var conn = CreateGameConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM item_definitions WHERE LOWER(name) LIKE @q LIMIT 10";
        cmd.Parameters.AddWithValue("@q", $"%{query.ToLower()}%");
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                Type = "item",
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Subtitle = "Item",
            });
        }
        reader.Close();

        // Search mobs
        var mobs = await _db.WikiMobs
            .Where(m => EF.Functions.Like(m.Name.ToLower(), $"%{query.ToLower()}%"))
            .Take(10)
            .ToListAsync();

        foreach (var mob in mobs)
        {
            results.Add(new SearchResult
            {
                Type = "mob",
                Id = mob.Id,
                Name = mob.Name,
                Subtitle = $"Nível {mob.Level} {(mob.IsBoss ? "- Boss" : mob.IsElite ? "- Elite" : "")}",
            });
        }

        // Return split
        return (
            results.Where(r => r.Type == "item").ToList(),
            results.Where(r => r.Type == "mob").ToList()
        );
    }

    public async Task EnsureDefaultDataAsync()
    {
        if (await _db.WikiMobs.AnyAsync())
            return;

        var mobs = new List<WikiMob>
        {
            new() { PrefabId = "slime", Name = "Slime", Level = 3, Health = 60, AttackDamage = 7, ExperienceReward = 8, GoldMin = 3, GoldMax = 10, IsBoss = false, IsElite = false, SpawnMaps = "Floresta Inicial, Planícies Verdes", Description = "Uma criatura gelatinosa comum nas planícies. Sua substância viscosa é usada em poções." },
            new() { PrefabId = "slimeElite", Name = "Slime Elite", Level = 5, Health = 250, AttackDamage = 18, ExperienceReward = 30, GoldMin = 15, GoldMax = 40, IsBoss = false, IsElite = true, SpawnMaps = "Floresta Inicial", Description = "Uma versão maior e mais poderosa do Slime comum. Derruba equipamentos." },
            new() { PrefabId = "goblin", Name = "Goblin", Level = 2, Health = 60, AttackDamage = 7, ExperienceReward = 5, GoldMin = 3, GoldMax = 10, IsBoss = false, IsElite = false, SpawnMaps = "Cavernas Rasas, Planícies Verdes", Description = "Criaturas pequenas e traiçoeiras que atacam em grupos." },
            new() { PrefabId = "wolf", Name = "Lobo", Level = 3, Health = 80, AttackDamage = 10, ExperienceReward = 8, GoldMin = 5, GoldMax = 15, IsBoss = false, IsElite = false, SpawnMaps = "Floresta Inicial, Montanhas", Description = "Lobos selvagens que caçam em alcateia." },
            new() { PrefabId = "skeleton", Name = "Esqueleto", Level = 5, Health = 120, AttackDamage = 15, ExperienceReward = 14, GoldMin = 8, GoldMax = 25, IsBoss = false, IsElite = false, SpawnMaps = "Catacumbas, Ruínas Antigas", Description = "Guerreiros mortos-vivos que guardam tumbas antigas." },
            new() { PrefabId = "cogumelo", Name = "Cogumelo", Level = 8, Health = 250, AttackDamage = 16, ExperienceReward = 35, GoldMin = 15, GoldMax = 40, IsBoss = false, IsElite = false, SpawnMaps = "Floresta Sombria, Pântano", Description = "Cogumelos gigantes que liberam esporos tóxicos." },
            new() { PrefabId = "cogumeloElite", Name = "Cogumelo Elite", Level = 11, Health = 700, AttackDamage = 28, ExperienceReward = 90, GoldMin = 45, GoldMax = 120, IsBoss = false, IsElite = true, SpawnMaps = "Floresta Sombria", Description = "Cogumelo gigante corrompido por magia negra." },
            new() { PrefabId = "plantaCarnivora", Name = "Planta Carnívora", Level = 10, Health = 350, AttackDamage = 20, ExperienceReward = 45, GoldMin = 18, GoldMax = 50, IsBoss = false, IsElite = false, SpawnMaps = "Pântano, Floresta Sombria", Description = "Plantas mutantes que devoram viajantes desatentos." },
            new() { PrefabId = "plantaCarnivoraElite", Name = "Planta Carnívora Elite", Level = 13, Health = 900, AttackDamage = 35, ExperienceReward = 110, GoldMin = 60, GoldMax = 150, IsBoss = false, IsElite = true, SpawnMaps = "Pântano Profundo", Description = "Planta ancestral alimentada por sangue." },
            new() { PrefabId = "slimeBoss", Name = "Slime Boss", Level = 10, Health = 3000, AttackDamage = 35, ExperienceReward = 150, GoldMin = 100, GoldMax = 500, IsBoss = true, IsElite = false, SpawnMaps = "Covil do Slime", Description = "Um Slime gigante corrompido por energia arcana." },
            new() { PrefabId = "boss_demon", Name = "Demon Lord", Level = 10, Health = 2000, AttackDamage = 30, ExperienceReward = 80, GoldMin = 50, GoldMax = 200, IsBoss = true, IsElite = false, SpawnMaps = "Fortaleza Abandonada", Description = "Um senhor demoníaco que comanda exércitos das sombras." },
        };

        _db.WikiMobs.AddRange(mobs);
        await _db.SaveChangesAsync();

        // Build a lookup by PrefabId to get the actual generated IDs
        var mobLookup = await _db.WikiMobs.ToDictionaryAsync(m => m.PrefabId, m => m.Id);

        var drops = new List<WikiDrop>
        {
            // Slime drops (server: Vida 20%, Mana 20%)
            new() { MobId = mobLookup["slime"], ItemId = 110, ItemName = "Poção de Vida", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["slime"], ItemId = 111, ItemName = "Poção de Mana", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            // Slime Elite drops (server: Vida 20%, Mana 20%, Equip Normal 25%)
            new() { MobId = mobLookup["slimeElite"], ItemId = 110, ItemName = "Poção de Vida", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["slimeElite"], ItemId = 111, ItemName = "Poção de Mana", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["slimeElite"], ItemId = 0, ItemName = "Equipamento Normal Nv.1", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.25, DropChanceDisplay = "25%", IsEquipmentDrop = true, EquipmentTier = "normal" },
            // Goblin drops (server: none)
            // Wolf drops (server: none)
            // Skeleton drops (server: none)
            // Cogumelo drops (server: Vida 20%, Mana 20%)
            new() { MobId = mobLookup["cogumelo"], ItemId = 110, ItemName = "Poção de Vida", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["cogumelo"], ItemId = 111, ItemName = "Poção de Mana", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            // Cogumelo Elite drops (server: Vida 20%, Mana 20%, Equip Normal 25%)
            new() { MobId = mobLookup["cogumeloElite"], ItemId = 110, ItemName = "Poção de Vida", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["cogumeloElite"], ItemId = 111, ItemName = "Poção de Mana", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.20, DropChanceDisplay = "20%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["cogumeloElite"], ItemId = 0, ItemName = "Equipamento Normal Nv.10", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.25, DropChanceDisplay = "25%", IsEquipmentDrop = true, EquipmentTier = "normal" },
            // Planta drops (server: none)
            // Planta Elite drops (server: Equip Normal 25%)
            new() { MobId = mobLookup["plantaCarnivoraElite"], ItemId = 0, ItemName = "Equipamento Normal Nv.10", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.25, DropChanceDisplay = "25%", IsEquipmentDrop = true, EquipmentTier = "normal" },
            // Slime Boss drops (server: Poeira 25% qty 5-10, Pergaminho Pet 1%, Equip Elite 15%)
            new() { MobId = mobLookup["slimeBoss"], ItemId = 107, ItemName = "Poeira Estelar", MinQuantity = 5, MaxQuantity = 10, DropChance = 0.25, DropChanceDisplay = "25%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["slimeBoss"], ItemId = 100, ItemName = "Pergaminho do Pet", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.01, DropChanceDisplay = "1%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["slimeBoss"], ItemId = 0, ItemName = "Equipamento Elite Nv.10", MinQuantity = 1, MaxQuantity = 1, DropChance = 0.15, DropChanceDisplay = "15%", IsEquipmentDrop = true, EquipmentTier = "elite" },
            // Demon Lord drops (server: no elite equip, Vida 75% qty 2-4, Mana 75% qty 2-4)
            new() { MobId = mobLookup["boss_demon"], ItemId = 110, ItemName = "Poção de Vida", MinQuantity = 2, MaxQuantity = 4, DropChance = 0.75, DropChanceDisplay = "75%", IsEquipmentDrop = false },
            new() { MobId = mobLookup["boss_demon"], ItemId = 111, ItemName = "Poção de Mana", MinQuantity = 2, MaxQuantity = 4, DropChance = 0.75, DropChanceDisplay = "75%", IsEquipmentDrop = false },
        };

        _db.WikiDrops.AddRange(drops);
        await _db.SaveChangesAsync();
    }

    private ItemSummary ParseItemSummary(int id, string name, int type, string defData)
    {
        var summary = new ItemSummary
        {
            Id = id,
            Name = name,
            Type = type.ToString(),
            TypeName = GetItemTypeName(type),
            IconUrl = GetItemIconUrl(id, name),
        };

        if (!string.IsNullOrEmpty(defData))
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(defData);
                var root = json.RootElement;
                if (root.TryGetProperty("RequiredLevel", out var rl))
                    summary.RequiredLevel = rl.GetInt32();
                if (root.TryGetProperty("IsElite", out var ie))
                    summary.IsElite = ie.GetBoolean();
                if (root.TryGetProperty("BuyPrice", out var bp))
                    summary.BuyPrice = bp.GetInt32();
                if (root.TryGetProperty("AllowedClasses", out var ac))
                {
                    var classStr = ac.GetString() ?? "";
                    if (!string.IsNullOrEmpty(classStr))
                        summary.Classes.Add(FormatClassName(classStr));
                }
            }
            catch { }
        }

        return summary;
    }

    private static string FormatClassName(string className)
    {
        return className switch
        {
            "Arqueiro" => "Arqueiro",
            "Ladino" => "Assassino",
            "Berseker" => "Berserker",
            "Guardiao" => "Guardião",
            "Clerigo" => "Clérigo",
            "Mago" => "Mago",
            _ => className,
        };
    }

    private static string GetItemTypeName(int type)
    {
        return type switch
        {
            1 => "Capacete",
            2 => "Peitoral",
            3 => "Cinto",
            4 => "Luvas",
            5 => "Calças",
            6 => "Botas",
            7 => "Arma",
            8 => "Escudo",
            9 => "Colar",
            10 => "Anel",
            11 => "Brinco",
            12 => "Runa",
            13 => "Asa",
            14 => "Montaria",
            15 => "Pet",
            16 => "Skin",
            17 => "Consumível",
            50 => "Consumível",
            51 => "Material",
            52 => "Bolsa",
            _ => "Outro",
        };
    }

    private static void AddStat(List<ItemStatDisplay> stats, string name, string value)
    {
        stats.Add(new ItemStatDisplay { Name = name, Value = value });
    }

    private static void AddStatFromJson(List<ItemStatDisplay> stats, System.Text.Json.JsonElement root, string propName, string displayName)
    {
        if (root.TryGetProperty(propName, out var val) && val.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            var num = val.GetInt32();
            if (num != 0)
            {
                // Check for min/max
                var minName = propName + "Min";
                var maxName = propName + "Max";
                if (root.TryGetProperty(minName, out var minVal) && root.TryGetProperty(maxName, out var maxVal))
                {
                    var min = minVal.GetInt32();
                    var max = maxVal.GetInt32();
                    if (min != 0 || max != 0)
                        stats.Add(new ItemStatDisplay { Name = displayName, Value = $"{min} ~ {max}" });
                }
                else
                {
                    stats.Add(new ItemStatDisplay { Name = displayName, Value = num.ToString() });
                }
            }
        }
    }

    private static void AddBoolStat(List<ItemStatDisplay> stats, string name, bool value)
    {
        if (value)
            stats.Add(new ItemStatDisplay { Name = name, Value = "Sim" });
    }
}
