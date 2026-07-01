namespace Mithara.Web.Models;

public class AccountInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime VipExpiry { get; set; }
    public List<CharacterInfo> Characters { get; set; } = new();
}

public class CharacterInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public string Race { get; set; } = "";
    public int Level { get; set; }
    public long Xp { get; set; }
    public int Gold { get; set; }
    public int BankGold { get; set; }
    public string CurrentMap { get; set; } = "main";
}
