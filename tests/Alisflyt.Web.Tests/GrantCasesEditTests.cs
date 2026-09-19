using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesEditTests
    {
        [Fact]
        public async Task GrantCasesController_EditGet_MapsDraftToEditViewModel()
        {
            var id = Guid.Parse("55555555-5555-5555-5555-555555555555");
            var dto = new GrantCaseDto { Id = id, CaseNumber = "CN5", HprNumber = "H5", EmploymentPercentage = 60m, EmploymentStartDate = new DateOnly(2026,2,1), EmploymentEndDate = new DateOnly(2026,12,1), Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Edit(id, default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.EditGrantCaseViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
            Assert.Equal(dto.HprNumber, model.HprNumber);
            Assert.Equal(dto.EmploymentPercentage, model.EmploymentPercentage);
            Assert.Equal(dto.EmploymentStartDate, model.EmploymentStartDate);
            Assert.Equal(dto.EmploymentEndDate, model.EmploymentEndDate);
            Assert.Single(fake.GetByIdCalls);
            Assert.Equal(id, fake.GetByIdCalls[0].id);
        }

        [Fact]
        public async Task GrantCasesController_EditGet_ReturnsBadRequest_WhenCaseIsNotDraft()
        {
            var id = Guid.Parse("66666666-6666-6666-6666-666666666666");
            var dto = new GrantCaseDto { Id = id, CaseNumber = "CN6", HprNumber = "H6", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Edit(id, default);

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task GrantCasesController_EditPost_DoesNotCallService_WhenModelStateIsInvalid()
        {
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);
            ctrl.ModelState.AddModelError("HprNumber", "Required");

            var model = new Alisflyt.Web.ViewModels.GrantCases.EditGrantCaseViewModel { Id = Guid.NewGuid() };

            var result = await ctrl.Edit(model, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.EditGrantCaseViewModel>(view.Model);
            Assert.Empty(fake.UpdateDraftCalls);
        }

        [Fact]
        public async Task GrantCasesController_EditPost_MapsRequestAndRedirectsToDetails_WhenModelIsValid()
        {
            var id = Guid.Parse("77777777-7777-7777-7777-777777777777");
            var updatedDto = new GrantCaseDto { Id = id, CaseNumber = "CN7", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { UpdateDraftResponse = updatedDto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var model = new Alisflyt.Web.ViewModels.GrantCases.EditGrantCaseViewModel { Id = id, HprNumber = "HPR7", EmploymentPercentage = 80m, EmploymentStartDate = new DateOnly(2026,3,1), EmploymentEndDate = new DateOnly(2026,9,1) };

            var result = await ctrl.Edit(model, default);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(id, redirect.RouteValues!["id"]);
            Assert.Single(fake.UpdateDraftCalls);
            var call = fake.UpdateDraftCalls[0].request;
            Assert.Equal(model.HprNumber, call.HprNumber);
            Assert.Equal(model.EmploymentPercentage, call.EmploymentPercentage);
            Assert.Equal(model.EmploymentStartDate, call.EmploymentStartDate);
            Assert.Equal(model.EmploymentEndDate, call.EmploymentEndDate);
        }
    }
}
