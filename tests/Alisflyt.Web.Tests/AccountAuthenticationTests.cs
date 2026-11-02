using System.Reflection;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Alisflyt.Web.Tests;

public class AccountAuthenticationTests
{
    [Fact]
    public void Login_view_model_requires_username_and_password()
    {
        var model = new LoginViewModel();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model,
            validationContext,
            errors,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Account_controller_allows_anonymous_login()
    {
        var attribute = typeof(AccountController).GetCustomAttribute<AllowAnonymousAttribute>();

        Assert.NotNull(attribute);
    }

    [Fact]
    public void Admin_controller_requires_administrator_role()
    {
        var attribute = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Administrator", attribute!.Roles);
    }
}
