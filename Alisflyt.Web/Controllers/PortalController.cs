using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Alisflyt.Web.ViewModels.Portals;

namespace Alisflyt.Web.Controllers
{
    [Authorize]
    public class PortalController : Controller
    {
        public IActionResult Index()
        {
            var vm = new PortalEntryViewModel
            {
                DemoNotice = "Dette er en demonstrasjon med testdata. Tilgang styres av ASP.NET Core Identity.",
                Roles = new[]
                {
                    new RoleItem { Label = "ALIS", Url = Url.Action("Index", "Alis") },
                    new RoleItem { Label = "Koordinator", Url = Url.Action("Index", "Coordinator") },
                    new RoleItem { Label = "Veileder", Url = Url.Action("Index", "Guidance") }
                }
            };

            return View(vm);
        }
    }
}
