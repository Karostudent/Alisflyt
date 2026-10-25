using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.ViewModels.Guidance;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Controllers;

public class SupervisorController(
    IGrantCaseApplicationService service,
    IGrantCaseRepository repository,
    TimeProvider timeProvider,
    IGrantCalculationApplicationService calculationService,
    IGuidanceApprovalService guidanceApprovalService) : Controller
{
    private static bool HasRelevantGuidanceSessions(GrantCase grantCase)
    {
        if (string.IsNullOrWhiteSpace(grantCase.ApplicationDataJson))
            return false;

        var app = System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson);
        return app?.Certificate?.Sessions?.Any(session => session.Hours is > 0m) == true;
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

        return "Avventer godkjenning";
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

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = Alisflyt.Web.Models.DemoAuthentication.GetCurrentUser(HttpContext);
        if (currentUser is null || !string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        try
        {
            var grantCase = await service.GetByIdAsync(id, cancellationToken);
            if (!Alisflyt.Web.Models.DemoAuthentication.IsCurrentSupervisorForDoctor(HttpContext, grantCase.HprNumber))
                return Forbid();

            var domainCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
            var currentApproval = domainCase.GetCurrentGuidanceApproval();
            var calculation = await calculationService.CalculateForCaseAsync(id, cancellationToken);
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
        var currentUser = Alisflyt.Web.Models.DemoAuthentication.GetCurrentUser(HttpContext);
        if (currentUser is null || !string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        try
        {
            if (!confirmsAccuracy) throw new ArgumentException("Bekreft at opplysningene er riktige.");
            var grantCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
            if (!Alisflyt.Web.Models.DemoAuthentication.IsCurrentSupervisorForDoctor(HttpContext, grantCase.HprNumber))
                return Forbid();

            var currentApproval = grantCase.GetCurrentGuidanceApproval();
            if (currentApproval is not null)
            {
                await guidanceApprovalService.ApproveAsync(id, currentApproval.Id, currentUser.Key, cancellationToken);
            }
            else
            {
                grantCase.ApproveCertificate(timeProvider.GetUtcNow());
                await repository.SaveChangesAsync(cancellationToken);
            }
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
