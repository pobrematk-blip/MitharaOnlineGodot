namespace Mithara.Server.Quests;

public enum QuestObjectiveType : byte
{
    Kill = 0,
    Collect = 1,
    Talk = 2,
    DropItem = 3,
    ReachLocation = 4,
    InteractObject = 5,
    UseItem = 6,
    Escort = 7,
    Craft = 8,
}

public class QuestObjective
{
    public QuestObjectiveType Type { get; set; }
    public string TargetId { get; set; } = "";
    public int RequiredCount { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float Radius { get; set; } = 48f;
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
    public string QuestText { get; set; } = "";
    public int RequiredLevel { get; set; }
    public string NpcScenePath { get; set; } = "";
    public string OfferDialog { get; set; } = "";
    public string AcceptedDialog { get; set; } = "";
    public string ProgressDialog { get; set; } = "";
    public string CompleteDialog { get; set; } = "";
    public string DeliveryDialog { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = new();
    public QuestReward Reward { get; set; } = new();
}
