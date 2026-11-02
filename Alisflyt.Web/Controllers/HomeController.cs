using Alisflyt.Web.ViewModels.Home;
using Alisflyt.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;

namespace Alisflyt.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var model = new LandingViewModel
            {
                CurrentUserName = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null,
                Roles = User.Claims
                    .Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(claim => claim.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };

            return View(model);
        }

        public IActionResult OmAlisflyt()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
