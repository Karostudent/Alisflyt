using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GuidanceControllerTests
    {
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
