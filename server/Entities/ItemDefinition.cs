using Mithara.Server.Database;

namespace Mithara.Server.Entities;

public enum ItemType : byte
{
    None = 0,
    Helmet = 1,
    Chestplate = 2,
    Belt = 3,
    Gloves = 4,
    Pants = 5,
    Boots = 6,
    Weapon = 7,
    Shield = 8,
    Necklace = 9,
    Ring = 10,
    Earring = 11,
    Rune = 12,
    Wing = 13,
    Mount = 14,
    Pet = 15,
    Skin = 16,
    Consumable = 50,
    Material = 51,
    Bag = 52,
}

public class ItemDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ItemType Type { get; set; } = ItemType.None;
    public int MaxStack { get; set; } = 1;
    public bool IsStackable { get; set; }
    public bool IsBag { get; set; }
    public int ExtraSlots { get; set; }
    public int Forca { get; set; }
    public int Agilidade { get; set; }
    public int Destreza { get; set; }
    public int Inteligencia { get; set; }
    public int BaseAttack { get; set; }
    public int Defense { get; set; }
    public int BuyPrice { get; set; }
    public List<string> AffixPool { get; set; } = new();
}

public static class ItemDefinitions
{
    private static readonly Dictionary<int, ItemDefinition> _defs = new();

    public const int PergaminhoCriacaoCla = 102;

    public static void LoadFromDatabase(DatabaseManager db)
    {
        _defs.Clear();
        var items = db.LoadItemDefinitions();
        foreach (var item in items)
            _defs[item.Id] = item;
        Logger.Info($"{_defs.Count} definições de item carregadas do banco.");
    }

    public static void Register(ItemDefinition def)
    {
        _defs[def.Id] = def;
    }

    public static ItemDefinition? Get(int id)
    {
        _defs.TryGetValue(id, out var def);
        return def;
    }

    public static bool Exists(int id) => _defs.ContainsKey(id);
}
