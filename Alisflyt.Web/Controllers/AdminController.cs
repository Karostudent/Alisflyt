using Alisflyt.Infrastructure.Identity;
using Alisflyt.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Alisflyt.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private static readonly string[] AvailableRoles = ["ALIS", "Veileder", "Avdelingsleder", "Administrator"];
    private readonly UserManager<AlisflytUser> _userManager;

    public AdminController(UserManager<AlisflytUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildDashboardModelAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(CreateUserViewModel model)
    {
        if (!AvailableRoles.Contains(model.Role, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(model.Role), "Velg en gyldig rolle.");
        if (string.Equals(model.Role, "Veileder", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(model.HprNumber))
            ModelState.AddModelError(nameof(model.HprNumber), "Veileder må ha et HPR-nummer.");

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardModelAsync());

        var user = new AlisflytUser
        {
            UserName = model.UserName,
            Email = model.Email,
            EmailConfirmed = true,
            HprNumber = string.IsNullOrWhiteSpace(model.HprNumber) ? null : model.HprNumber.Trim()
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View(nameof(Index), await BuildDashboardModelAsync());
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            AddErrors(roleResult);
            return View(nameof(Index), await BuildDashboardModelAsync());
        }

        TempData["Success"] = "Brukeren er opprettet.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminDashboardViewModel> BuildDashboardModelAsync()
    {
        var users = new List<AdminUserViewModel>();
        foreach (var user in _userManager.Users.OrderBy(user => user.UserName))
        {
            users.Add(new AdminUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                HprNumber = user.HprNumber,
                Roles = (await _userManager.GetRolesAsync(user)).ToArray()
            });
        }

        return new AdminDashboardViewModel
        {
            CurrentUserName = User.Identity?.Name,
            Users = users,
            Roles = AvailableRoles
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRole(UpdateUserRoleViewModel model)
    {
        if (!AvailableRoles.Contains(model.Role, StringComparer.Ordinal))
        {
            TempData["Error"] = "Ugyldig rolle.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            TempData["Error"] = "Brukeren finnes ikke.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Contains("Administrator", StringComparer.Ordinal)
            && !string.Equals(model.Role, "Administrator", StringComparison.Ordinal)
            && await IsLastAdministratorAsync(user))
        {
            TempData["Error"] = "Den siste administratorrollen kan ikke fjernes.";
            return RedirectToAction(nameof(Index));
        }

        var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            TempData["Error"] = string.Join(" ", removeResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        var addResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addResult.Succeeded)
        {
            TempData["Error"] = string.Join(" ", addResult.Errors.Select(error => error.Description));
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Rollen er oppdatert.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsLastAdministratorAsync(AlisflytUser user)
    {
        var administrators = await _userManager.GetUsersInRoleAsync("Administrator");
        return administrators.Count(candidate => !string.Equals(candidate.Id, user.Id, StringComparison.Ordinal)) == 0;
    }

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
    }
}
