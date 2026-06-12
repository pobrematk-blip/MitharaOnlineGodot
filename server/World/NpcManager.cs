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
            PrefabId = "velho_sabio",
            Name = "Velho Sábio",
            DialogId = "velho_sabio_inicio",
            Race = "Cidadão",
            AnimPrefix = "mago",
            FactionId = "solari",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "mestre_guerreiro",
            Name = "Mestre Guerreiro",
            DialogId = "mestre_guerreiro_inicio",
            Race = "Humano",
            AnimPrefix = "berzerk",
            FactionId = "solari",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "mercador",
            Name = "Mercador Viajante",
            DialogId = "mercador_inicio",
            ShopId = "mercador_loja",
            Race = "Mercador Geral",
            AnimPrefix = "padrao",
            FactionId = "noctori",
        });

        RegisterTemplate(new NpcTemplate
        {
            PrefabId = "mestre_treino",
            Name = "Instrutor de Treino",
            DialogId = "mestre_treino_inicio",
            AnimPrefix = "ladino",
            FactionId = "solari",
        });

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

        _spawnPoints.Add(new NpcSpawnPoint { X = 920, Y = 950, PrefabId = "velho_sabio" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 980, Y = 940, PrefabId = "mestre_guerreiro" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 900, Y = 1050, PrefabId = "mercador" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1070, Y = 960, PrefabId = "mestre_treino" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1040, Y = 1050, PrefabId = "banqueiro" });
        _spawnPoints.Add(new NpcSpawnPoint { X = 1070, Y = 1050, PrefabId = "guarda_solareth" });
    }

    private void RegisterDialogs()
    {
        _dialogs["velho_sabio_inicio"] = new DialogNode
        {
            Id = "velho_sabio_inicio",
            Text = "Olá, jovem aventureiro! O mundo de Mithara precisa de heróis como você. " +
                   "Os monstros estão se multiplicando nas florestas e precisamos de ajuda.",
            Options = new List<DialogOption>
            {
                new() { Text = "Conte-me mais sobre Mithara", Action = "goto", ActionData = "velho_sabio_historia" },
                new() { Text = "Aceito ajudar! Alguma missão?", Action = "goto", ActionData = "velho_sabio_quest" },
                new() { Text = "Preciso treinar", Action = "goto", ActionData = "velho_sabio_treino" },
                new() { Text = "Adeus.", Action = "close" },
            },
        };

        _dialogs["velho_sabio_historia"] = new DialogNode
        {
            Id = "velho_sabio_historia",
            Text = "Mithara já foi uma terra próspera, mas há cem anos o Rei das Trevas " +
                   "lançou uma maldição sobre estas terras. Os elementos se corromperam e " +
                   "criaturas sombrias emergiram das profundezas. Dizem que apenas um " +
                   "herói de lenda pode restaurar a paz.",
            Options = new List<DialogOption>
            {
                new() { Text = "Fale mais sobre os monstros", Action = "goto", ActionData = "velho_sabio_monstros" },
                new() { Text = "Estou pronto para começar", Action = "goto", ActionData = "velho_sabio_quest" },
                new() { Text = "Adeus.", Action = "close" },
            },
        };

        _dialogs["velho_sabio_monstros"] = new DialogNode
        {
            Id = "velho_sabio_monstros",
            Text = "Os Slimes são as criaturas mais comuns - surgem da umidade corrupta. " +
                   "Os Goblins são mais perigosos, armados e organizados em tribos. " +
                   "Cuidado com os Lobos, caçam em bando. E nos lugares mais sombrios, " +
                   "Esqueletos ancestrais guardam tesouros esquecidos.",
            Options = new List<DialogOption>
            {
                new() { Text = "O que devo fazer primeiro?", Action = "goto", ActionData = "velho_sabio_quest" },
                new() { Text = "Preciso de equipamentos", Action = "goto", ActionData = "velho_sabio_treino" },
                new() { Text = "Adeus.", Action = "close" },
            },
        };

        _dialogs["velho_sabio_quest"] = new DialogNode
        {
            Id = "velho_sabio_quest",
            Text = "Claro! Vá até a clareira ao norte e elimine alguns Slimes. " +
                   "Eles são fracos mas numerosos. Volte quando tiver experiência suficiente! " +
                   "(Você pode ver suas missões na janela de Missões)",
            Options = new List<DialogOption>
            {
                new() { Text = "Vou agora mesmo!", Action = "close" },
                new() { Text = "Voltar", Action = "goto", ActionData = "velho_sabio_inicio" },
            },
        };

        _dialogs["velho_sabio_treino"] = new DialogNode
        {
            Id = "velho_sabio_treino",
            Text = "Treine com os Slimes e Goblins nas redondezas. Cada batalha o tornará " +
                   "mais forte. Use a tecla C para abrir seu inventário e equipar itens. " +
                   "Boa sorte, aventureiro!",
            Options = new List<DialogOption>
            {
                new() { Text = "Obrigado, sábio!", Action = "close" },
            },
        };

        _dialogs["mestre_guerreiro_inicio"] = new DialogNode
        {
            Id = "mestre_guerreiro_inicio",
            Text = "Saudações! Sou o Mestre Guerreiro. Se precisar de combate, " +
                   "estou aqui para ajudar. Mostre suas habilidades!",
            Options = new List<DialogOption>
            {
                new() { Text = "O que você vende?", Action = "shop", ActionData = "mestre_guerreiro_loja" },
                new() { Text = "Tenho uma missão para completar", Action = "goto", ActionData = "mestre_guerreiro_quest" },
                new() { Text = "Tchau.", Action = "close" },
            },
        };

        _dialogs["mestre_guerreiro_quest"] = new DialogNode
        {
            Id = "mestre_guerreiro_quest",
            Text = "Ótimo! Precisa de ajuda com alguma missão? " +
                   "Verifique sua lista de missões para ver o progresso.",
            Options = new List<DialogOption>
            {
                new() { Text = "Voltar", Action = "goto", ActionData = "mestre_guerreiro_inicio" },
                new() { Text = "Adeus.", Action = "close" },
            },
        };

        _dialogs["mercador_inicio"] = new DialogNode
        {
            Id = "mercador_inicio",
            Text = "Psst! Tenho mercadorias raras e úteis para aventureiros " +
                   "como você. Precisa de algo? Poções, equipamentos, eu tenho de tudo!",
            Options = new List<DialogOption>
            {
                new() { Text = "Ver mercadorias", Action = "shop", ActionData = "mercador_loja" },
                new() { Text = "Vender itens", Action = "goto", ActionData = "mercador_vender" },
                new() { Text = "Não agora.", Action = "close" },
            },
        };

        _dialogs["mercador_vender"] = new DialogNode
        {
            Id = "mercador_vender",
            Text = "Claro! Me mostre o que tem. Comprarei seus itens por " +
                   "um preço justo. (Abra seu inventário e use a opção de venda)",
            Options = new List<DialogOption>
            {
                new() { Text = "Voltar à loja", Action = "shop", ActionData = "mercador_loja" },
                new() { Text = "Voltar", Action = "goto", ActionData = "mercador_inicio" },
            },
        };

        _dialogs["mestre_treino_inicio"] = new DialogNode
        {
            Id = "mestre_treino_inicio",
            Text = "Olá! Sou o instrutor de treino. Aqui você pode praticar " +
                   "suas habilidades sem risco de morte. Use o boneco de treino " +
                   "ali perto para testar seus ataques e skills. " +
                   "Aperte F para interagir com objetos.",
            Options = new List<DialogOption>
            {
                new() { Text = "Como funcionam as skills?", Action = "goto", ActionData = "mestre_treino_skills" },
                new() { Text = "Obrigado!", Action = "close" },
            },
        };

        _dialogs["mestre_treino_skills"] = new DialogNode
        {
            Id = "mestre_treino_skills",
            Text = "Você pode aprender skills na Árvore de Talentos (tecla T). " +
                   "Cada classe tem habilidades únicas. Gaste seus pontos de talento " +
                   "com sabedoria! As skills são ativadas pelos slots na barra inferior.",
            Options = new List<DialogOption>
            {
                new() { Text = "Voltar", Action = "goto", ActionData = "mestre_treino_inicio" },
            },
        };

        _dialogs["banqueiro_inicio"] = new DialogNode
        {
            Id = "banqueiro_inicio",
            Text = "Bem-vindo ao Banco Real de Mithara! " +
                   "Aqui você pode guardar itens e depositar seu ouro com segurança. " +
                   "Pressione 'Abrir Banco' para acessar seu cofre.",
            Options = new List<DialogOption>
            {
                new() { Text = "Abrir Banco", Action = "bank", ActionData = "open" },
                new() { Text = "Sair", Action = "close" },
            },
        };

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
    }

    private void RegisterShops()
    {
        _shops["mercador_loja"] = new List<ShopEntry>
        {
            new() { ItemId = 1, Price = 10, Stock = 20 },
            new() { ItemId = 2, Price = 25, Stock = 10 },
            new() { ItemId = 10, Price = 100, Stock = 5 },
            new() { ItemId = 20, Price = 150, Stock = 3 },
        };

        _shops["mestre_guerreiro_loja"] = new List<ShopEntry>
        {
            new() { ItemId = 1, Price = 8, Stock = 30 },
            new() { ItemId = 2, Price = 20, Stock = 15 },
            new() { ItemId = 11, Price = 200, Stock = 2 },
            new() { ItemId = 24, Price = 500, Stock = 1 },
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
