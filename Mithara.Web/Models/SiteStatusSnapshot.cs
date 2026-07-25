namespace Mithara.Web.Models;

public class SiteStatusSnapshot
{
    public int OnlinePlayers { get; set; }
    public int TotalAccounts { get; set; }
    public int TotalCharacters { get; set; }
    public int TotalGuilds { get; set; }
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}
