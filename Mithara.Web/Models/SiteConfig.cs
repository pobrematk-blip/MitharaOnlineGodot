using System.ComponentModel.DataAnnotations;

namespace Mithara.Web.Models;

public class SiteConfig
{
    [Key, MaxLength(100)]
    public string Key { get; set; } = "";

    public string Value { get; set; } = "";
}
