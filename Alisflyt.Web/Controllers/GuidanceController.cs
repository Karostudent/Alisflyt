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
                Message = "Veilederfunksjonalitet kommer snart. Dette er en demonstrasjon."
            };

            return View(vm);
        }

        public IActionResult Info()
        {
            return View();
        }
    }
}
