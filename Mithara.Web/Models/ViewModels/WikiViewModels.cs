using Mithara.Web.Models;

namespace Mithara.Web.Models.ViewModels;

public class WikiIndexViewModel
{
    public int TotalItems { get; set; }
    public int TotalMobs { get; set; }
    public List<WikiMob> RecentMobs { get; set; } = new();
    public string SearchQuery { get; set; } = "";
    public List<SearchResult> SearchResults { get; set; } = new();
}

public class WikiItemsViewModel
{
    public List<ItemSummary> Items { get; set; } = new();
    public string Search { get; set; } = "";
    public string TypeFilter { get; set; } = "";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public List<string> ItemTypes { get; set; } = new();
}

public class WikiMobsViewModel
{
    public List<WikiMob> Mobs { get; set; } = new();
    public string Search { get; set; } = "";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
}

public class ItemSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string TypeName { get; set; } = "";
    public int RequiredLevel { get; set; }
    public bool IsElite { get; set; }
    public int BuyPrice { get; set; }
    public List<string> Classes { get; set; } = new();
    public string IconUrl { get; set; } = "";
    public string ClassDisplay => Classes.Count > 0 ? string.Join(", ", Classes) : "Todas as classes";
}

public class WikiItemDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public bool IsElite { get; set; }
    public int RequiredLevel { get; set; }
    public int BuyPrice { get; set; }
    public List<string> AllowedClasses { get; set; } = new();
    public string AffixPool { get; set; } = "";
    public List<ItemStatDisplay> Stats { get; set; } = new();
    public List<MobDropInfo> DroppedBy { get; set; } = new();
    public string IconUrl { get; set; } = "";
    public string ClassDisplay => AllowedClasses.Count > 0
        ? string.Join(", ", AllowedClasses)
        : "Todas as classes";
}

public class ItemStatDisplay
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public class MobDropInfo
{
    public int MobId { get; set; }
    public string MobName { get; set; } = "";
    public int MobLevel { get; set; }
    public string DropChanceDisplay { get; set; } = "";
    public double DropChance { get; set; }
}

public class SearchResult
{
    public string Type { get; set; } = ""; // "item" ou "mob"
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
}

public class WikiMobDetailViewModel
{
    public WikiMob Mob { get; set; } = new();
    public List<WikiDrop> Drops { get; set; } = new();
}
