using System;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class CoordinatorControllerReturnTests
    {
        [Fact]
        public async Task CoordinatorController_StartReview_Post_Redirects_OnSuccess()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.StartReview(id, default);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(id, redirect.RouteValues!["id"]);
            Assert.Single(fake.StartReviewCalls);
            Assert.Equal(id, fake.StartReviewCalls[0].id);
        }

        [Fact]
        public async Task CoordinatorController_StartReview_Post_NotFound_When_Service_Throws()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService { StartReviewException = new KeyNotFoundException() };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.StartReview(id, default);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task CoordinatorController_ReturnForCorrection_Get_MapsViewModel()
        {
            var id = Guid.NewGuid();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, CaseNumber = "CN-1", Status = Alisflyt.Domain.Enums.GrantCaseStatus.UnderReview, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.ReturnForCorrection(id, default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Coordinator.ReturnForCorrectionViewModel>(view.Model);
            Assert.Equal(id, model.Id);
            Assert.Equal(dto.CaseNumber, model.CaseNumber);
        }

        [Fact]
        public async Task CoordinatorController_ReturnForCorrection_Post_InvalidModel_DoesNotCallService()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);
            ctrl.ModelState.AddModelError("Reason", "Required");

            var vm = new Alisflyt.Web.ViewModels.Coordinator.ReturnForCorrectionViewModel { Id = id };

            var result = await ctrl.ReturnForCorrection(vm, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Empty(fake.ReturnForCorrectionCalls);
        }

        [Fact]
        public async Task CoordinatorController_ReturnForCorrection_Post_Valid_CallsService_AndRedirects()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var vm = new Alisflyt.Web.ViewModels.Coordinator.ReturnForCorrectionViewModel { Id = id, Reason = "Please fix" };

            var result = await ctrl.ReturnForCorrection(vm, default);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Single(fake.ReturnForCorrectionCalls);
            Assert.Equal(id, fake.ReturnForCorrectionCalls[0].id);
            Assert.Equal("Please fix", fake.ReturnForCorrectionCalls[0].request.Reason);
        }

        [Fact]
        public async Task CoordinatorController_ReturnForCorrection_Post_ServiceDomainError_ShowsModelError()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService { ReturnForCorrectionException = new InvalidOperationException() };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var vm = new Alisflyt.Web.ViewModels.Coordinator.ReturnForCorrectionViewModel { Id = id, Reason = "x" };

            var result = await ctrl.ReturnForCorrection(vm, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.True(ctrl.ModelState.ErrorCount > 0);
        }
    }

    // No helper throwing fakes remain; tests use configured exception properties on the existing FakeGrantCaseApplicationService.
}
