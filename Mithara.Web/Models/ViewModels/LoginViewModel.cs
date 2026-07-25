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

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Informe seu usuario ou e-mail")]
    public string UsernameOrEmail { get; set; } = "";
}

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Senha obrigatoria")]
    [MinLength(6, ErrorMessage = "Minimo 6 caracteres")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirme a senha")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Senhas nao conferem")]
    public string ConfirmPassword { get; set; } = "";
}
