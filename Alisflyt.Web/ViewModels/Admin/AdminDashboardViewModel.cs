namespace Alisflyt.Web.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public string? CurrentUserName { get; set; }
    public IReadOnlyList<AdminUserViewModel> Users { get; set; } = Array.Empty<AdminUserViewModel>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
