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
    public int MagicDefense { get; set; }
    public int Hp { get; set; }
    public int Mana { get; set; }
    public float Evasion { get; set; }
    public int BuyPrice { get; set; }
    public int RequiredLevel { get; set; } = 1;
    public bool IsElite { get; set; }
    public string AllowedClasses { get; set; } = "";
    public int ForcaMin { get; set; }
    public int ForcaMax { get; set; }
    public int AgilidadeMin { get; set; }
    public int AgilidadeMax { get; set; }
    public int DestrezaMin { get; set; }
    public int DestrezaMax { get; set; }
    public int InteligenciaMin { get; set; }
    public int InteligenciaMax { get; set; }
    public int BaseAttackMin { get; set; }
    public int BaseAttackMax { get; set; }
    public int DefenseMin { get; set; }
    public int DefenseMax { get; set; }
    public int MagicDefenseMin { get; set; }
    public int MagicDefenseMax { get; set; }
    public int HpMin { get; set; }
    public int HpMax { get; set; }
    public int ManaMin { get; set; }
    public int ManaMax { get; set; }
    public float EvasionMin { get; set; }
    public float EvasionMax { get; set; }
    public List<string> AffixPool { get; set; } = new();
}

public static class ItemDefinitions
{
    private static readonly Dictionary<int, ItemDefinition> _defs = new();

    public const int PergaminhoDoPet = 100;
    public const int PergaminhoCriacaoCla = 102;
    public const int PergaminhoVip7Dias = 103;
    public const int PergaminhoVip15Dias = 104;
    public const int PergaminhoVip30Dias = 105;
    public const int PergaminhoVip7DiasTrial = 106;
    public const int PoeiraEstelar = 107;
    public const int LojinhaPequena = 108;
    public const int LojinhaMedia = 109;
    public const int PocaoVida = 110;
    public const int PocaoMana = 111;
    public const int LojinhaGrande = 112;
    public const int PergaminhoResetTalentos = 113;
    public const int PergaminhoDoPet5 = 114;

    public static int GetLojinhaMaxSlots(int itemId) => itemId switch
    {
        LojinhaPequena => 5,
        LojinhaMedia => 10,
        LojinhaGrande => 15,
        _ => 0,
    };

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

    public static IEnumerable<ItemDefinition> GetAll() => _defs.Values;
}
