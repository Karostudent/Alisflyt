using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        private readonly ILogger<CoordinatorController> _logger;

        private bool IsCoordinator() => Alisflyt.Web.Models.DemoAuthentication.IsInRole(HttpContext, "Koordinator");

        public CoordinatorController(
            IGrantCaseApplicationService svc,
            ILogger<CoordinatorController>? logger = null)
        {
            _svc = svc;
            _logger = logger ?? NullLogger<CoordinatorController>.Instance;
        }

        public async Task<IActionResult> Index(string status = "All", CancellationToken cancellationToken = default)
        {
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

            var list = await _svc.ListOverviewAsync(cancellationToken).ConfigureAwait(false);

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
            else if (status == "AwaitingAuditorApproval")
            {
                list = list.Where(c => c.Status == GrantCaseStatus.AwaitingAuditorApproval).ToList();
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
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

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
                LastModifiedAtUtc = d.LastModifiedAtUtc,
                ReadyForAuditorApprovalAtUtc = d.ReadyForAuditorApprovalAtUtc,
                ReturnReason = d.ReturnReason,
                ReturnedAtUtc = d.ReturnedAtUtc,
                    ApplicationData = d.ApplicationData,
                    GuidanceApprovalRequired = d.GuidanceApprovalRequired,
                    GuidanceApprovalApproved = d.GuidanceApprovalApproved,
                    GuidanceApprovalStatus = d.GuidanceApprovalStatus,
                    GuidanceCorrectionReason = d.GuidanceCorrectionReason
                };

            try
            {
                var calcSvc = HttpContext?.RequestServices.GetService(typeof(Alisflyt.Application.Services.IGrantCalculationApplicationService)) as Alisflyt.Application.Services.IGrantCalculationApplicationService;
                if (calcSvc is not null)
                {
                    var calc = await calcSvc.CalculateForCaseAsync(d.Id, cancellationToken).ConfigureAwait(false);
                    vm = new CoordinatorCaseDetailsViewModel
                    {
                        Id = vm.Id,
                        CaseNumber = vm.CaseNumber,
                        HprNumber = vm.HprNumber,
                        EmploymentPercentage = vm.EmploymentPercentage,
                        EmploymentStartDate = vm.EmploymentStartDate,
                        EmploymentEndDate = vm.EmploymentEndDate,
                        Status = vm.Status,
                        CreatedAtUtc = vm.CreatedAtUtc,
                        LastModifiedAtUtc = vm.LastModifiedAtUtc,
                        ReadyForAuditorApprovalAtUtc = vm.ReadyForAuditorApprovalAtUtc,
                        ReturnReason = vm.ReturnReason,
                            ReturnedAtUtc = vm.ReturnedAtUtc,
                            ApplicationData = vm.ApplicationData,
                            GuidanceApprovalRequired = vm.GuidanceApprovalRequired,
                            GuidanceApprovalApproved = vm.GuidanceApprovalApproved,
                            GuidanceApprovalStatus = vm.GuidanceApprovalStatus,
                            GuidanceCorrectionReason = vm.GuidanceCorrectionReason,
                            Calculation = calc
                        };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Calculation failed in {Action} for case {CaseId}.", nameof(Details), d.Id);
                ViewData["CalculationError"] = "Beregningen kunne ikke lastes på grunn av en teknisk feil.";
            }

            return View("~/Views/Coordinator/Details.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReadyForAuditorApproval(System.Guid id, CancellationToken cancellationToken)
        {
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

            try
            {
                await _svc.MarkReadyForAuditorApprovalAsync(id, cancellationToken).ConfigureAwait(false);
                TempData["Success"] = "Søknaden er klar for revisorkontroll.";
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException)
            {
                TempData["Error"] = "Saken kan ikke klargjøres for revisorkontroll før veiledningen er godkjent av veileder.";
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex, "Database save failed in {Action} for case {CaseId}.", nameof(MarkReadyForAuditorApproval), id);
                TempData["Error"] = "Endringen kunne ikke lagres. Prøv igjen.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(System.Guid id,
            [FromServices] IGrantCalculationApplicationService calculationService,
            [FromServices] TimeProvider timeProvider, CancellationToken cancellationToken)
        {
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

            Alisflyt.Application.Models.GrantCaseDto application;
            try
            {
                application = await _svc.GetByIdAsync(id, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            if (application.Status is not (GrantCaseStatus.UnderReview or GrantCaseStatus.AwaitingAuditorApproval))
                return BadRequest("Eksport er bare tilgjengelig når saken er under behandling eller klar for revisorkontroll.");

            var calculation = await calculationService.CalculateForCaseAsync(id, cancellationToken);
            var bytes = Alisflyt.Web.Exports.GrantCaseExcelExport.Create(application, calculation, timeProvider.GetUtcNow());
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Soknad-{application.Id}.xlsx");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCentrality(System.Guid id, bool? grade6, decimal? amount,
            [FromServices] Alisflyt.Application.Abstractions.IGrantCaseRepository repository,
            [FromServices] TimeProvider timeProvider, CancellationToken cancellationToken)
        {
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

            try
            {
                if (!grade6.HasValue)
                {
                    SetCentralityValidationState(grade6, amount, "Velg Ja eller Nei for sentralitetsgrad 6.", null);
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (!ModelState.IsValid)
                {
                    SetCentralityValidationState(grade6, amount, "Oppgi et gyldig beløp.", null);
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (grade6.Value && !amount.HasValue)
                {
                    SetCentralityValidationState(grade6, amount, "Oppgi søkt beløp når sentralitetsgrad 6 er Ja.", "Beløpet er påkrevd når sentralitetsgrad 6 er Ja.");
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (amount < 0)
                {
                    SetCentralityValidationState(grade6, amount, "Beløpet kan ikke være negativt.", "Beløpet kan ikke være negativt.");
                    return RedirectToAction(nameof(Details), new { id });
                }

                var grantCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
                grantCase.UpdateCentrality(grade6.Value, amount, timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex, "Database save failed in {Action} for case {CaseId}.", nameof(SaveCentrality), id);
                TempData["Error"] = "Endringen kunne ikke lagres. Prøv igjen.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(System.Guid id, CancellationToken cancellationToken)
        {
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

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
                TempData["Error"] = "Saken kan bare settes til behandling når den er sendt inn av ALIS.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex, "Database save failed in {Action} for case {CaseId}.", nameof(StartReview), id);
                TempData["Error"] = "Endringen kunne ikke lagres. Prøv igjen.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        public async Task<IActionResult> ReturnForCorrection(System.Guid id, CancellationToken cancellationToken)
        {
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

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
                TempData["Error"] = "Saken kan bare returneres for korrigering mens den er under behandling.";
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
            if (!IsCoordinator())
                return StatusCode(StatusCodes.Status403Forbidden);

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
                ModelState.AddModelError(nameof(model.Reason), GetReturnReasonError(model.Reason));
                return View("~/Views/Coordinator/ReturnForCorrection.cshtml", model);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Saken kan bare returneres for korrigering mens den er under behandling.");
                return View("~/Views/Coordinator/ReturnForCorrection.cshtml", model);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex, "Database save failed in {Action} for case {CaseId}.", nameof(ReturnForCorrection), model.Id);
                ModelState.AddModelError(string.Empty, "Endringen kunne ikke lagres. Prøv igjen.");
                return View("~/Views/Coordinator/ReturnForCorrection.cshtml", model);
            }
        }

        private void SetCentralityValidationState(bool? grade6, decimal? amount, string summary, string? amountError)
        {
            TempData["CentralityGrade6"] = grade6?.ToString()?.ToLowerInvariant();
            TempData["CentralityAmount"] = amount?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            TempData["CentralityGrade6Error"] = grade6.HasValue ? null : summary;
            TempData["CentralityAmountError"] = amountError;
        }

        private static string GetReturnReasonError(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "Skriv en begrunnelse før saken returneres.";
            return reason.Trim().Length > 1000
                ? "Begrunnelsen kan være på maksimalt 1000 tegn."
                : "Ugyldig begrunnelse.";
        }
    }
}
