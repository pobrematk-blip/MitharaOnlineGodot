namespace Mithara.Web.Models;

public class GuildInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int Xp { get; set; }
    public int MemberCount { get; set; }
    public string LeaderName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
