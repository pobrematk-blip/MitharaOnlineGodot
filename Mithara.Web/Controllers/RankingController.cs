using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class RankingController : Controller
{
    private readonly GameDbService _gameDb;

    public RankingController(GameDbService gameDb)
    {
        _gameDb = gameDb;
    }

    public IActionResult Index()
    {
        var characters = _gameDb.GetCharacterRanking(100);
        var guilds = _gameDb.GetGuildRanking(100);
        ViewBag.Characters = characters;
        ViewBag.Guilds = guilds;
        return View();
    }
}
