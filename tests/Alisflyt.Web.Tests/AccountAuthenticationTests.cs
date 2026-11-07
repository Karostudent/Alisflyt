using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Models;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Alisflyt.Web.Tests;

public class AccountAuthenticationTests
{
    [Fact]
    public void Login_stores_user_in_session()
    {
        var context = CreateContext();
        var controller = CreateController(context);

        var result = controller.Login("ALIS");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.NotNull(DemoAuthentication.GetCurrentUser(context));
    }

    [Fact]
    public void Login_shows_general_error_when_session_write_fails()
    {
        var context = CreateContext();
        ((FailingSession)context.Session).ThrowOnSet = true;
        var logger = new TestLogger<AccountController>();
        var controller = CreateController(context, logger);

        var result = controller.Login("ALIS");

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Innlogging kunne ikke fullføres. Prøv igjen.", controller.TempData!["Error"]);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.DoesNotContain("123456789", entry.State);
    }

    [Fact]
    public void Logout_removes_user_from_session()
    {
        var context = CreateContext();
        DemoAuthentication.SignIn(context, DemoAuthentication.GetUserByRole("ALIS")!);
        var controller = CreateController(context);

        var result = controller.Logout();

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(DemoAuthentication.GetCurrentUser(context));
    }

    [Fact]
    public void Logout_shows_general_error_when_session_remove_fails()
    {
        var context = CreateContext();
        DemoAuthentication.SignIn(context, DemoAuthentication.GetUserByRole("ALIS")!);
        ((FailingSession)context.Session).ThrowOnRemove = true;
        var logger = new TestLogger<AccountController>();
        var controller = CreateController(context, logger);

        var result = controller.Logout();

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Utlogging kunne ikke fullføres. Prøv igjen.", controller.TempData!["Error"]);
        Assert.NotNull(DemoAuthentication.GetCurrentUser(context));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.DoesNotContain("123456789", entry.State);
    }

    [Fact]
    public void GetCurrentUser_returns_null_for_missing_session()
    {
        var context = CreateContext();

        Assert.Null(DemoAuthentication.GetCurrentUser(context));
    }

    [Fact]
    public void GetCurrentUser_fails_closed_for_corrupt_session_and_logs_without_payload()
    {
        var context = CreateContext();
        context.Session.Set("Alisflyt.DemoUser", "not-json"u8.ToArray());
        var logger = new TestLogger<AccountController>();

        var user = DemoAuthentication.GetCurrentUser(context, logger);

        Assert.Null(user);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.DoesNotContain("not-json", entry.State);
    }

    [Fact]
    public void Role_protected_action_rejects_missing_session()
    {
        var context = CreateContext();
        var controller = new CoordinatorController(
            new FakeGrantCaseApplicationService());
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = controller.Index();

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result.Result).StatusCode);
    }

    private static AccountController CreateController(HttpContext context, ILogger<AccountController>? logger = null)
    {
        var controller = new AccountController(logger)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
        return controller;
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext { Session = new FailingSession() };
        return context;
    }

    private sealed class FailingSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool ThrowOnSet { get; set; }
        public bool ThrowOnRemove { get; set; }
        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key)
        {
            if (ThrowOnRemove)
                throw new InvalidOperationException("session remove failed");
            _store.Remove(key);
        }
        public void Set(string key, byte[] value)
        {
            if (ThrowOnSet)
                throw new InvalidOperationException("session set failed");
            _store[key] = value;
        }
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
