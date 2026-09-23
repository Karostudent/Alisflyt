using System.Text.Json;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Alisflyt.Web.ViewModels.GrantCases;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Tests;

public class GrantCasesSaveTests
{
    [Fact]
    public void Create_uses_shared_view_with_empty_form()
    {
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService());
        var view = Assert.IsType<ViewResult>(controller.Create());
        Assert.Equal("Application", view.ViewName);
        var model = Assert.IsType<GrantApplicationFormViewModel>(view.Model);
        Assert.Equal(Guid.Empty, model.Id);
        Assert.Null(model.HprNumber);
        Assert.Empty(model.EmploymentPeriods);
        Assert.Empty(model.Certificate.Sessions);
        Assert.False(model.ReadOnly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_preserves_both_steps_and_returns_navigation_links(bool existing)
    {
        var savedId = Guid.NewGuid();
        var dto = new GrantCaseDto { Id = savedId };
        var fake = new FakeGrantCaseApplicationService { CreateDraftResponse = dto, UpdateDraftResponse = dto };
        var controller = new GrantCasesController(fake) { Url = new GrantCaseUrlHelper() };
        var model = new GrantApplicationFormViewModel { Id = existing ? savedId : Guid.Empty, HprNumber = "1234567",
            EmploymentPeriods = [new() { PositionPercentage = 50 }, new() { PositionPercentage = 25 }],
            Certificate = new() { Sessions = [new() { Hours = 1.5m, Topic = "Tema" }] } };
        var result = Assert.IsType<OkObjectResult>(await controller.SaveApplication(model, default));
        if (existing)
        {
            var call = Assert.Single(fake.UpdateDraftCalls);
            Assert.Equal(savedId, call.id);
            Assert.Same(model, call.request.ApplicationData);
            Assert.Empty(fake.CreateDraftCalls);
        }
        else
        {
            Assert.Same(model, Assert.Single(fake.CreateDraftCalls).request.ApplicationData);
            Assert.Empty(fake.UpdateDraftCalls);
        }
        var response = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(savedId, response.GetProperty("id").GetGuid());
        Assert.Equal($"/GrantCases/Edit/{savedId}", response.GetProperty("editUrl").GetString());
        Assert.Equal($"/GrantCases/Details/{savedId}", response.GetProperty("detailsUrl").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_model_is_not_saved(bool existing)
    {
        var fake = new FakeGrantCaseApplicationService();
        var controller = new GrantCasesController(fake);
        controller.ModelState.AddModelError("Certificate.Sessions[0].Hours", "Ugyldig antall timer");
        Assert.IsType<BadRequestObjectResult>(await controller.SaveApplication(new() { Id = existing ? Guid.NewGuid() : Guid.Empty }, default));
        Assert.Empty(fake.CreateDraftCalls);
        Assert.Empty(fake.UpdateDraftCalls);
    }

    [Fact]
    public async Task Submitted_case_cannot_be_edited_or_saved()
    {
        var fake = new FakeGrantCaseApplicationService { GetByIdResponse = new() { Status = GrantCaseStatus.Submitted }, UpdateDraftException = new InvalidOperationException() };
        var controller = new GrantCasesController(fake);
        var id = Guid.NewGuid();
        Assert.IsType<BadRequestResult>(await controller.Edit(id, default));
        Assert.IsType<ConflictObjectResult>(await controller.SaveApplication(new() { Id = id }, default));
    }
}
