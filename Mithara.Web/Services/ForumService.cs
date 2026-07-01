using Microsoft.EntityFrameworkCore;
using Mithara.Web.Data;
using Mithara.Web.Models.Forum;

namespace Mithara.Web.Services;

public class ForumService
{
    private readonly WebDbContext _db;
    private readonly GameDbService _gameDb;

    public ForumService(WebDbContext db, GameDbService gameDb)
    {
        _db = db;
        _gameDb = gameDb;
    }

    public async Task<List<ForumCategory>> GetCategoriesAsync()
    {
        return await _db.ForumCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ForumCategory
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                SortOrder = c.SortOrder,
                CreatedAt = c.CreatedAt,
                Topics = c.Topics.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastPostAt ?? t.CreatedAt).Take(1).ToList(),
            })
            .ToListAsync();
    }

    public async Task<ForumCategory?> GetCategoryAsync(int categoryId)
    {
        return await _db.ForumCategories
            .Include(c => c.Topics.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastPostAt ?? t.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive);
    }

    public async Task<int> GetTopicCountAsync(int categoryId)
    {
        return await _db.ForumTopics.CountAsync(t => t.CategoryId == categoryId);
    }

    public async Task<List<ForumTopic>> GetTopicsAsync(int categoryId, int page, int pageSize = 20)
    {
        return await _db.ForumTopics
            .Where(t => t.CategoryId == categoryId)
            .OrderByDescending(t => t.IsPinned)
            .ThenByDescending(t => t.LastPostAt ?? t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<ForumTopic?> GetTopicAsync(int topicId)
    {
        return await _db.ForumTopics
            .Include(t => t.Posts.OrderBy(p => p.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == topicId);
    }

    public async Task<int> GetPostCountAsync(int topicId)
    {
        return await _db.ForumPosts.CountAsync(p => p.TopicId == topicId);
    }

    public async Task<List<ForumPost>> GetPostsAsync(int topicId, int page, int pageSize = 20)
    {
        return await _db.ForumPosts
            .Where(p => p.TopicId == topicId)
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<ForumTopic> CreateTopicAsync(int categoryId, string title, string content, int accountId, string authorName)
    {
        var topic = new ForumTopic
        {
            CategoryId = categoryId,
            Title = title,
            AccountId = accountId,
            AuthorName = authorName,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ForumTopics.Add(topic);
        await _db.SaveChangesAsync();

        var post = new ForumPost
        {
            TopicId = topic.Id,
            AccountId = accountId,
            AuthorName = authorName,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ForumPosts.Add(post);
        topic.LastPostAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return topic;
    }

    public async Task<ForumPost> CreatePostAsync(int topicId, string content, int accountId, string authorName)
    {
        var topic = await _db.ForumTopics.FindAsync(topicId);
        if (topic == null || topic.IsLocked)
            throw new InvalidOperationException("T\u00F3pico n\u00E3o encontrado ou bloqueado.");

        var post = new ForumPost
        {
            TopicId = topicId,
            AccountId = accountId,
            AuthorName = authorName,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ForumPosts.Add(post);
        topic.LastPostAt = DateTime.UtcNow;
        topic.ViewCount = Math.Max(0, topic.ViewCount);
        await _db.SaveChangesAsync();

        return post;
    }

    public async Task IncrementViewCountAsync(int topicId)
    {
        var topic = await _db.ForumTopics.FindAsync(topicId);
        if (topic != null)
        {
            topic.ViewCount++;
            await _db.SaveChangesAsync();
        }
    }

    public async Task EnsureDefaultCategoriesAsync()
    {
        if (!await _db.ForumCategories.AnyAsync())
        {
            _db.ForumCategories.AddRange(
                new ForumCategory { Name = "Not\u00EDcias e Atualiza\u00E7\u00F5es", Description = "Acompanhe as novidades do Mithara Online", SortOrder = 0 },
                new ForumCategory { Name = "Discuss\u00E3o Geral", Description = "Converse sobre o jogo com a comunidade", SortOrder = 1 },
                new ForumCategory { Name = "D\u00FAvidas e Suporte", Description = "Tire d\u00FAvidas sobre o jogo", SortOrder = 2 },
                new ForumCategory { Name = "Guildas e Cl\u00E3s", Description = "Divulgue sua guilda e recrute membros", SortOrder = 3 },
                new ForumCategory { Name = "Sugest\u00F5es e Feedback", Description = "Envie suas ideias para melhorar o jogo", SortOrder = 4 },
                new ForumCategory { Name = "Off-Topic", Description = "Assuntos fora do jogo", SortOrder = 5 }
            );
            await _db.SaveChangesAsync();
        }
    }
}
