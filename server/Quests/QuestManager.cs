using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mithara.Server.Quests;

public class QuestManager
{
    private readonly Dictionary<int, QuestDefinition> _definitions = new();

    public QuestManager()
    {
        if (!TryLoadCatalog())
            RegisterQuests();
    }

    private bool TryLoadCatalog()
    {
        string? path = FindQuestCatalogPath();
        if (path == null)
            return false;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var catalog = JsonSerializer.Deserialize<QuestCatalogData>(File.ReadAllText(path), options);
            if (catalog?.Quests == null || catalog.Quests.Count == 0)
                return false;

            foreach (var quest in catalog.Quests)
            {
                if (quest.Id <= 0 || string.IsNullOrWhiteSpace(quest.Name))
                    continue;

                Register(new QuestDefinition
                {
                    Id = quest.Id,
                    Name = quest.Name,
                    Description = quest.Description,
                    QuestText = quest.QuestText,
                    RequiredLevel = quest.RequiredLevel <= 0 ? 1 : quest.RequiredLevel,
                    NpcScenePath = quest.NpcScenePath,
                    OfferDialog = quest.OfferDialog,
                    AcceptedDialog = quest.AcceptedDialog,
                    ProgressDialog = quest.ProgressDialog,
                    CompleteDialog = quest.CompleteDialog,
                    DeliveryDialog = quest.DeliveryDialog,
                    Objectives = quest.Objectives
                        .Where(o => !string.IsNullOrWhiteSpace(o.TargetId))
                        .Select(o => new QuestObjective
                        {
                            Type = ParseObjectiveType(o.Type),
                            TargetId = o.TargetId,
                            RequiredCount = o.RequiredCount <= 0 ? 1 : o.RequiredCount,
                            TargetX = o.TargetX,
                            TargetY = o.TargetY,
                            Radius = o.Radius <= 0 ? 48f : o.Radius,
                        })
                        .ToList(),
                    Reward = new QuestReward
                    {
                        Experience = quest.Reward?.Experience ?? 0,
                        Gold = quest.Reward?.Gold ?? 0,
                        Items = quest.Reward?.Items
                            .Where(i => i.ItemId > 0 && i.Quantity > 0)
                            .Select(i => (i.ItemId, i.Quantity))
                            .ToList() ?? new List<(int itemId, int quantity)>(),
                    },
                });
            }

            return _definitions.Count > 0;
        }
        catch
        {
            _definitions.Clear();
            return false;
        }
    }

    private static string? FindQuestCatalogPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "quests.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data", "quests.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "server", "data", "quests.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "server", "data", "quests.json")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static QuestObjectiveType ParseObjectiveType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "collect" or "coletar" => QuestObjectiveType.Collect,
            "talk" or "falar" => QuestObjectiveType.Talk,
            "dropitem" or "drop" or "loot" or "dropar" => QuestObjectiveType.DropItem,
            "reachlocation" or "reach" or "location" or "chegarlocal" or "local" => QuestObjectiveType.ReachLocation,
            "interactobject" or "interact" or "objeto" => QuestObjectiveType.InteractObject,
            "useitem" or "use" or "usaritem" => QuestObjectiveType.UseItem,
            "escort" or "escoltar" => QuestObjectiveType.Escort,
            "craft" or "fabricar" => QuestObjectiveType.Craft,
            _ => QuestObjectiveType.Kill,
        };
    }

    private void RegisterQuests()
    {
        Register(new QuestDefinition
        {
            Id = 1,
            Name = "Caça aos Slimes",
            Description = "Elimine 5 slimes na floresta.",
            RequiredLevel = 1,
            Objectives = new List<QuestObjective>
            {
                new() { Type = QuestObjectiveType.Kill, TargetId = "slime", RequiredCount = 5 },
            },
            Reward = new QuestReward
            {
                Experience = 35,
                Gold = 10,
            },
        });

        Register(new QuestDefinition
        {
            Id = 2,
            Name = "Lobo Solitário",
            Description = "Derrote 3 lobos e colete 2 peles de lobo.",
            RequiredLevel = 2,
            Objectives = new List<QuestObjective>
            {
                new() { Type = QuestObjectiveType.Kill, TargetId = "wolf", RequiredCount = 3 },
                new() { Type = QuestObjectiveType.Collect, TargetId = "wolf_fur", RequiredCount = 2 },
            },
            Reward = new QuestReward
            {
                Experience = 80,
                Gold = 25,
                Items = new List<(int, int)> { (3, 1) },
            },
        });

        Register(new QuestDefinition
        {
            Id = 3,
            Name = "Defensor da Floresta",
            Description = "Elimine 10 slimes e 5 lobos.",
            RequiredLevel = 3,
            Objectives = new List<QuestObjective>
            {
                new() { Type = QuestObjectiveType.Kill, TargetId = "slime", RequiredCount = 10 },
                new() { Type = QuestObjectiveType.Kill, TargetId = "wolf", RequiredCount = 5 },
            },
            Reward = new QuestReward
            {
                Experience = 180,
                Gold = 50,
            },
        });
    }

    public void Register(QuestDefinition definition)
    {
        _definitions[definition.Id] = definition;
    }

    public QuestDefinition? GetDefinition(int questId)
    {
        _definitions.TryGetValue(questId, out var def);
        return def;
    }

    public List<QuestDefinition> GetAllDefinitions()
    {
        return _definitions.Values.ToList();
    }

    public List<QuestDefinition> GetQuestsForLevel(int level)
    {
        return _definitions.Values.Where(q => q.RequiredLevel <= level).ToList();
    }

    public bool CheckCompletion(QuestDefinition definition, PlayerQuest progress)
    {
        if (progress.Completed) return true;

        for (int i = 0; i < definition.Objectives.Count; i++)
        {
            int current = i < progress.Progress.Count ? progress.Progress[i] : 0;
            if (current < definition.Objectives[i].RequiredCount)
                return false;
        }

        return true;
    }

    private sealed class QuestCatalogData
    {
        public int Version { get; set; }
        public List<QuestDefinitionData> Quests { get; set; } = new();
    }

    private sealed class QuestDefinitionData
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string QuestText { get; set; } = "";
        public int RequiredLevel { get; set; } = 1;
        public string NpcScenePath { get; set; } = "";
        public string OfferDialog { get; set; } = "";
        public string AcceptedDialog { get; set; } = "";
        public string ProgressDialog { get; set; } = "";
        public string CompleteDialog { get; set; } = "";
        public string DeliveryDialog { get; set; } = "";
        public List<QuestObjectiveData> Objectives { get; set; } = new();
        public QuestRewardData Reward { get; set; } = new();
    }

    private sealed class QuestObjectiveData
    {
        public string Type { get; set; } = "Kill";
        public string TargetId { get; set; } = "";
        public int RequiredCount { get; set; } = 1;
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float Radius { get; set; } = 48f;
    }

    private sealed class QuestRewardData
    {
        public long Experience { get; set; }
        public int Gold { get; set; }
        public List<QuestRewardItemData> Items { get; set; } = new();
    }

    private sealed class QuestRewardItemData
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
