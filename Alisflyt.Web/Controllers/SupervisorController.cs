using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Controllers;

public class SupervisorController(
    IGrantCaseApplicationService service,
    IGrantCaseRepository repository,
    TimeProvider timeProvider,
    IGrantCalculationApplicationService calculationService,
    IGuidanceApprovalService guidanceApprovalService) : Controller
{
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var currentUser = Alisflyt.Web.Models.DemoAuthentication.GetCurrentUser(HttpContext);
        if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase)))
            return Forbid();

        try
        {
            var grantCase = await service.GetByIdAsync(id, cancellationToken);
            if (!Alisflyt.Web.Models.DemoAuthentication.IsCurrentSupervisorForDoctor(HttpContext, grantCase.HprNumber))
                return Forbid();

            ViewData["Calculation"] = await calculationService.CalculateForCaseAsync(id, cancellationToken);
            return View(grantCase);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, bool confirmsAccuracy, CancellationToken cancellationToken)
    {
        var currentUser = Alisflyt.Web.Models.DemoAuthentication.GetCurrentUser(HttpContext);
        if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase)))
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
