using System.ComponentModel.DataAnnotations;

namespace Mithara.Web.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Usu\u00E1rio obrigat\u00F3rio")]
    [MinLength(3, ErrorMessage = "M\u00EDnimo 3 caracteres")]
    [MaxLength(20, ErrorMessage = "M\u00E1ximo 20 caracteres")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Apenas letras, n\u00FAmeros e _")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Email obrigat\u00F3rio")]
    [EmailAddress(ErrorMessage = "Email inv\u00E1lido")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Senha obrigat\u00F3ria")]
    [MinLength(6, ErrorMessage = "M\u00EDnimo 6 caracteres")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirme a senha")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Senhas n\u00E3o conferem")]
    public string ConfirmPassword { get; set; } = "";
}
