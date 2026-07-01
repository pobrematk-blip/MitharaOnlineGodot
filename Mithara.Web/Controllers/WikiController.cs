using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class WikiController : Controller
{
    private readonly WikiService _wikiService;

    public WikiController(WikiService wikiService)
    {
        _wikiService = wikiService;
    }

    public async Task<IActionResult> Index(string search = "")
    {
        if (!string.IsNullOrEmpty(search))
        {
            var (items, mobs) = await _wikiService.SearchAsync(search);
            return View(new WikiIndexViewModel
            {
                SearchQuery = search,
                SearchResults = items.Concat(mobs).ToList(),
            });
        }

        var totalItems = await _wikiService.CountItemsAsync();
        var totalMobs = await _wikiService.CountMobsAsync();
        var recentMobs = await _wikiService.GetMobsAsync("", 1, 6);

        return View(new WikiIndexViewModel
        {
            TotalItems = totalItems,
            TotalMobs = totalMobs,
            RecentMobs = recentMobs,
        });
    }

    public async Task<IActionResult> Items(string search = "", string type = "", int page = 1)
    {
        var items = await _wikiService.SearchItemsAsync(search, type, page);
        var total = await _wikiService.CountItemsAsync(search, type);
        var types = await _wikiService.GetItemTypesAsync();
        var pageSize = 30;

        var typeNames = new Dictionary<string, string>();
        foreach (var t in types)
        {
            var id = int.Parse(t);
            typeNames[t] = id switch
            {
                1 => "Capacetes",
                2 => "Peitorais",
                3 => "Cintos",
                4 => "Luvas",
                5 => "Calças",
                6 => "Botas",
                7 => "Armas",
                8 => "Escudos",
                9 => "Colares",
                10 => "Anéis",
                11 => "Brincos",
                50 => "Consumíveis",
                51 => "Materiais",
                _ => "Outros",
            };
        }

        ViewBag.TypeNames = typeNames;

        return View(new WikiItemsViewModel
        {
            Items = items,
            Search = search,
            TypeFilter = type,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
            ItemTypes = types,
        });
    }

    public async Task<IActionResult> Item(int id)
    {
        var detail = await _wikiService.GetItemDetailAsync(id);
        if (detail == null)
            return NotFound();

        return View(detail);
    }

    public async Task<IActionResult> Mobs(string search = "", int page = 1)
    {
        var mobs = await _wikiService.GetMobsAsync(search, page);
        var total = await _wikiService.CountMobsAsync(search);
        var pageSize = 30;

        return View(new WikiItemsViewModel
        {
            Items = mobs.Select(m => new ItemSummary
            {
                Id = m.Id,
                Name = m.Name,
                TypeName = $"Nível {m.Level}",
                RequiredLevel = m.Level,
                IsElite = m.IsElite,
            }).ToList(),
            Search = search,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)total / pageSize),
        });
    }

    public async Task<IActionResult> Mob(int id)
    {
        var detail = await _wikiService.GetMobDetailAsync(id);
        if (detail == null)
            return NotFound();

        return View(detail);
    }
}
