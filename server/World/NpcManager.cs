using Mithara.Server.Entities;

namespace Mithara.Server.World;

public class NpcTemplate
{
    public string PrefabId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DialogId { get; set; } = "";
    public string ShopId { get; set; } = "";
    public string Race { get; set; } = "Humano";
    public string AnimPrefix { get; set; } = "mago";
    public string FactionId { get; set; } = "solari";
}

public class DialogNode
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<DialogOption> Options { get; set; } = new();
}

public class DialogOption
{
    public string Text { get; set; } = "";
    public string Action { get; set; } = ""; // "goto", "shop", "quest_available", "quest_complete", "close"
    public string ActionData { get; set; } = ""; // dialog node id for "goto"
}

public class ShopEntry
{
    public int ItemId { get; set; }
    public int Price { get; set; }
    public int Stock { get; set; } = -1;
}

public class NpcManager
{
    private readonly Dictionary<string, NpcTemplate> _templates = new();
    private readonly List<NpcSpawnPoint> _spawnPoints = new();
    private readonly Dictionary<string, DialogNode> _dialogs = new();
    private readonly Dictionary<string, List<ShopEntry>> _shops = new();

    public NpcManager()
    {
        RegisterNpcs();
        RegisterDialogs();
        RegisterShops();
    }

    private void RegisterNpcs()
    {
        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "banqueiro",
            Name = "Banqueiro",
            DialogId = "banco",
            Race = "Secretaria",
            AnimPrefix = "padrao",
            FactionId = "solari",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "guarda_solareth",
            Name = "Guardião de Solareth",
            DialogId = "guilda",
            Race = "Cidadão",
            AnimPrefix = "mago",
            FactionId = "solari",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "general_merchant",
            Name = "General Merchante",
            DialogId = "general_merchant",
            ShopId = "general_merchant_shop",
            Race = "Mercador Geral",
            AnimPrefix = "padrao",
            FactionId = "solari",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "refiner",
            Name = "Refinador",
            DialogId = "refino",
            Race = "Cidadão",
            AnimPrefix = "padrao",
            FactionId = "solari",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "merchant_auctioneer",
            Name = "Mercador Leiloeiro",
            DialogId = "leilao",
            Race = "Mercador",
            AnimPrefix = "padrao",
            FactionId = "solari",
        });

        _spawnPoints.Add(new NpcSpawnPoint { X = 1109, Y = 957, PrefabId = "banqueiro" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1098, Y = 1177, PrefabId = "guarda_solareth" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1130, Y = 1040, PrefabId = "general_merchant" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1070, Y = 1025, PrefabId = "refiner" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1145, Y = 1040, PrefabId = "merchant_auctioneer" });
    }

    private void RegisterDialogs()
    {
        _dialogs["banco"] = new DialogNode
        {
            Id = "banco",
            Text = "Bem-vindo ao Banco! Aqui seus tesouros ficam seguros. Deseja acessar seu cofre?",
            Options = new List<DialogOption>
            {
                new() { Text = "Sim, abrir banco", Action = "bank", ActionData = "open" },
                new() { Text = "Não, depois", Action = "close" },
            },
        };

        _dialogs["guilda"] = new DialogNode
        {
            Id = "guilda",
            Text = "Bem-vindo, aventureiro! Já ouviu falar de Solareth? " +
                   "Dizem que é uma terra próspera onde aventureiros audaciosos " +
                   "fundaram sua própria guilda. Você tem coragem de começar essa jornada? " +
                   "(Para fundar uma guilda é preciso ter 10.000 moedas de ouro " +
                   "ou um Pergaminho de Criação de Clã.)",
            Options = new List<DialogOption>
            {
                new() { Text = "Quero fundar uma guilda em Solareth!", Action = "guild_open_form", ActionData = "" },
                new() { Text = "Ainda não estou pronto", Action = "close" },
            },
        };

        _dialogs["general_merchant"] = new DialogNode
        {
            Id = "general_merchant",
            Text = "Bem-vindo à loja geral! Compro itens de aventureiros e vendo suprimentos. " +
                   "O que você deseja?",
            Options = new List<DialogOption>
            {
                new() { Text = "Comprar itens", Action = "shop", ActionData = "general_merchant_shop" },
                new() { Text = "Vender itens", Action = "merchant_sell", ActionData = "" },
                new() { Text = "Sair", Action = "close" },
            },
        };

        _dialogs["leilao"] = new DialogNode
        {
            Id = "leilao",
            Text = "Bem-vindo ao Mercado das Lojinhas! Aqui você encontra itens de todos os vendedores ambulantes. Deseja ver o catálogo completo?",
            Options = new List<DialogOption>
            {
                new() { Text = "Ver itens das lojinhas", Action = "leilao_list", ActionData = "" },
                new() { Text = "Sair", Action = "close" },
            },
        };

        _dialogs["refino"] = new DialogNode
        {
            Id = "refino",
            Text = "Bem-vindo à forja! Posso refinar seu equipamento para torná-lo mais poderoso. " +
                   "Cada nível de refino aumenta os atributos do item permanentemente!",
            Options = new List<DialogOption>
            {
                new() { Text = "Abrir Forja de Refino", Action = "open_refine", ActionData = "" },
                new() { Text = "Sair", Action = "close" },
            },
        };

    }

    private void RegisterShops()
    {
        _shops["general_merchant_shop"] = new List<ShopEntry>
        {
            new() { ItemId = ItemDefinitions.PocaoVida, Price = 10, Stock = -1 },
            new() { ItemId = ItemDefinitions.PocaoMana, Price = 10, Stock = -1 },
        };
    }

    public void RegisterTemplate(NpcTemplate template)
    {
        _templates[template.PrefabId] = template;
    }

    public NpcTemplate? GetTemplate(string prefabId)
    {
        _templates.TryGetValue(prefabId, out var template);
        return template;
    }

    public DialogNode? GetDialog(string dialogId)
    {
        _dialogs.TryGetValue(dialogId, out var dialog);
        return dialog;
    }

    public List<ShopEntry>? GetShop(string shopId)
    {
        _shops.TryGetValue(shopId, out var shop);
        return shop;
    }

    public List<NpcSpawnPoint> GetSpawnPoints() => _spawnPoints;

    public void ConfigureSpawnPoints(IEnumerable<NpcSpawnPoint> spawnPoints)
    {
        _spawnPoints.Clear();
        foreach (var point in spawnPoints)
        {
            if (string.IsNullOrWhiteSpace(point.PrefabId))
                continue;

            _spawnPoints.Add(new NpcSpawnPoint
            {
                X = point.X,
                Y = point.Y,
                PrefabId = point.PrefabId,
            });
        }
    }

    public NPCEntity? CreateNpc(NpcSpawnPoint point)
    {
        var template = GetTemplate(point.PrefabId);
        if (template == null) return null;

        return new NPCEntity
        {
            PrefabId = template.PrefabId,
            Name = template.Name,
            DialogId = template.DialogId,
            ShopId = template.ShopId,
            Race = template.Race,
            AnimPrefix = template.AnimPrefix,
            FactionId = template.FactionId,
            X = point.X,
            Y = point.Y,
        };
    }

    public NPCEntity? CreateNpcAt(NpcTemplate template, float x, float y)
    {
        return new NPCEntity
        {
            PrefabId = template.PrefabId,
            Name = template.Name,
            DialogId = template.DialogId,
            ShopId = template.ShopId,
            Race = template.Race,
            AnimPrefix = template.AnimPrefix,
            FactionId = template.FactionId,
            X = x,
            Y = y,
        };
    }
}

public class NpcSpawnPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public string PrefabId { get; set; } = "";
}
