namespace Mithara.Web.Models.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalAccounts { get; set; }
    public int TotalCharacters { get; set; }
    public int TotalGuilds { get; set; }
    public int TotalForumTopics { get; set; }
    public int TotalForumPosts { get; set; }
    public int OnlinePlayers { get; set; }
    public List<AdminLog> RecentLogs { get; set; } = new();
}

public class AdminUsersViewModel
{
    public List<AccountInfo> Accounts { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
