using System.Collections.Generic;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.ViewModels.Dashboards
{
    public sealed class AlisDashboardViewModel
    {
        public IEnumerable<GrantCaseListItemViewModel> Cases { get; set; } = new GrantCaseListItemViewModel[0];

        public int DraftCount { get; set; }
        public int SubmittedCount { get; set; }
    }
}
