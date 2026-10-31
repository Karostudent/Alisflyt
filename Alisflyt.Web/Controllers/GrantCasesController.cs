using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Services;
using Alisflyt.Application.Models;
using Alisflyt.Web.Models;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.Controllers
{
    public class GrantCasesController : Controller
    {
        private readonly IGrantCaseApplicationService _svc;
        private readonly IGrantCaseRepository? _repository;

        public GrantCasesController(IGrantCaseApplicationService svc, IGrantCaseRepository? repository = null)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _repository = repository;
        }

        private bool UserCanAccessCase(string? doctorHprNumber)
        {
            return DemoAuthentication.CanAccessCaseForCurrentUser(HttpContext, doctorHprNumber);
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var list = await _svc.ListAsync(cancellationToken).ConfigureAwait(false);
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (currentUser is not null && string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase))
            {
                list = list
                    .Where(c => string.Equals(c.HprNumber, currentUser.HprNumber, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else if (currentUser is not null && string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase))
            {
                var allowedDoctors = new HashSet<string>(DemoAuthentication.GetAssignedDoctorHprNumbers(currentUser.Key), StringComparer.OrdinalIgnoreCase);
                list = list
                    .Where(c => !string.IsNullOrWhiteSpace(c.HprNumber) && allowedDoctors.Contains(c.HprNumber))
                    .ToList();
            }

            var vm = list.Select(d => new GrantCaseListItemViewModel
            {
                Id = d.Id,
                CaseNumber = d.CaseNumber,
                HprNumber = d.HprNumber,
                Status = d.Status,
                LastModifiedAtUtc = d.LastModifiedAtUtc
            }).ToList();
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _svc.GetByIdAsync(id, cancellationToken);
                if (HttpContext is not null && !UserCanAccessCase(data.HprNumber))
                    return StatusCode(StatusCodes.Status403Forbidden);

                var attachment = data.ApplicationData?.Attachments.FirstOrDefault(a => a.Id == attachmentId);
                if (attachment is null) return NotFound();
                return File(Convert.FromBase64String(attachment.ContentBase64), "application/octet-stream", System.IO.Path.GetFileName(attachment.FileName));
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        [HttpGet]
        public IActionResult Create()
        {
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)))
                return StatusCode(StatusCodes.Status403Forbidden);

            var model = new GrantApplicationFormViewModel
            {
                GrantType = Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                HprNumber = currentUser?.HprNumber,
                DoctorName = currentUser?.Name
            };

            return View("Application", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(23_000_000)]
        public async Task<IActionResult> SaveApplication([FromBody] GrantApplicationFormViewModel model, CancellationToken cancellationToken)
        {
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)))
                return StatusCode(StatusCodes.Status403Forbidden);

            model ??= new GrantApplicationFormViewModel();
            model.Attachments ??= [];
            model.Certificate ??= new();
            model.Certificate.Sessions ??= [];
            model.EmploymentPeriods ??= [];
            model.SelectedPositionTypes ??= [];
            model.DoctorProfessions ??= [];

            if (currentUser is not null)
            {
                model.HprNumber ??= currentUser.HprNumber;
                model.DoctorName ??= currentUser.Name;
            }

            model.GrantType ??= Domain.Forms.GrantType.AlisAgreementIncludingSupervision;
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Kontroller tall og datoer før du lagrer." });
            model.GrantType = Domain.Forms.GrantType.AlisAgreementIncludingSupervision;
            try
            {
                var saved = model.Id == Guid.Empty
                    ? await _svc.CreateDraftAsync(new CreateGrantCaseRequest { ApplicationData = model }, cancellationToken)
                    : await _svc.UpdateDraftAsync(model.Id, new UpdateGrantCaseDraftRequest { ApplicationData = model }, cancellationToken);
                return Ok(new { id = saved.Id, message = "Utkastet er lagret.",
                    editUrl = Url.Action(nameof(Edit), new { id = saved.Id }),
                    detailsUrl = Url.Action(nameof(Details), new { id = saved.Id }) });
            }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Søknaden finnes ikke lenger." }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException) { return Conflict(new { message = "Søknaden kan ikke redigeres i nåværende status." }); }
        }

        private static GrantApplicationFormViewModel FormModel(GrantCaseDto data)
        {
            var form = data.ApplicationData ?? new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = data.HprNumber,
                EmploymentPeriods = data.EmploymentPercentage.HasValue || data.EmploymentStartDate.HasValue || data.EmploymentEndDate.HasValue
                    ? [new() { PositionPercentage = data.EmploymentPercentage, EmploymentStartDate = data.EmploymentStartDate,
                        FundingFrom = data.EmploymentStartDate, FundingThrough = data.EmploymentEndDate }]
                    : []
            };
            var model = System.Text.Json.JsonSerializer.Deserialize<GrantApplicationFormViewModel>(System.Text.Json.JsonSerializer.Serialize(form))!;
            model.Id = data.Id;
            return model;
        }

        [HttpGet]
        public async Task<IActionResult> Application(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _svc.GetByIdAsync(id, cancellationToken);
                if (HttpContext is not null && !UserCanAccessCase(data.HprNumber))
                    return StatusCode(StatusCodes.Status403Forbidden);

                var model = FormModel(data);
                model.ReadOnly = true;
                return View(model);
            }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(); }
        }


        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }

            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && currentUser is not null && string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(d.HprNumber, currentUser.HprNumber, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            if (HttpContext is not null && currentUser is not null && string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase)
                && !DemoAuthentication.IsCurrentSupervisorForDoctor(HttpContext, d.HprNumber))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var vm = new GrantCaseDetailsViewModel
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
                ApplicationData = d.ApplicationData
            };

            if (_repository is not null)
            {
                var domainCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                var currentApproval = domainCase?.GetCurrentGuidanceApproval();
                if (currentApproval is not null && currentApproval.Status == Domain.Enums.GuidanceApprovalStatus.NeedsCorrection)
                {
                    ViewData["GuidanceCorrectionStatus"] = "Korrigering nødvendig";
                    ViewData["GuidanceCorrectionReason"] = currentApproval.RequestedRevisionReason;
                    ViewData["GuidanceCorrectionLink"] = Url is null ? null : Url.Action(nameof(EditGuidance), new { id });
                }
            }

            // Try to fetch calculation (resolve per-request so ctor doesn't require it)
            try
            {
                var calcSvc = (Alisflyt.Application.Services.IGrantCalculationApplicationService?)HttpContext?.RequestServices.GetService(typeof(Alisflyt.Application.Services.IGrantCalculationApplicationService));
                if (calcSvc != null)
                {
                    var calc = await calcSvc.CalculateForCaseAsync(d.Id, cancellationToken).ConfigureAwait(false);
                    // Calculation property is init-only; create a new VM instance
                    vm = new GrantCaseDetailsViewModel
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
                        Calculation = calc
                    };
                }
            }
            catch
            {
                // Swallow to keep details view stable; calculation not critical
            }

            // Return explicit Details view so MVC does not search for a Submit.cshtml view.
            return View("Details", vm);
        }

        [HttpGet]
        public async Task<IActionResult> EditGuidance(Guid id, CancellationToken cancellationToken)
        {
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)))
                return StatusCode(StatusCodes.Status403Forbidden);

            if (_repository is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);

            var grantCase = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(grantCase.HprNumber, currentUser?.HprNumber, StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status403Forbidden);

            var domainCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException();
            var currentApproval = domainCase.GetCurrentGuidanceApproval();
            if (currentApproval is null || currentApproval.Status != Domain.Enums.GuidanceApprovalStatus.NeedsCorrection)
                return BadRequest();

            var data = domainCase.ApplicationDataJson is null
                ? new Alisflyt.Domain.Forms.GrantApplicationData()
                : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(domainCase.ApplicationDataJson)
                    ?? new Alisflyt.Domain.Forms.GrantApplicationData();

            var vm = new Alisflyt.Web.ViewModels.Guidance.GuidanceCorrectionEditViewModel
            {
                CaseId = domainCase.Id,
                CaseNumber = domainCase.CaseNumber,
                HprNumber = grantCase.HprNumber,
                DoctorName = data.DoctorName ?? currentUser?.Name ?? string.Empty,
                CorrectionReason = currentApproval.RequestedRevisionReason,
                Sessions = data.Certificate?.Sessions ?? []
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGuidance(Guid? id, Alisflyt.Web.ViewModels.Guidance.GuidanceCorrectionEditViewModel model, CancellationToken cancellationToken)
        {
            if (id is null || id == Guid.Empty)
                id = model.CaseId == Guid.Empty ? null : model.CaseId;

            if (id is null || id == Guid.Empty)
                return BadRequest("Mangler saks-ID for oppdatering av veiledningstimer.");

            model.CaseId = id.Value;

            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)))
                return StatusCode(StatusCodes.Status403Forbidden);

            if (_repository is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);

            try
            {
                var grantCase = await _svc.GetByIdAsync(id.Value, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(grantCase.HprNumber, currentUser?.HprNumber, StringComparison.OrdinalIgnoreCase))
                    return StatusCode(StatusCodes.Status403Forbidden);

                var domainCase = await _repository.GetByIdAsync(id.Value, cancellationToken).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException();
                var currentApproval = domainCase.GetCurrentGuidanceApproval();
                if (currentApproval is null || currentApproval.Status != Domain.Enums.GuidanceApprovalStatus.NeedsCorrection)
                    return BadRequest();

                if (model.Sessions is null || model.Sessions.Count == 0)
                    ModelState.AddModelError(nameof(model.Sessions), "Legg til minst én veiledningsøkt.");
                else
                {
                    foreach (var session in model.Sessions)
                    {
                        if (!session.Date.HasValue)
                            ModelState.AddModelError(nameof(model.Sessions), "Velg dato for hver veiledningsøkt.");
                        if (!session.Hours.HasValue || session.Hours <= 0m)
                            ModelState.AddModelError(nameof(model.Sessions), "Oppgi timer større enn 0 for hver veiledningsøkt.");
                        if (string.IsNullOrWhiteSpace(session.Topic))
                            ModelState.AddModelError(nameof(model.Sessions), "Legg inn tema for hver veiledningsøkt.");
                    }
                }

                if (!ModelState.IsValid)
                    return View("EditGuidance", model);

                var data = domainCase.ApplicationDataJson is null
                    ? new Alisflyt.Domain.Forms.GrantApplicationData()
                    : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(domainCase.ApplicationDataJson)
                        ?? new Alisflyt.Domain.Forms.GrantApplicationData();

                data.HprNumber ??= grantCase.HprNumber;
                data.DoctorName ??= currentUser?.Name ?? data.DoctorName;
                if (data.Certificate is null)
                    data.Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel();
                data.Certificate.Sessions = model.Sessions
                    .Select(s => new Alisflyt.Domain.Forms.SupervisionSessionInputModel
                    {
                        Date = s.Date,
                        Hours = s.Hours,
                        Topic = s.Topic ?? string.Empty
                    })
                    .ToList();

                data.ValidateDraft();
                var now = DateTimeOffset.UtcNow;
                domainCase.UpdateApplication(data, now);
                domainCase.RequestGuidanceApproval(
                    grantCase.HprNumber ?? data.HprNumber ?? currentUser?.HprNumber ?? string.Empty,
                    currentApproval.SupervisorHprNumber,
                    currentApproval.SupervisorUserKey,
                    now,
                    currentApproval.SupervisorName);
                await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                TempData["Success"] = "Veiledningstimer er oppdatert og sendt til ny godkjenning.";
                return RedirectToAction(nameof(Details), new { id = id.Value });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "Saken ble oppdatert av en annen bruker eller vindu mens du jobbet. Last inn siden på nytt og prøv igjen.");
                return View("EditGuidance", model);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("EditGuidance", model);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("EditGuidance", model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CalculationPreview(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var calcSvc = (Alisflyt.Application.Services.IGrantCalculationApplicationService?)HttpContext?.RequestServices.GetService(typeof(Alisflyt.Application.Services.IGrantCalculationApplicationService));
                if (calcSvc == null) return StatusCode(500);
                var calc = await calcSvc.CalculateForCaseAsync(id, cancellationToken).ConfigureAwait(false);
                return PartialView("_CalculationResultPartial", calc);
            }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(); }
            catch { return StatusCode(500); }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }

            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(d.HprNumber, currentUser.HprNumber, StringComparison.OrdinalIgnoreCase)))
            {
                return Forbid();
            }

            if (d.Status != Domain.Enums.GrantCaseStatus.Draft && d.Status != Domain.Enums.GrantCaseStatus.ReturnedForCorrection)
                return BadRequest();

            var model = FormModel(d);
            model.GrantType = Domain.Forms.GrantType.AlisAgreementIncludingSupervision;
            return View("Application", model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        {
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "ALIS", StringComparison.OrdinalIgnoreCase)))
                return Forbid();

            try
            {
                Alisflyt.Application.Models.SubmitGrantCaseRequest? guidanceRequest = null;
                if (currentUser is not null)
                {
                    var data = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(data.HprNumber, currentUser.HprNumber, StringComparison.OrdinalIgnoreCase))
                        return Forbid();

                    guidanceRequest = CreateGuidanceRequest(data);
                }

                await _svc.SubmitAsync(id, guidanceRequest, cancellationToken).ConfigureAwait(false);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Alisflyt.Domain.Forms.SubmissionValidationException ex)
            {
                foreach (var error in ex.Errors)
                    ModelState.AddModelError(string.Empty, error);
            }
            catch (ArgumentOutOfRangeException)
            {
                ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn: Stillingsprosent må være større enn 0 og høyst 100.");
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn: " + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Søknaden kan ikke sendes inn i nåværende status."
                    : ex.Message;
                ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn: " + message);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Søknaden kunne ikke lagres. Ingen del av innsendingen ble fullført.");
            }

            // Reuse the complete details model while preserving validation errors.
            return await Details(id, cancellationToken);
        }

        private static Alisflyt.Application.Models.SubmitGrantCaseRequest? CreateGuidanceRequest(GrantCaseDto grantCase)
        {
            var certificate = grantCase.ApplicationData?.Certificate;
            if (certificate?.Sessions is null || certificate.Sessions.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(grantCase.HprNumber))
                throw new InvalidOperationException("Kan ikke opprette godkjenningsforespørsel uten HPR-nummer for ALIS-legens sak.");

            var doctorUserKey = DemoAuthentication.GetDoctorUserKeyForHprNumber(grantCase.HprNumber);
            if (string.IsNullOrWhiteSpace(doctorUserKey))
                throw new InvalidOperationException($"Fant ingen ALIS-bruker for HPR-nummer {grantCase.HprNumber}.");

            var supervisorUserKey = DemoAuthentication.GetSupervisorUserKeyForDoctorUserKey(doctorUserKey);
            if (string.IsNullOrWhiteSpace(supervisorUserKey))
                throw new InvalidOperationException($"Fant ingen tilknyttet veileder for ALIS-bruker {doctorUserKey}.");

            var supervisor = DemoAuthentication.DemoUsers.FirstOrDefault(u =>
                string.Equals(u.Key, supervisorUserKey, StringComparison.OrdinalIgnoreCase));
            if (supervisor is null)
                throw new InvalidOperationException($"Fant ingen veileder med nøkkel {supervisorUserKey}.");

            return new Alisflyt.Application.Models.SubmitGrantCaseRequest
            {
                DoctorHprNumber = grantCase.HprNumber,
                SupervisorHprNumber = string.IsNullOrWhiteSpace(certificate.SupervisorHprNumber)
                    ? supervisor.HprNumber
                    : certificate.SupervisorHprNumber,
                SupervisorUserKey = supervisorUserKey,
                SupervisorName = supervisor.Name
            };
        }
    }
}
