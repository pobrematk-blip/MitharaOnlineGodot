using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Mithara.Web.Models.ViewModels;
using Mithara.Web.Services;

namespace Mithara.Web.Controllers;

public class ForumController : Controller
{
    private readonly ForumService _forumService;

    public ForumController(ForumService forumService)
    {
        _forumService = forumService;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _forumService.GetCategoriesAsync();
        return View(new ForumIndexViewModel { Categories = categories });
    }

    public async Task<IActionResult> Category(int id, int page = 1)
    {
        var category = await _forumService.GetCategoryAsync(id);
        if (category == null)
            return NotFound();

        int pageSize = 20;
        var totalTopics = await _forumService.GetTopicCountAsync(id);
        var topics = await _forumService.GetTopicsAsync(id, page, pageSize);

        return View(new ForumCategoryViewModel
        {
            Category = category,
            Topics = topics,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalTopics / pageSize),
        });
    }

    public async Task<IActionResult> Topic(int id, int page = 1)
    {
        var topic = await _forumService.GetTopicAsync(id);
        if (topic == null)
            return NotFound();

        await _forumService.IncrementViewCountAsync(id);

        int pageSize = 20;
        var totalPosts = await _forumService.GetPostCountAsync(id);
        var posts = await _forumService.GetPostsAsync(id, page, pageSize);

        return View(new ForumTopicViewModel
        {
            Topic = topic,
            Posts = posts,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalPosts / pageSize),
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTopic(CreateTopicViewModel model)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return RedirectToAction("Category", new { id = model.CategoryId });

        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var authorName = User.Identity.Name ?? "Desconhecido";

        var topic = await _forumService.CreateTopicAsync(model.CategoryId, model.Title, model.Content, accountId, authorName);
        return RedirectToAction("Topic", new { id = topic.Id });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePost(int topicId, CreatePostViewModel model)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return RedirectToAction("Topic", new { id = topicId });

        var accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var authorName = User.Identity.Name ?? "Desconhecido";

        try
        {
            await _forumService.CreatePostAsync(topicId, model.Content, accountId, authorName);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction("Topic", new { id = topicId });
    }
}
