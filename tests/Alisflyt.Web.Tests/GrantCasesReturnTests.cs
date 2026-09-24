using System;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesReturnTests
    {
        [Fact]
        public async Task GrantCases_Details_Maps_ReturnFields()
        {
            var id = Guid.NewGuid();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, CaseNumber = "CN-1", Status = Alisflyt.Domain.Enums.GrantCaseStatus.ReturnedForCorrection, ReturnReason = "Fix this", ReturnedAtUtc = DateTimeOffset.Parse("2026-01-02T03:04:05Z"), CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Details(id, default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.GrantCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.ReturnReason, model.ReturnReason);
            Assert.Equal(dto.ReturnedAtUtc, model.ReturnedAtUtc);
        }

        [Fact]
        public async Task GrantCases_EditGet_Allows_ReturnedForCorrection()
        {
            var id = Guid.NewGuid();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, HprNumber = "H", Status = Alisflyt.Domain.Enums.GrantCaseStatus.ReturnedForCorrection, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Edit(id, default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.GrantApplicationFormViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
        }

        [Fact]
        public async Task GrantCases_SaveApplication_Allows_ReturnedForCorrection()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService { UpdateDraftResponse = new Alisflyt.Application.Models.GrantCaseDto { Id = id } };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake) { Url = new GrantCaseUrlHelper() };

            var model = new Alisflyt.Web.ViewModels.GrantCases.GrantApplicationFormViewModel { Id = id, HprNumber = "H" };

            var result = await ctrl.SaveApplication(model, default);

            Assert.IsType<OkObjectResult>(result);
            Assert.Single(fake.UpdateDraftCalls);
        }

        [Fact]
        public async Task GrantCases_Submit_From_ReturnedForCorrection_RedirectsOnSuccess()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Submit(id, default);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Single(fake.SubmitCalls);
        }
    }
}
