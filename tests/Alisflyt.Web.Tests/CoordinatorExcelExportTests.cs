using System.Collections.Generic;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public class CoordinatorExcelExportTests
{
    [Theory]
    [InlineData(GrantCaseStatus.Draft)]
    [InlineData(GrantCaseStatus.Submitted)]
    [InlineData(GrantCaseStatus.ReturnedForCorrection)]
    public async Task Export_requires_started_review(GrantCaseStatus status)
    {
        var controller = CreateAuthorizedController(new GrantCaseDto { Status = status });
        Assert.IsType<BadRequestObjectResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));
    }

    [Theory]
    [InlineData(GrantCaseStatus.UnderReview)]
    [InlineData(GrantCaseStatus.AwaitingAuditorApproval)]
    public async Task Export_returns_download_for_allowed_status_without_changing_case(GrantCaseStatus status)
    {
        var readyAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var application = new GrantCaseDto { Status = status, ReadyForAuditorApprovalAtUtc = status == GrantCaseStatus.AwaitingAuditorApproval ? readyAt : null };
        var controller = CreateAuthorizedController(application);

        var result = Assert.IsType<FileContentResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));

        Assert.EndsWith(".xlsx", result.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.NotEmpty(result.FileContents);
        Assert.Equal(status, application.Status);
        Assert.Equal(status == GrantCaseStatus.AwaitingAuditorApproval ? readyAt : null, application.ReadyForAuditorApprovalAtUtc);
    }

    [Fact]
    public async Task Missing_case_returns_not_found()
    {
        var controller = CreateAuthorizedController(null, new KeyNotFoundException());
        Assert.IsType<NotFoundResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));
    }

    [Fact]
    public async Task Export_direct_call_is_forbidden_without_coordinator_role()
    {
        var controller = new CoordinatorController(new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto { Status = GrantCaseStatus.UnderReview }
        });

        var forbidden = Assert.IsType<StatusCodeResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static CoordinatorController CreateAuthorizedController(GrantCaseDto? response, Exception? exception = null)
    {
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
        {
            Key = "koordinator-marta",
            Role = "Koordinator",
            Name = "Marta Solberg"
        });
        var controller = new CoordinatorController(new FakeGrantCaseApplicationService
        {
            GetByIdResponse = response,
            GetByIdException = exception
        });
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class Calculation : IGrantCalculationApplicationService
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
}
