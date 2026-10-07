using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesCalculationPreviewTests
    {
        [Fact]
        public async Task CalculationPreview_Returns_Partial_With_Available_Calc()
        {
            var fakeSvc = new FakeGrantCaseApplicationService();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = Guid.NewGuid() };
            fakeSvc.GetByIdResponse = dto;

            var fakeCalc = new FakeCalc();
            fakeCalc.Result = new GrantCalculationDetailsDto { IsAvailable = true, PracticeCompensationAmount = 100, LearningActivitiesAmount = 50 };

            var controller = new GrantCasesController(fakeSvc);
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Alisflyt.Application.Services.IGrantCalculationApplicationService), fakeCalc);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory), typeof(TestTempDataFactory));
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider), typeof(TestTempDataProvider));
            var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
            controller.ControllerContext = new ControllerContext();
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = provider };
            controller.ControllerContext.HttpContext = httpContext;

            var result = await controller.CalculationPreview(dto.Id, default);
            var view = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_CalculationResultPartial", view.ViewName);
        }

        [Fact]
        public async Task CalculationPreview_Returns_Partial_With_Unavailable_Calc()
        {
            var fakeSvc = new FakeGrantCaseApplicationService();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = Guid.NewGuid() };
            fakeSvc.GetByIdResponse = dto;

            var fakeCalc = new FakeCalc();
            fakeCalc.Result = new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Ufullstendig søknad" };

            var controller = new GrantCasesController(fakeSvc);
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Alisflyt.Application.Services.IGrantCalculationApplicationService), fakeCalc);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory), typeof(TestTempDataFactory));
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider), typeof(TestTempDataProvider));
            var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
            controller.ControllerContext = new ControllerContext();
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = provider };
            controller.ControllerContext.HttpContext = httpContext;

            var result = await controller.CalculationPreview(dto.Id, default);
            var view = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_CalculationResultPartial", view.ViewName);
        }

        private class FakeCalc : Alisflyt.Application.Services.IGrantCalculationApplicationService
        {
            public GrantCalculationDetailsDto Result { get; set; } = new GrantCalculationDetailsDto { IsAvailable = false };
            public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Result);
        }

        // Minimal test implementations to satisfy Controller.View() TempData dependency
        private class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public System.Collections.Generic.IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new System.Collections.Generic.Dictionary<string, object>();
            public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, System.Collections.Generic.IDictionary<string, object> values) { }
        }

        private class TestTempDataFactory : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory
        {
            public Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary GetTempData(Microsoft.AspNetCore.Http.HttpContext context)
            {
                return new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(context, new TestTempDataProvider());
            }
        }
    }
}
