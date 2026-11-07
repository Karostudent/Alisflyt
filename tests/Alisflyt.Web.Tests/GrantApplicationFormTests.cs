using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
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
            ApplicationData = new() { GrantType = GrantType.SupervisionOnly, DoctorName = "Lege", Certificate = new() { SupervisorHprNumber = "1234567", Sessions = [new() { Hours = 2m, Topic = "Tema" }] } } };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });
        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);
        Assert.Equal(dto.Id, model.Id);
        Assert.Equal(GrantType.SupervisionOnly, model.GrantType);
        Assert.Equal("Lege", model.DoctorName);
        Assert.Equal("1234567", model.Certificate.SupervisorHprNumber);
        Assert.Equal(2m, Assert.Single(model.Certificate.Sessions).Hours);
    }

    [Fact]
    public async Task Old_draft_is_presented_as_an_employment_row()
    {
        var dto = new GrantCaseDto { Id = Guid.NewGuid(), HprNumber = "123", EmploymentPercentage = 75,
            EmploymentStartDate = new(2026, 1, 1), EmploymentEndDate = new(2026, 6, 30), Status = GrantCaseStatus.Draft };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });
        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);
        Assert.Equal("123", model.HprNumber);
        var period = Assert.Single(model.EmploymentPeriods);
        Assert.Equal(75, period.PositionPercentage);
        Assert.Equal(dto.EmploymentEndDate, period.FundingThrough);
    }

    [Fact]
    public async Task Edit_exposes_missing_funding_dates_as_field_errors()
    {
        var dto = new GrantCaseDto
        {
            Id = Guid.NewGuid(),
            Status = GrantCaseStatus.Draft,
            ApplicationData = new()
            {
                EmploymentPeriods = [new() { PositionPercentage = 80, EmploymentStartDate = new(2026, 1, 1) }]
            }
        };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });

        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);

        Assert.Contains(model.InitialErrors, error => error.Field == "EmploymentPeriods[0].FundingFrom");
        Assert.Contains(model.InitialErrors, error => error.Field == "EmploymentPeriods[0].FundingThrough");
    }

    [Fact]
    public async Task Edit_maps_position_group_and_agreement_errors_to_stable_sections()
    {
        var dto = new GrantCaseDto
        {
            Id = Guid.NewGuid(),
            Status = GrantCaseStatus.ReturnedForCorrection,
            ApplicationData = new()
            {
                GrantType = GrantType.AlisAgreementIncludingSupervision,
                SelectedPositionTypes = [],
                Attachments = [],
                EmploymentPeriods = [new() { PositionType = PositionType.RegularGpOrLocum }]
            }
        };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });

        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);

        Assert.Contains(model.InitialErrors, error => error.Field == "position-types" && error.Code == "PositionTypes");
        Assert.Contains(model.InitialErrors, error => error.Field == "EmploymentPeriods[0].PositionType");
        Assert.Contains(model.InitialErrors, error => error.Field == "agreement-upload-area" && error.Code == "AgreementAttachment");
    }

    [Fact]
    public async Task Edit_maps_missing_first_regular_gp_date_to_its_field()
    {
        var dto = new GrantCaseDto
        {
            Id = Guid.NewGuid(),
            Status = GrantCaseStatus.ReturnedForCorrection,
            ApplicationData = new()
            {
                SelectedPositionTypes = [PositionType.RegularGpOrLocum],
                FirstRegularGpOrLocumDate = null
            }
        };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });

        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);
        var error = Assert.Single(model.InitialErrors, item => item.Code == "FirstRegularGpOrLocumDate");

        Assert.Equal("FirstRegularGpOrLocumDate", error.Field);
        Assert.Equal("Oppgi når legen første gang startet som fastlege eller fastlegevikar.", error.Message);
    }

    [Fact]
    public async Task Edit_does_not_map_first_regular_gp_date_when_date_is_present()
    {
        var dto = new GrantCaseDto
        {
            Id = Guid.NewGuid(),
            Status = GrantCaseStatus.ReturnedForCorrection,
            ApplicationData = new()
            {
                SelectedPositionTypes = [PositionType.RegularGpOrLocum],
                FirstRegularGpOrLocumDate = new DateOnly(2020, 1, 1)
            }
        };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto });

        var result = Assert.IsType<ViewResult>(await controller.Edit(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);

        Assert.DoesNotContain(model.InitialErrors, item => item.Code == "FirstRegularGpOrLocumDate");
    }

    [Fact]
    public async Task Submitted_application_can_be_viewed_read_only_by_coordinator()
    {
        var dto = new GrantCaseDto { Id = Guid.NewGuid(), Status = GrantCaseStatus.Submitted, ApplicationData = new() { DoctorName = "Lege" } };
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService { GetByIdResponse = dto })
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateCoordinator() }
        };
        var result = Assert.IsType<ViewResult>(await controller.Application(dto.Id, default));
        var model = Assert.IsType<GrantApplicationFormViewModel>(result.Model);
        Assert.True(model.ReadOnly);
        Assert.Equal("Lege", model.DoctorName);
    }
}
