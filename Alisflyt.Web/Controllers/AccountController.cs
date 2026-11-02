using Alisflyt.Infrastructure.Identity;
using Alisflyt.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<AlisflytUser> _signInManager;

    public AccountController(SignInManager<AlisflytUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleHome();

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet]
    public IActionResult RoleHome() => RedirectToRoleHome();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _signInManager.UserManager.FindByEmailAsync(model.UserNameOrEmail);
        var loginName = user?.UserName ?? model.UserNameOrEmail;
        var result = await _signInManager.PasswordSignInAsync(
            loginName,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToRoleHome();
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Brukeren er midlertidig låst. Prøv igjen senere."
            : "Ugyldig brukernavn/e-post eller passord.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToRoleHome()
    {
        if (User.IsInRole("ALIS"))
            return RedirectToAction("Index", "Alis")!;
        if (User.IsInRole("Avdelingsleder"))
            return RedirectToAction("Index", "Coordinator")!;
        if (User.IsInRole("Veileder"))
            return RedirectToAction("Index", "Guidance")!;
        if (User.IsInRole("Administrator"))
            return RedirectToAction("Index", "Admin")!;

        return RedirectToAction("Index", "Home")!;
    }
}
