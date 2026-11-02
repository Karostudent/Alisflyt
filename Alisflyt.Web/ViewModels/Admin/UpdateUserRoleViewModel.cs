using System.ComponentModel.DataAnnotations;

namespace Alisflyt.Web.ViewModels.Admin;

public sealed class UpdateUserRoleViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}
