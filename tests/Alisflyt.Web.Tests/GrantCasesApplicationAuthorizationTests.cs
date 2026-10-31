using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public sealed class GrantCasesApplicationAuthorizationTests
{
    [Fact]
    public async Task Application_allows_owner_alis()
    {
        var service = new Fakes.FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("123456789") };
        var controller = CreateController(service, CreateUser("ALIS", "123456789", "alis-anna"));

        var result = await controller.Application(service.GetByIdResponse.Id, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Application_rejects_other_alis_without_returning_content()
        => await AssertForbiddenAsync("ALIS", "987654321");

    [Fact]
    public async Task Application_allows_assigned_supervisor()
    {
        var service = new Fakes.FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("123456789") };
        var controller = CreateController(service, CreateUser("Veileder", "987654321", "veileder-per"));

        var result = await controller.Application(service.GetByIdResponse.Id, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Application_rejects_unassigned_supervisor_without_returning_content()
        => await AssertForbiddenAsync("Veileder", "555555555", "veileder-per");

    [Fact]
    public async Task Application_allows_coordinator()
    {
        var service = new Fakes.FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("999999999") };
        var controller = CreateController(service, CreateUser("Koordinator", "000000000", "koordinator-marta"));

        var result = await controller.Application(service.GetByIdResponse.Id, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Application_rejects_administrator_before_loading_content()
        => await AssertForbiddenBeforeLoadAsync("Administrator", "999999999");

    [Fact]
    public async Task Application_rejects_anonymous_before_loading_content()
        => await AssertForbiddenBeforeLoadAsync(null, null);

    [Fact]
    public async Task Application_rejects_unknown_role_before_loading_content()
        => await AssertForbiddenBeforeLoadAsync("Unknown", "000000001", "unknown-user");

    private static async Task AssertForbiddenAsync(string role, string caseHpr, string key = "alis-test")
    {
        var service = new Fakes.FakeGrantCaseApplicationService { GetByIdResponse = CreateDto(caseHpr) };
        var controller = CreateController(service, CreateUser(role, role == "ALIS" ? "123456789" : "987654321", key));

        var result = await controller.Application(service.GetByIdResponse.Id, default);

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static async Task AssertForbiddenBeforeLoadAsync(string? role, string? hpr, string key = "test-user")
    {
        var service = new Fakes.FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("123456789") };
        var controller = CreateController(service, role is null ? null : CreateUser(role, hpr!, key));

        var result = await controller.Application(Guid.NewGuid(), default);

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(service.GetByIdCalls);
    }

    private static GrantCasesController CreateController(Fakes.FakeGrantCaseApplicationService service, DemoUser? user)
    {
        var controller = new GrantCasesController(service);
        if (user is not null)
        {
            var context = new DefaultHttpContext { Session = new TestSession() };
            DemoAuthentication.SignIn(context, user);
            controller.ControllerContext = new ControllerContext { HttpContext = context };
        }
        return controller;
    }

    private static DemoUser CreateUser(string role, string hpr, string key)
        => new() { Role = role, HprNumber = hpr, Key = key, Name = "Testbruker" };

    private static GrantCaseDto CreateDto(string hprNumber) => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = "APPLICATION-AUTH",
        HprNumber = hprNumber,
        Status = GrantCaseStatus.Submitted,
        ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData { DoctorName = "Sensitiv lege" }
    };

    private sealed class TestSession : ISession
    {
        private readonly System.Collections.Generic.Dictionary<string, byte[]> _store = new();
        public System.Collections.Generic.IEnumerable<string> Keys => _store.Keys;
        public string Id => "application-auth";
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
