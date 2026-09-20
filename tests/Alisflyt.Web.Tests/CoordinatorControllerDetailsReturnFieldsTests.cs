using System;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class CoordinatorControllerDetailsReturnFieldsTests
    {
        [Fact]
        public async Task CoordinatorController_Details_Maps_ReturnReason_And_ReturnedAtUtc()
        {
            var id = Guid.NewGuid();
            var returnedAt = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, CaseNumber = "CN-R", Status = Alisflyt.Domain.Enums.GrantCaseStatus.ReturnedForCorrection, ReturnReason = "Feil i periode", ReturnedAtUtc = returnedAt, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.Details(id, default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Coordinator.CoordinatorCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.ReturnReason, model.ReturnReason);
            Assert.Equal(dto.ReturnedAtUtc, model.ReturnedAtUtc);
        }
    }
}
