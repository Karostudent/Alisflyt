using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Application.Services;
using Alisflyt.Web.ViewModels.Dashboards;
using Alisflyt.Web.ViewModels.GrantCases;
using Alisflyt.Web.ViewModels.Coordinator;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Web.Controllers
{
    public class CoordinatorController : Controller
    {
        private readonly IGrantCaseApplicationService _svc;

        public CoordinatorController(IGrantCaseApplicationService svc)
        {
            _svc = svc;
        }

        public async Task<IActionResult> Index(string status = "All", CancellationToken cancellationToken = default)
        {
            var list = await _svc.ListAsync(cancellationToken).ConfigureAwait(false);

            // Only support All or Submitted filters for the coordinator dashboard
            if (status == "Submitted")
            {
                list = list.Where(c => c.Status == GrantCaseStatus.Submitted).ToList();
            }

            var vm = new CoordinatorDashboardViewModel
            {
                Cases = list.Select(c => new GrantCaseListItemViewModel
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                }).ToList(),
                Filter = status
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(System.Guid id, CancellationToken cancellationToken)
        {
            var d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (d is null)
                return NotFound();

            var vm = new CoordinatorCaseDetailsViewModel
            {
                Id = d.Id,
                CaseNumber = d.CaseNumber,
                HprNumber = d.HprNumber,
                EmploymentPercentage = d.EmploymentPercentage,
                EmploymentStartDate = d.EmploymentStartDate,
                EmploymentEndDate = d.EmploymentEndDate,
                Status = d.Status,
                CreatedAtUtc = d.CreatedAtUtc,
                LastModifiedAtUtc = d.LastModifiedAtUtc
            };

            return View("~/Views/Coordinator/Details.cshtml", vm);
        }
    }
}
