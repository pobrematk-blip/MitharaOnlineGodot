using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Data;
using Mithara.Web.Models;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly GameDbService _gameDb;
    private readonly WebDbContext _webDb;
    private readonly IConfiguration _config;

    public AdminController(GameDbService gameDb, WebDbContext webDb, IConfiguration config)
    {
        _gameDb = gameDb;
        _webDb = webDb;
        _config = config;
    }

    private bool IsAdminUser()
    {
        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return _gameDb.IsAdmin(accountId);
    }

    public IActionResult Dashboard()
    {
        if (!IsAdminUser())
            return RedirectToAction("Index", "Home");

        var vm = new AdminDashboardViewModel
        {
            TotalAccounts = _gameDb.GetTotalAccounts(),
            TotalCharacters = _gameDb.GetTotalCharacters(),
            TotalGuilds = _gameDb.GetTotalGuilds(),
            TotalForumTopics = _webDb.ForumTopics.Count(),
            TotalForumPosts = _webDb.ForumPosts.Count(),
            OnlinePlayers = _gameDb.GetOnlinePlayerCount(),
            RecentLogs = _webDb.AdminLogs.OrderByDescending(l => l.CreatedAt).Take(20).ToList(),
        };

        return View(vm);
    }

    public IActionResult Users(int page = 1)
    {
        if (!IsAdminUser())
            return RedirectToAction("Index", "Home");

        int pageSize = 50;
        var accounts = _gameDb.GetAllAccounts(page, pageSize);
        var totalAccounts = _gameDb.GetTotalAccounts();

        return View(new AdminUsersViewModel
        {
            Accounts = accounts,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalAccounts / pageSize),
        });
    }

    public IActionResult Forum()
    {
        if (!IsAdminUser())
            return RedirectToAction("Index", "Home");

        var categories = _webDb.ForumCategories.OrderBy(c => c.SortOrder).ToList();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTopic(int topicId)
    {
        if (!IsAdminUser())
            return Json(new { success = false });

        var topic = await _webDb.ForumTopics.FindAsync(topicId);
        if (topic == null)
            return Json(new { success = false });

        _webDb.ForumTopics.Remove(topic);
        await _webDb.SaveChangesAsync();

        LogAdminAction("delete_topic", $"T\u00F3pico #{topicId} removido");
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTopicLock(int topicId)
    {
        if (!IsAdminUser())
            return Json(new { success = false });

        var topic = await _webDb.ForumTopics.FindAsync(topicId);
        if (topic == null)
            return Json(new { success = false });

        topic.IsLocked = !topic.IsLocked;
        await _webDb.SaveChangesAsync();

        LogAdminAction("toggle_lock", $"T\u00F3pico #{topicId} {(topic.IsLocked ? "bloqueado" : "destravado")}");
        return Json(new { success = true, locked = topic.IsLocked });
    }

    private void LogAdminAction(string action, string details)
    {
        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var username = User.Identity?.Name ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

        _webDb.AdminLogs.Add(new AdminLog
        {
            AccountId = accountId,
            Username = username,
            Action = action,
            Details = details,
            IpAddress = ip,
        });
        _webDb.SaveChanges();
    }
}
