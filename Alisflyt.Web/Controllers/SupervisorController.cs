using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.ViewModels.Guidance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Alisflyt.Infrastructure.Identity;
using System.Text.Json;

namespace Alisflyt.Web.Controllers;

[Authorize(Roles = "Veileder")]
public class SupervisorController(
    IGrantCaseRepository repository,
    IGrantCalculationApplicationService calculationService,
    IGuidanceApprovalService guidanceApprovalService,
    UserManager<AlisflytUser> userManager) : Controller
{
    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private void EnsureTempData()
    {
        TempData ??= new TempDataDictionary(HttpContext, new EmptyTempDataProvider());
    }

    private static bool HasRelevantGuidanceSessions(GrantCase grantCase)
    {
        if (string.IsNullOrWhiteSpace(grantCase.ApplicationDataJson))
            return false;

        var app = System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson);
        return app?.Certificate?.Sessions?.Any(session => session.Hours is > 0m) == true;
    }

    private static bool IsCurrentSupervisor(GrantCase grantCase, string supervisorHpr)
    {
        if (string.IsNullOrWhiteSpace(grantCase.ApplicationDataJson))
            return false;

        var application = JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson);
        return string.Equals(
            application?.Certificate?.SupervisorHprNumber,
            supervisorHpr,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AlisflytUser?> GetCurrentSupervisorAsync()
    {
        var user = await userManager.GetUserAsync(User);
        return user is { } && !string.IsNullOrWhiteSpace(user.HprNumber) ? user : null;
    }

    private static string GetGuidanceStatusText(GrantCase grantCase, GuidanceApproval? currentApproval)
    {
        if (currentApproval is not null)
        {
            return currentApproval.Status switch
            {
                GuidanceApprovalStatus.Pending => "Avventer godkjenning",
                GuidanceApprovalStatus.Approved => "Godkjent",
                GuidanceApprovalStatus.NeedsCorrection => "Korrigering nødvendig",
                GuidanceApprovalStatus.Superseded => "Tidligere attestversjon / erstattet",
                _ => currentApproval.Status.ToString()
            };
        }

        if (!HasRelevantGuidanceSessions(grantCase))
            return "Godkjenning ikke påkrevd";

        if (grantCase.Status is GrantCaseStatus.Draft or GrantCaseStatus.ReturnedForCorrection)
            return "Ikke sendt til godkjenning";

        return "Mangler aktiv godkjenningsforespørsel";
    }

    private static GuidanceCalculationViewModel ToGuidanceCalculationViewModel(GrantCalculationDetailsDto? calculation)
    {
        if (calculation is null)
        {
            return new GuidanceCalculationViewModel { IsAvailable = false, UnavailableReason = "Beregning ikke tilgjengelig" };
        }

        return new GuidanceCalculationViewModel
        {
            IsAvailable = calculation.IsAvailable,
            GuidanceAmount = calculation.GuidanceAmount,
            RateSetName = calculation.RateSetName,
            RateSetValidFrom = calculation.RateSetValidFrom,
            RateSetValidTo = calculation.RateSetValidTo,
            UnavailableReason = calculation.UnavailableReason
        };
    }

    private static GrantCaseDto ToGrantCaseDto(GrantCase grantCase)
    {
        return new GrantCaseDto
        {
            ApplicationData = grantCase.ApplicationDataJson is null
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson),
            Id = grantCase.Id,
            CaseNumber = grantCase.CaseNumber,
            HprNumber = grantCase.HprNumber,
            EmploymentPercentage = grantCase.EmploymentPercentage,
            EmploymentStartDate = grantCase.EmploymentStartDate,
            EmploymentEndDate = grantCase.EmploymentEndDate,
            ReturnReason = grantCase.ReturnReason,
            ReturnedAtUtc = grantCase.ReturnedAtUtc,
            Status = grantCase.Status,
            CreatedAtUtc = grantCase.CreatedAtUtc,
            LastModifiedAtUtc = grantCase.LastModifiedAtUtc,
            ReadyForAuditorApprovalAtUtc = grantCase.ReadyForAuditorApprovalAtUtc,
            GuidanceApprovalRequired = grantCase.RequiresGuidanceApproval(),
            GuidanceApprovalApproved = grantCase.HasApprovedCurrentGuidanceApproval()
        };
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentSupervisorAsync();
        if (currentUser is null)
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            var domainCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
            if (!IsCurrentSupervisor(domainCase, currentUser.HprNumber!))
                return StatusCode(StatusCodes.Status403Forbidden);

            var grantCase = ToGrantCaseDto(domainCase);
            var currentApproval = domainCase.GetCurrentGuidanceApproval();
            GrantCalculationDetailsDto? calculation;
            try
            {
                calculation = await calculationService.CalculateForCaseAsync(id, cancellationToken);
            }
            catch (KeyNotFoundException)
            {
                calculation = null;
            }
            var requiresGuidanceApproval = HasRelevantGuidanceSessions(domainCase)
                && domainCase.Status is not GrantCaseStatus.Draft
                && domainCase.Status is not GrantCaseStatus.ReturnedForCorrection;

            ViewData["GuidanceCalculation"] = ToGuidanceCalculationViewModel(calculation);
            ViewData["GuidanceApproval"] = currentApproval;
            ViewData["GuidanceStatusText"] = GetGuidanceStatusText(domainCase, currentApproval);
            ViewData["RequiresGuidanceApproval"] = requiresGuidanceApproval;
            return View(grantCase);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, bool confirmsAccuracy, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentSupervisorAsync();
        if (currentUser is null)
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            if (!confirmsAccuracy)
                throw new ArgumentException("Bekreft at opplysningene er riktige.");

            var grantCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
            if (!IsCurrentSupervisor(grantCase, currentUser.HprNumber!))
                return StatusCode(StatusCodes.Status403Forbidden);

            var currentApproval = grantCase.GetCurrentGuidanceApproval();
            if (currentApproval is null)
                throw new InvalidOperationException("Det finnes ingen aktiv godkjenningsforespørsel for denne attestversjonen.");
            if (currentApproval.Status != GuidanceApprovalStatus.Pending)
                throw new InvalidOperationException("Kun en ventende attestversjon kan godkjennes.");

            await guidanceApprovalService.ApproveAsync(id, currentApproval.Id, currentUser.Id, cancellationToken);
            EnsureTempData();
            TempData["Success"] = "Veiledningstimene er godkjent.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { EnsureTempData(); TempData["Error"] = ex.Message; }
        catch (InvalidOperationException ex) { EnsureTempData(); TempData["Error"] = ex.Message; }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCorrection(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentSupervisorAsync();
        if (currentUser is null)
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Begrunnelse for korrigering er påkrevd.");

            var grantCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
            if (!IsCurrentSupervisor(grantCase, currentUser.HprNumber!))
                return StatusCode(StatusCodes.Status403Forbidden);

            var currentApproval = grantCase.GetCurrentGuidanceApproval();
            if (currentApproval is null)
                throw new InvalidOperationException("Det finnes ingen aktiv godkjenningsforespørsel for denne attestversjonen.");
            if (currentApproval.Status != GuidanceApprovalStatus.Pending)
                throw new InvalidOperationException("Kun en ventende attestversjon kan sendes tilbake for korrigering.");

            await guidanceApprovalService.RequestCorrectionAsync(id, currentApproval.Id, reason.Trim(), cancellationToken);
            EnsureTempData();
            TempData["Success"] = "Korrigering er sendt til ALIS.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { EnsureTempData(); TempData["Error"] = ex.Message; }
        catch (InvalidOperationException ex) { EnsureTempData(); TempData["Error"] = ex.Message; }

        return RedirectToAction(nameof(Details), new { id });
    }
}
