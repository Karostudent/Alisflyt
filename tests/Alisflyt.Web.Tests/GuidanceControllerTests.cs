using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GuidanceControllerTests
    {
        [Theory]
        [InlineData("All", 2)]
        [InlineData("Draft", 1)]
        [InlineData("Unknown", 2)]
        public async Task Applications_filters_cases(string status, int count)
        {
            var fake = new FakeGrantCaseApplicationService { ListResponse = new[] {
                new Alisflyt.Application.Models.GrantCaseListItemDto { Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft },
                new Alisflyt.Application.Models.GrantCaseListItemDto { Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted }
            } };
            var controller = new Alisflyt.Web.Controllers.GuidanceController();
            var result = Assert.IsType<ViewResult>(await controller.Applications(fake, status));
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.GuidanceApplicationsViewModel>(result.Model);
            Assert.Equal(count, model.Cases.Count);
            Assert.Equal(status == "Unknown" ? "All" : status, model.Filter);
        }

        [Fact]
        public void GuidanceController_Index_ReturnsGuidanceDashboardViewModel()
        {
            var ctrl = new Alisflyt.Web.Controllers.GuidanceController();

            var result = ctrl.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.GuidanceDashboardViewModel>(view.Model);
            Assert.Contains("Veilederfunksjonalitet kommer snart", model.Message);
        }
    }
}
