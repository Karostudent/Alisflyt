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
            // Support filtering by status using enum names from domain
            if (status == "Submitted")
            {
                list = list.Where(c => c.Status == GrantCaseStatus.Submitted).ToList();
            }
            else if (status == "Draft")
            {
                list = list.Where(c => c.Status == GrantCaseStatus.Draft).ToList();
            }
            else if (status == "UnderReview")
            {
                list = list.Where(c => c.Status == GrantCaseStatus.UnderReview).ToList();
            }
            else if (status == "ReturnedForCorrection")
            {
                list = list.Where(c => c.Status == GrantCaseStatus.ReturnedForCorrection).ToList();
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
            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

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
                ,
                ReturnReason = d.ReturnReason,
                ReturnedAtUtc = d.ReturnedAtUtc
            };

            return View("~/Views/Coordinator/Details.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(System.Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _svc.StartReviewAsync(id, cancellationToken).ConfigureAwait(false);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException)
            {
                TempData["Error"] = "Saken kan ikke settes til behandling.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        public async Task<IActionResult> ReturnForCorrection(System.Guid id, CancellationToken cancellationToken)
        {
            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            if (d.Status != Domain.Enums.GrantCaseStatus.UnderReview)
            {
                TempData["Error"] = "Saken er ikke i behandling og kan ikke returneres.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vm = new ViewModels.Coordinator.ReturnForCorrectionViewModel
            {
                Id = d.Id,
                CaseNumber = d.CaseNumber
            };

            return View("~/Views/Coordinator/ReturnForCorrection.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnForCorrection(ViewModels.Coordinator.ReturnForCorrectionViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Coordinator/ReturnForCorrection.cshtml", model);

            var req = new Alisflyt.Application.Models.ReturnForCorrectionRequest { Reason = model.Reason };

            try
            {
                await _svc.ReturnForCorrectionAsync(model.Id, req, cancellationToken).ConfigureAwait(false);
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(string.Empty, "Ugyldig begrunnelse.");
                return View("~/Views/Coordinator/ReturnForCorrection.cshtml", model);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Saken kan ikke returneres i gjeldende tilstand.");
                return View("~/Views/Coordinator/ReturnForCorrection.cshtml", model);
            }
        }
    }
}
