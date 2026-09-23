using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Alisflyt.Web.ViewModels.GrantCases;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public class GrantApplicationFormTests
{
    [Fact]
    public async Task Edit_reopens_full_application_and_certificate()
    {
        var dto = new GrantCaseDto { Id = Guid.NewGuid(), Status = GrantCaseStatus.Draft,
            ApplicationData = new() { DoctorName = "Lege", Certificate = new() { SupervisorName = "Veileder", Sessions = [new() { Hours = 1.5m, Topic = "Tema" }] } } };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });
        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<EditGrantCaseViewModel>(result.Model);
        Assert.Equal(dto.Id, model.Id);
        Assert.Equal("Lege", model.DoctorName);
        Assert.Equal("Veileder", model.Certificate.SupervisorName);
        Assert.Equal(1.5m, Assert.Single(model.Certificate.Sessions).Hours);
    }

    [Fact]
    public async Task Old_draft_is_presented_as_an_employment_row()
    {
        var dto = new GrantCaseDto { Id = Guid.NewGuid(), HprNumber = "123", EmploymentPercentage = 75,
            EmploymentStartDate = new(2026, 1, 1), EmploymentEndDate = new(2026, 6, 30), Status = GrantCaseStatus.Draft };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });
        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<EditGrantCaseViewModel>(result.Model);
        Assert.Equal("123", model.HprNumber);
        var period = Assert.Single(model.EmploymentPeriods);
        Assert.Equal(75, period.PositionPercentage);
        Assert.Equal(dto.EmploymentEndDate, period.FundingThrough);
    }

    [Fact]
    public async Task Submitted_application_can_be_viewed_read_only()
    {
        var dto = new GrantCaseDto { Id = Guid.NewGuid(), Status = GrantCaseStatus.Submitted, ApplicationData = new() { DoctorName = "Lege" } };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });
        var result = Assert.IsType<ViewResult>(await controller.Application(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);
        Assert.True(model.ReadOnly);
        Assert.Equal("Lege", model.DoctorName);
    }
}
