using System.ComponentModel.DataAnnotations;

namespace Mithara.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Usu\u00E1rio obrigat\u00F3rio")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Senha obrigat\u00F3ria")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}
