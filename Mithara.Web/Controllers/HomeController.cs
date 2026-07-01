using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class HomeController : Controller
{
    private readonly GameDbService _gameDb;
    private readonly IConfiguration _config;

    public HomeController(GameDbService gameDb, IConfiguration config)
    {
        _gameDb = gameDb;
        _config = config;
    }

    public IActionResult Index()
    {
        ViewBag.SiteName = _config["GameSettings:SiteName"];
        ViewBag.DownloadUrl = _config["GameSettings:DownloadUrl"];
        ViewBag.DiscordUrl = _config["GameSettings:DiscordUrl"];
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Features()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }
}
