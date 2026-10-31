using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace Alisflyt.Web.Tests;

public class CoordinatorAuthorizationTests
{
    [Theory]
    [InlineData("Index")]
    [InlineData("Details")]
    [InlineData("StartReview")]
    [InlineData("SaveCentrality")]
    [InlineData("ReturnForCorrection")]
    [InlineData("ExportExcel")]
    [InlineData("MarkReadyForAuditorApproval")]
    public async Task Non_coordinators_are_forbidden_before_case_access(string action)
    {
        var fake = new FakeGrantCaseApplicationService
        {
            ListResponse = [],
            GetByIdResponse = new GrantCaseDto { Id = Guid.NewGuid() }
        };
        var controller = CreateController(fake, "ALIS");
        var id = Guid.NewGuid();
        IActionResult result = action switch
        {
            "Index" => await controller.Index(),
            "Details" => await controller.Details(id, default),
            "StartReview" => await controller.StartReview(id, default),
            "SaveCentrality" => await controller.SaveCentrality(id, true, 100m, null!, TimeProvider.System, default),
            "ReturnForCorrection" => await controller.ReturnForCorrection(id, default),
            "ExportExcel" => await controller.ExportExcel(id, new NoOpCalculationService(), TimeProvider.System, default),
            "MarkReadyForAuditorApproval" => await controller.MarkReadyForAuditorApproval(id, default),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(fake.ListCalls);
        Assert.Empty(fake.GetByIdCalls);
        Assert.Empty(fake.StartReviewCalls);
        Assert.Empty(fake.MarkReadyForAuditorApprovalCalls);
        Assert.Empty(fake.ReturnForCorrectionCalls);
    }

    [Theory]
    [InlineData("ALIS")]
    [InlineData("Veileder")]
    [InlineData(null)]
    public async Task All_non_coordinator_roles_are_forbidden(string? role)
    {
        var fake = new FakeGrantCaseApplicationService { ListResponse = [] };
        var controller = CreateController(fake, role);

        var result = await controller.Index();

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Empty(fake.ListCalls);
    }

    private static CoordinatorController CreateController(FakeGrantCaseApplicationService fake, string? role)
    {
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        if (role is not null)
        {
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = role.ToLowerInvariant() + "-test",
                Role = role,
                Name = role + " Test"
            });
        }

        var controller = new CoordinatorController(fake)
        {
            TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class NoOpCalculationService : IGrantCalculationApplicationService
    {
        public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
            => Task.FromResult(new GrantCalculationDetailsDto { IsAvailable = true });
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
