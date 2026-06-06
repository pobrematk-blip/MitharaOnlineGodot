namespace Mithara.Server.Quests;

public enum QuestObjectiveType : byte
{
    Kill = 0,
    Collect = 1,
    Talk = 2,
}

public class QuestObjective
{
    public QuestObjectiveType Type { get; set; }
    public string TargetId { get; set; } = "";
    public int RequiredCount { get; set; }
}

public class QuestReward
{
    public long Experience { get; set; }
    public int Gold { get; set; }
    public List<(int itemId, int quantity)> Items { get; set; } = new();
}

public class QuestDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int RequiredLevel { get; set; }
    public List<QuestObjective> Objectives { get; set; } = new();
    public QuestReward Reward { get; set; } = new();
}
