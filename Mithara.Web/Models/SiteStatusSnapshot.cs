using System.Collections.Generic;

namespace Mithara.Web.Models;

public class SiteStatusSnapshot
{
    public int OnlinePlayers { get; set; }
    public List<string> OnlinePlayerNames { get; set; } = new();
    public int TotalAccounts { get; set; }
    public int TotalCharacters { get; set; }
    public int TotalGuilds { get; set; }
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
}
