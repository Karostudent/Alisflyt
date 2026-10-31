using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
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
        public async Task CoordinatorController_MarkReadyForAuditorApproval_Post_Redirects_OnSuccess()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = CreateControllerForRole(fake, "Koordinator");

            var result = await ctrl.MarkReadyForAuditorApproval(id, default);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Single(fake.MarkReadyForAuditorApprovalCalls);
            Assert.Equal(id, fake.MarkReadyForAuditorApprovalCalls[0].id);
        }

        [Theory]
        [InlineData("ALIS")]
        [InlineData("Veileder")]
        [InlineData(null)]
        public async Task CoordinatorController_MarkReadyForAuditorApproval_Post_Forbids_non_coordinators_before_service(string? role)
        {
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = CreateControllerForRole(fake, role);

            var result = await ctrl.MarkReadyForAuditorApproval(Guid.NewGuid(), default);

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(fake.MarkReadyForAuditorApprovalCalls);
        }

        [Fact]
        public async Task CoordinatorController_MarkReadyForAuditorApproval_Post_ShowsError_OnDomainFailure()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService { MarkReadyForAuditorApprovalException = new InvalidOperationException() };
            var ctrl = CreateControllerForRole(fake, "Koordinator");

            var result = await ctrl.MarkReadyForAuditorApproval(id, default);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Saken kan ikke markeres som klar for revisorkontroll i gjeldende status.", ctrl.TempData!["Error"]);
        }

        private static Alisflyt.Web.Controllers.CoordinatorController CreateControllerForRole(FakeGrantCaseApplicationService fake, string? role)
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

            var controller = new Alisflyt.Web.Controllers.CoordinatorController(fake)
            {
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
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

    internal sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    // No helper throwing fakes remain; tests use configured exception properties on the existing FakeGrantCaseApplicationService.
}
