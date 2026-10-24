using Alisflyt.Web.Models;

namespace Alisflyt.Web.ViewModels.Home;

public class LandingViewModel
{
    public DemoUser? CurrentUser { get; set; }
    public IReadOnlyList<DemoUser> DemoUsers { get; set; } = Array.Empty<DemoUser>();
}
