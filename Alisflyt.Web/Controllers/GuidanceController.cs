using Microsoft.AspNetCore.Mvc;
using Alisflyt.Web.ViewModels.Dashboards;

namespace Alisflyt.Web.Controllers
{
    public class GuidanceController : Controller
    {
        public IActionResult Index()
        {
            var vm = new GuidanceDashboardViewModel
            {
                Message = "Veilederfunksjonalitet kommer snart. Dette er en test."
            };

            return View(vm);
        }

        // Info action removed: no view or routes reference Info.cshtml in the project.
    }
}
