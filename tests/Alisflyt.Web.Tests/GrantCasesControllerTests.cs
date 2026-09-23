using System;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesControllerTests
    {
        [Fact]
        public async Task GrantCasesController_Details_ReturnsNotFound_When_Service_Throws_KeyNotFoundException()
        {
            var fake = new FakeGrantCaseApplicationService { GetByIdException = new KeyNotFoundException() };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Details(Guid.NewGuid(), default);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GrantCasesController_EditGet_ReturnsNotFound_When_Service_Throws_KeyNotFoundException()
        {
            var fake = new FakeGrantCaseApplicationService { GetByIdException = new KeyNotFoundException() };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Edit(Guid.NewGuid(), default);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GrantCasesController_SaveApplication_ReturnsNotFound_When_UpdateDraftAsync_Throws_KeyNotFoundException()
        {
            var fake = new FakeGrantCaseApplicationService { UpdateDraftException = new KeyNotFoundException() };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var model = new Alisflyt.Web.ViewModels.GrantCases.GrantApplicationFormViewModel { Id = Guid.NewGuid() };

            var result = await ctrl.SaveApplication(model, default);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GrantCasesController_Submit_ReturnsNotFound_When_Service_Throws_KeyNotFoundException()
        {
            var fake = new FakeGrantCaseApplicationService { SubmitException = new KeyNotFoundException() };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Submit(Guid.NewGuid(), default);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
