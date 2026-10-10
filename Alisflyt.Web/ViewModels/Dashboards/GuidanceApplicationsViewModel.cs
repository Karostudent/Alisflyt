using Alisflyt.Web.ViewModels.GrantCases;
namespace Alisflyt.Web.ViewModels.Dashboards;
public sealed class GuidanceApplicationsViewModel
{
    public IReadOnlyList<GrantCaseListItemViewModel> Cases { get; init; } = [];
    public string Filter { get; init; } = "All";
}
