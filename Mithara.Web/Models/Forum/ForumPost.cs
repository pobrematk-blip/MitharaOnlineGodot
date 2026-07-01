using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mithara.Web.Models.Forum;

public class ForumPost
{
    [Key]
    public int Id { get; set; }

    public int TopicId { get; set; }

    public int AccountId { get; set; }

    [MaxLength(50)]
    public string AuthorName { get; set; } = "";

    [Required]
    public string Content { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EditedAt { get; set; }

    [ForeignKey(nameof(TopicId))]
    public ForumTopic? Topic { get; set; }
}
