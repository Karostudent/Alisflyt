using Alisflyt.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Alisflyt.Web.Controllers;

public class AccountController : Controller
{
    private readonly ILogger<AccountController> _logger;

    public AccountController(ILogger<AccountController>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance;
    }

    [HttpGet]
    public IActionResult Login(string? role = null)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return View(DemoAuthentication.DemoUsers);
        }

        var user = DemoAuthentication.GetUserByRole(role);
        if (user is null)
        {
            return NotFound();
        }

        if (!DemoAuthentication.SignIn(HttpContext, user, _logger))
        {
            TempData["Error"] = "Innlogging kunne ikke fullføres. Prøv igjen.";
            return RedirectToAction(nameof(Login));
        }

        return role switch
        {
            "ALIS" => RedirectToAction("Index", "Alis"),
            "Koordinator" => RedirectToAction("Index", "Coordinator"),
            "Veileder" => RedirectToAction("Index", "Guidance"),
            "Administrator" => RedirectToAction("Index", "Admin"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string role, string? returnUrl = null)
    {
        var user = DemoAuthentication.GetUserByRole(role);
        if (user is null)
        {
            TempData["Error"] = "Velg en gyldig demo-bruker.";
            return RedirectToAction(nameof(Login));
        }

        if (!DemoAuthentication.SignIn(HttpContext, user, _logger))
        {
            TempData["Error"] = "Innlogging kunne ikke fullføres. Prøv igjen.";
            return RedirectToAction(nameof(Login));
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return role switch
        {
            "ALIS" => RedirectToAction("Index", "Alis"),
            "Koordinator" => RedirectToAction("Index", "Coordinator"),
            "Veileder" => RedirectToAction("Index", "Guidance"),
            "Administrator" => RedirectToAction("Index", "Admin"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    public IActionResult Logout()
    {
        if (!DemoAuthentication.SignOut(HttpContext, _logger))
            TempData["Error"] = "Utlogging kunne ikke fullføres. Prøv igjen.";

        return RedirectToAction("Index", "Home");
    }
}
