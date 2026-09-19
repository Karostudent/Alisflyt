using System;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class CoordinatorControllerTests
    {
        [Fact]
        public async Task CoordinatorController_Details_ReturnsNotFound_When_Service_Throws_KeyNotFoundException()
        {
            var fake = new FakeGrantCaseApplicationService { GetByIdException = new KeyNotFoundException() };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.Details(Guid.NewGuid(), default);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
