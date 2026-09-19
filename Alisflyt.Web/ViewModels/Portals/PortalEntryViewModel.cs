using System.Collections.Generic;

namespace Alisflyt.Web.ViewModels.Portals
{
    public sealed class PortalEntryViewModel
    {
        public string DemoNotice { get; set; } = string.Empty;
        public IEnumerable<RoleItem> Roles { get; set; } = new RoleItem[0];
    }

    public sealed class RoleItem
    {
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }
    }
}
