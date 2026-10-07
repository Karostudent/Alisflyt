using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Controllers;

public class SupervisorController(IGrantCaseApplicationService service, IGrantCaseRepository repository, TimeProvider timeProvider) : Controller
{
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        try { return View(await service.GetByIdAsync(id, cancellationToken)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, bool confirmsAccuracy, CancellationToken cancellationToken)
    {
        try
        {
            if (!confirmsAccuracy) throw new ArgumentException("Bekreft at opplysningene er riktige.");
            var grantCase = await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException();
            grantCase.ApproveCertificate(timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
