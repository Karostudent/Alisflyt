namespace Alisflyt.Web.ViewModels.Home;

public class LandingViewModel
{
    public string? CurrentUserName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
