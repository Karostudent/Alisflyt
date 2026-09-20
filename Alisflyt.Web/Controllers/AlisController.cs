using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Application.Services;
using Alisflyt.Web.ViewModels.Dashboards;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.Controllers
{
    public class AlisController : Controller
    {
        private readonly IGrantCaseApplicationService _svc;

        public AlisController(IGrantCaseApplicationService svc)
        {
            _svc = svc;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var listResult = await _svc.ListAsync(cancellationToken).ConfigureAwait(false);
            var list = listResult ?? new System.Collections.Generic.List<GrantCaseListItemDto>();
            var cases = (list ?? new System.Collections.Generic.List<GrantCaseListItemDto>()).Where(c => c != null).ToList();
            var vm = new AlisDashboardViewModel
            {
                Cases = cases.Select(c => new GrantCaseListItemViewModel
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                }).ToList()
            };

            // Calculate status counts from the already retrieved list
            vm.DraftCount = cases.Count(c => c != null && c.Status == GrantCaseStatus.Draft);
            vm.SubmittedCount = cases.Count(c => c != null && c.Status == GrantCaseStatus.Submitted);

            // Support optional status filter from query string (e.g., ?status=Submitted)
            var status = "All";
            var q = ControllerContext?.HttpContext?.Request?.Query;
            if (q != null && q.ContainsKey("status"))
            {
                status = q["status"].FirstOrDefault() ?? "All";
            }

            vm.Filter = status;

            if (status == "Submitted")
            {
                vm.Cases = vm.Cases.Where(c => c.Status == GrantCaseStatus.Submitted).ToList();
            }
            else if (status == "Draft")
            {
                vm.Cases = vm.Cases.Where(c => c.Status == GrantCaseStatus.Draft).ToList();
            }
            else if (status == "UnderReview")
            {
                vm.Cases = vm.Cases.Where(c => c.Status == GrantCaseStatus.UnderReview).ToList();
            }
            else if (status == "ReturnedForCorrection")
            {
                vm.Cases = vm.Cases.Where(c => c.Status == GrantCaseStatus.ReturnedForCorrection).ToList();
            }

            return View(vm);
        }

        // ALIS uses the existing GrantCases Details view via GrantCasesController; keep AlisController focused on index only.
    }
}
