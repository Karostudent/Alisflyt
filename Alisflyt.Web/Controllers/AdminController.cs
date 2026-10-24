using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Services;
using Alisflyt.Web.Models;
using Alisflyt.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Controllers;

public class AdminController : Controller
{
    private readonly IGrantCaseApplicationService _grantCaseService;
    private readonly IGrantRateSetRepository _grantRateSetRepository;

    public AdminController(IGrantCaseApplicationService grantCaseService, IGrantRateSetRepository grantRateSetRepository)
    {
        _grantCaseService = grantCaseService;
        _grantRateSetRepository = grantRateSetRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
        if (currentUser is null || !string.Equals(currentUser.Role, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Du må være logget inn som administrator for å få tilgang til denne delen.";
            return RedirectToAction("Index", "Home");
        }

        var cases = await _grantCaseService.ListAsync(cancellationToken);
        var rateSets = await _grantRateSetRepository.ListAsync(cancellationToken);

        var vm = new AdminDashboardViewModel
        {
            CurrentUser = currentUser,
            Cases = cases,
            RateSets = rateSets
        };

        return View(vm);
    }
}
