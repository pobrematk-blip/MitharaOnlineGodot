using Mithara.Web.Models.Forum;

namespace Mithara.Web.Models.ViewModels;

public class ForumIndexViewModel
{
    public List<ForumCategory> Categories { get; set; } = new();
}

public class ForumCategoryViewModel
{
    public ForumCategory Category { get; set; } = new();
    public List<ForumTopic> Topics { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}

public class ForumTopicViewModel
{
    public ForumTopic Topic { get; set; } = new();
    public List<ForumPost> Posts { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}

public class CreateTopicViewModel
{
    public int CategoryId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string Title { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    public string Content { get; set; } = "";
}

public class CreatePostViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Content { get; set; } = "";
}
