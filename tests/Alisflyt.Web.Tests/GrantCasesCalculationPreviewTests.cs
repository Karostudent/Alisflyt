using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = Guid.NewGuid(), HprNumber = "123456789" };
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
            var httpContext = GrantCasesTestContext.CreateAlis();
            httpContext.RequestServices = provider;
            controller.ControllerContext.HttpContext = httpContext;

            var result = await controller.CalculationPreview(dto.Id, default);
            var view = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_CalculationResultPartial", view.ViewName);
        }

        [Fact]
        public async Task CalculationPreview_Returns_Partial_With_Unavailable_Calc()
        {
            var fakeSvc = new FakeGrantCaseApplicationService();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = Guid.NewGuid(), HprNumber = "123456789" };
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
            var httpContext = GrantCasesTestContext.CreateAlis();
            httpContext.RequestServices = provider;
            controller.ControllerContext.HttpContext = httpContext;

            var result = await controller.CalculationPreview(dto.Id, default);
            var view = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_CalculationResultPartial", view.ViewName);
        }

        [Fact]
        public async Task CalculationPreview_rejects_anonymous_before_calculation_service()
        {
            var fakeSvc = new FakeGrantCaseApplicationService();
            var fakeCalc = new FakeCalc();
            var controller = new GrantCasesController(fakeSvc)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                    {
                        RequestServices = CreateServiceProvider(fakeCalc)
                    }
                }
            };

            var result = await controller.CalculationPreview(Guid.NewGuid(), default);

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
            Assert.Empty(fakeSvc.GetByIdCalls);
            Assert.Equal(0, fakeCalc.CallCount);
        }

        [Fact]
        public async Task CalculationPreview_allows_assigned_supervisor()
        {
            var fakeSvc = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new GrantCaseDto { HprNumber = "123456789" }
            };
            var fakeCalc = new FakeCalc { Result = new GrantCalculationDetailsDto { IsAvailable = true } };
            var httpContext = GrantCasesTestContext.CreateSupervisor();
            httpContext.RequestServices = CreateServiceProvider(fakeCalc);
            var controller = new GrantCasesController(fakeSvc)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.CalculationPreview(Guid.NewGuid(), default);

            Assert.IsType<PartialViewResult>(result);
            Assert.Equal(1, fakeCalc.CallCount);
        }

        [Fact]
        public async Task CalculationPreview_rejects_unassigned_supervisor_before_calculation_service()
        {
            var fakeSvc = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new GrantCaseDto { HprNumber = "999999999" }
            };
            var fakeCalc = new FakeCalc();
            var httpContext = GrantCasesTestContext.CreateSupervisor();
            httpContext.RequestServices = CreateServiceProvider(fakeCalc);
            var controller = new GrantCasesController(fakeSvc)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.CalculationPreview(Guid.NewGuid(), default);

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
            Assert.Equal(0, fakeCalc.CallCount);
        }

        [Fact]
        public async Task CalculationPreview_rejects_another_alis_before_calculation_service()
        {
            var fakeSvc = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new GrantCaseDto { HprNumber = "999999999" }
            };
            var fakeCalc = new FakeCalc();
            var httpContext = GrantCasesTestContext.CreateAlis();
            httpContext.RequestServices = CreateServiceProvider(fakeCalc);
            var controller = new GrantCasesController(fakeSvc)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.CalculationPreview(Guid.NewGuid(), default);

            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
            Assert.Equal(1, fakeSvc.GetByIdCalls.Count);
            Assert.Equal(0, fakeCalc.CallCount);
        }

        private static IServiceProvider CreateServiceProvider(FakeCalc fakeCalc)
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            services.AddSingleton<Alisflyt.Application.Services.IGrantCalculationApplicationService>(fakeCalc);
            services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory, TestTempDataFactory>();
            services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider, TestTempDataProvider>();
            return services.BuildServiceProvider();
        }

        private class FakeCalc : Alisflyt.Application.Services.IGrantCalculationApplicationService
        {
            public GrantCalculationDetailsDto Result { get; set; } = new GrantCalculationDetailsDto { IsAvailable = false };
            public int CallCount { get; private set; }
            public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, System.Threading.CancellationToken cancellationToken = default)
            {
                CallCount++;
                return Task.FromResult(Result);
            }
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
