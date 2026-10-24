using Alisflyt.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Controllers;

public class AccountController : Controller
{
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

        DemoAuthentication.SignIn(HttpContext, user);

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

        DemoAuthentication.SignIn(HttpContext, user);

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
        DemoAuthentication.SignOut(HttpContext);
        return RedirectToAction("Index", "Home");
    }
}
