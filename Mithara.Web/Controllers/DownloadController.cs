using Microsoft.AspNetCore.Mvc;

namespace Mithara.Web.Controllers;

public class DownloadController : Controller
{
    private readonly IConfiguration _config;

    public DownloadController(IConfiguration config)
    {
        _config = config;
    }

    public IActionResult Index()
    {
        ViewBag.DownloadUrl = _config["GameSettings:DownloadUrl"];
        ViewBag.DiscordUrl = _config["GameSettings:DiscordUrl"];
        return View();
    }
}
