using System.Collections.Generic;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.ViewModels.Dashboards
{
    public sealed class CoordinatorDashboardViewModel
    {
        public IEnumerable<GrantCaseListItemViewModel> Cases { get; set; } = new GrantCaseListItemViewModel[0];
        public string Filter { get; set; } = "All";
    }
}
