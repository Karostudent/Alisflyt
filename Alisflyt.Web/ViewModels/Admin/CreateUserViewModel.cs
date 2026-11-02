using System.ComponentModel.DataAnnotations;

namespace Alisflyt.Web.ViewModels.Admin;

public sealed class CreateUserViewModel
{
    [Required]
    [Display(Name = "Brukernavn")]
    public string UserName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "E-post")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Passord")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Rolle")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Veilederens HPR-nummer")]
    public string? HprNumber { get; set; }
}
