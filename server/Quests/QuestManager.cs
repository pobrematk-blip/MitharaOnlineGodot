using System.Collections.Generic;
using System.Linq;

namespace Mithara.Server.Quests;

public class QuestManager
{
    private readonly Dictionary<int, QuestDefinition> _definitions = new();

    public QuestManager()
    {
        RegisterQuests();
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
}
