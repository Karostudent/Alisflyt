using System;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Web.Tests.Fakes;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class PortalControllerTests
    {
        [Fact]
        public void PortalController_Index_ReturnsExpectedRoleEntries()
        {
            var ctrl = new Alisflyt.Web.Controllers.PortalController();
            ctrl.Url = new FakeUrlHelper();

            var result = ctrl.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Portals.PortalEntryViewModel>(view.Model);
            Assert.Contains("DEMOVERSJON", model.DemoNotice);
            Assert.Collection(model.Roles,
                r => Assert.Equal("ALIS", r.Label),
                r => Assert.Equal("Koordinator", r.Label),
                r => Assert.Equal("Veileder", r.Label));
            Assert.Contains(model.Roles, r => r.Url == "/Alis/Index");
            Assert.Contains(model.Roles, r => r.Url == "/Coordinator/Index");
            Assert.Contains(model.Roles, r => r.Url == "/Guidance/Index");
        }
    }
}
