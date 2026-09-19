using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesCreateTests
    {
        [Fact]
        public void GrantCasesController_CreateGet_ReturnsEmptyCreateViewModel()
        {
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = ctrl.Create();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.CreateGrantCaseViewModel>(view.Model);
            Assert.Null(model.HprNumber);
            Assert.Null(model.EmploymentPercentage);
            Assert.Null(model.EmploymentStartDate);
            Assert.Null(model.EmploymentEndDate);
        }

        [Fact]
        public async Task GrantCasesController_CreatePost_DoesNotCallService_WhenModelStateIsInvalid()
        {
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);
            ctrl.ModelState.AddModelError("HprNumber", "Required");

            var model = new Alisflyt.Web.ViewModels.GrantCases.CreateGrantCaseViewModel { HprNumber = null };

            var result = await ctrl.Create(model, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.CreateGrantCaseViewModel>(view.Model);
            Assert.Empty(fake.CreateDraftCalls);
        }

        [Fact]
        public async Task GrantCasesController_CreatePost_MapsRequestAndRedirectsToDetails_WhenModelIsValid()
        {
            var id = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var createdDto = new GrantCaseDto { Id = id, CaseNumber = "CN4", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { CreateDraftResponse = createdDto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var model = new Alisflyt.Web.ViewModels.GrantCases.CreateGrantCaseViewModel { HprNumber = "HPR4", EmploymentPercentage = 20m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,6,1) };

            var result = await ctrl.Create(model, default);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(id, redirect.RouteValues!["id"]);
            Assert.Single(fake.CreateDraftCalls);
            var call = fake.CreateDraftCalls[0].request;
            Assert.Equal(model.HprNumber, call.HprNumber);
            Assert.Equal(model.EmploymentPercentage, call.EmploymentPercentage);
            Assert.Equal(model.EmploymentStartDate, call.EmploymentStartDate);
            Assert.Equal(model.EmploymentEndDate, call.EmploymentEndDate);
        }
    }
}
