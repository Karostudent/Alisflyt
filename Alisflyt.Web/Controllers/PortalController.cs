using Microsoft.AspNetCore.Mvc;
using Alisflyt.Web.ViewModels.Portals;

namespace Alisflyt.Web.Controllers
{
    public class PortalController : Controller
    {
        public IActionResult Index()
        {
            var vm = new PortalEntryViewModel
            {
                DemoNotice = "DEMOVERSJON: Dette er en demonstrasjon. Ingen reell autentisering er implementert.",
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
