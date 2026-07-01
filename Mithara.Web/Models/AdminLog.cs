using System.ComponentModel.DataAnnotations;

namespace Mithara.Web.Models;

public class AdminLog
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    [MaxLength(50)]
    public string Username { get; set; } = "";

    [MaxLength(50)]
    public string Action { get; set; } = "";

    public string Details { get; set; } = "";

    [MaxLength(50)]
    public string IpAddress { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
