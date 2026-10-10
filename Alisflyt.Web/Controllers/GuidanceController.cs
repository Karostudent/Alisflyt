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

        [HttpGet]
        public async Task<IActionResult> Applications([FromServices] Alisflyt.Application.Services.IGrantCaseApplicationService service,
            string status = "All", CancellationToken cancellationToken = default)
        {
            var cases = await service.ListAsync(cancellationToken);
            if (!new[] { "All", "Draft", "Submitted", "UnderReview", "ReturnedForCorrection" }.Contains(status)) status = "All";
            return View(new GuidanceApplicationsViewModel
            {
                Filter = status,
                Cases = cases.Where(c => status == "All" || c.Status.ToString() == status)
                    .OrderByDescending(c => c.LastModifiedAtUtc)
                    .Select(c => new Alisflyt.Web.ViewModels.GrantCases.GrantCaseListItemViewModel
                    {
                        Id = c.Id, CaseNumber = c.CaseNumber, HprNumber = c.HprNumber,
                        Status = c.Status, LastModifiedAtUtc = c.LastModifiedAtUtc
                    }).ToList()
            });
        }

        // Info action removed: no view or routes reference Info.cshtml in the project.
    }
}
