using Alisflyt.Web.Models;

namespace Alisflyt.Web.ViewModels.Admin;

public class AdminDashboardViewModel
{
    public DemoUser? CurrentUser { get; set; }
    public IReadOnlyList<Alisflyt.Application.Models.GrantCaseListItemDto> Cases { get; set; } = Array.Empty<Alisflyt.Application.Models.GrantCaseListItemDto>();
    public IReadOnlyList<Alisflyt.Domain.Entities.GrantRateSet> RateSets { get; set; } = Array.Empty<Alisflyt.Domain.Entities.GrantRateSet>();
}
