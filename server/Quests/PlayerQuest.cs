namespace Mithara.Server.Quests;

public class PlayerQuest
{
    public int QuestId { get; set; }
    public List<int> Progress { get; set; } = new();
    public bool Completed { get; set; }
    public bool Claimed { get; set; }

    public bool IsObjectiveComplete(int index)
    {
        return index >= 0 && index < Progress.Count;
    }
}
