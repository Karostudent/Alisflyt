using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public class GrantCasesDetailsAuthorizationTests
{
    [Fact]
    public async Task Details_rejects_anonymous_before_loading_sensitive_data()
    {
        var service = new FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("123456789") };
        var controller = new GrantCasesController(service);

        var result = await controller.Details(Guid.NewGuid(), default);

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Empty(service.GetByIdCalls);
    }

    [Fact]
    public async Task Details_allows_owner_alis()
    {
        var service = new FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("123456789") };
        var controller = new GrantCasesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
        };

        var result = await controller.Details(service.GetByIdResponse.Id, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Details_rejects_other_alis_without_returning_view()
    {
        var service = new FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("987654321") };
        var controller = new GrantCasesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
        };

        var result = await controller.Details(service.GetByIdResponse.Id, default);

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public async Task Details_allows_assigned_supervisor()
    {
        var service = new FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("123456789") };
        var controller = new GrantCasesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateSupervisor() }
        };

        var result = await controller.Details(service.GetByIdResponse.Id, default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task Details_rejects_unassigned_supervisor()
    {
        var service = new FakeGrantCaseApplicationService { GetByIdResponse = CreateDto("555555555") };
        var controller = new GrantCasesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateSupervisor() }
        };

        var result = await controller.Details(service.GetByIdResponse.Id, default);

        var forbidden = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    private static GrantCaseDto CreateDto(string hprNumber) => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = "CASE-AUTH",
        HprNumber = hprNumber,
        Status = GrantCaseStatus.Draft,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        LastModifiedAtUtc = DateTimeOffset.UtcNow,
        ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData { DoctorName = "Testlege" }
    };
}
