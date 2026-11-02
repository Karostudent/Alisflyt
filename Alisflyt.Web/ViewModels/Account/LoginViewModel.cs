using System.ComponentModel.DataAnnotations;

namespace Alisflyt.Web.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required]
    [Display(Name = "Brukernavn eller e-post")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Husk meg")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
