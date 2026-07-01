using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mithara.Web.Models.Forum;

public class ForumTopic
{
    [Key]
    public int Id { get; set; }

    public int CategoryId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    public int AccountId { get; set; }

    [MaxLength(50)]
    public string AuthorName { get; set; } = "";

    public bool IsPinned { get; set; }

    public bool IsLocked { get; set; }

    public int ViewCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastPostAt { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public ForumCategory? Category { get; set; }

    public List<ForumPost> Posts { get; set; } = new();
}
