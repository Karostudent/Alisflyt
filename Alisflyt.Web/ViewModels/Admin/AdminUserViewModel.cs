namespace Alisflyt.Web.ViewModels.Admin;

public sealed class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? HprNumber { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
