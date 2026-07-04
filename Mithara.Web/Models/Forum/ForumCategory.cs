using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mithara.Web.Models.Forum;

public class ForumCategory
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string Description { get; set; } = "";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int TopicCount { get; set; }

    public List<ForumTopic> Topics { get; set; } = new();
}
