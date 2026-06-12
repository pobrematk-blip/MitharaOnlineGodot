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
}

public static class ItemDefinitions
{
    private static readonly Dictionary<int, ItemDefinition> _defs = new();

    static ItemDefinitions()
    {
        Register(new ItemDefinition { Id = 1, Name = "Poção de Vida", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 10 });
        Register(new ItemDefinition { Id = 2, Name = "Poção de Mana", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 25 });
        Register(new ItemDefinition { Id = 3, Name = "Mochila de Couro", Type = ItemType.Bag, IsBag = true, ExtraSlots = 6, MaxStack = 1, BuyPrice = 100 });
        Register(new ItemDefinition { Id = 10, Name = "Espada Curta", Type = ItemType.Weapon, BaseAttack = 5, BuyPrice = 150 });
        Register(new ItemDefinition { Id = 11, Name = "Espada Longa", Type = ItemType.Weapon, BaseAttack = 10, BuyPrice = 200 });
        Register(new ItemDefinition { Id = 12, Name = "Cajado de Madeira", Type = ItemType.Weapon, BaseAttack = 3, Inteligencia = 5, BuyPrice = 120 });
        Register(new ItemDefinition { Id = 13, Name = "Arco Curto", Type = ItemType.Weapon, BaseAttack = 6, Agilidade = 3, BuyPrice = 180 });
        Register(new ItemDefinition { Id = 20, Name = "Capacete de Ferro", Type = ItemType.Helmet, Defense = 3, BuyPrice = 300 });
        Register(new ItemDefinition { Id = 21, Name = "Peitoral de Couro", Type = ItemType.Chestplate, Defense = 5, BuyPrice = 500 });
        Register(new ItemDefinition { Id = 22, Name = "Calças de Couro", Type = ItemType.Pants, Defense = 3, BuyPrice = 250 });
        Register(new ItemDefinition { Id = 23, Name = "Botas de Couro", Type = ItemType.Boots, Defense = 2, Agilidade = 2, BuyPrice = 200 });
        Register(new ItemDefinition { Id = 24, Name = "Luvas de Couro", Type = ItemType.Gloves, Defense = 1, Destreza = 2, BuyPrice = 180 });
        Register(new ItemDefinition { Id = 30, Name = "Colar da Sabedoria", Type = ItemType.Necklace, Inteligencia = 5, BuyPrice = 1000 });
        Register(new ItemDefinition { Id = 31, Name = "Anel de Força", Type = ItemType.Ring, Forca = 3, BuyPrice = 1000 });
        Register(new ItemDefinition { Id = 100, Name = "Pergaminho do Pet", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 500 });
        Register(new ItemDefinition { Id = 101, Name = "Pergaminho de Ressureicao", Type = ItemType.Consumable, MaxStack = 99, IsStackable = true, BuyPrice = 500 });
        Register(new ItemDefinition { Id = 200, Name = "Slime Pet", Type = ItemType.Pet, MaxStack = 1, BuyPrice = 2000 });
        Register(new ItemDefinition { Id = 201, Name = "Lobo Pet", Type = ItemType.Pet, MaxStack = 1, BuyPrice = 3000 });
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
