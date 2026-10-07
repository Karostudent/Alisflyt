using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.ViewModels.GrantCases;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesDetailsCalculationTests
    {
        [Fact]
        public async Task Details_Attaches_Calculation_To_ViewModel()
        {
            var fakeSvc = new FakeGrantCaseApplicationService();
            var dto = new GrantCaseDto
            {
                Id = Guid.NewGuid(),
                CaseNumber = "CASE-1",
                HprNumber = "123",
                Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastModifiedAtUtc = DateTimeOffset.UtcNow,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                    EmploymentPeriods = new System.Collections.Generic.List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                    {
                        new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2023,1,1), FundingFrom = new DateOnly(2023,1,1), FundingThrough = new DateOnly(2023,12,31) }
                    },
                    AbsenceCompensation = 10000m,
                    LearningActivityExpenses = 5000m,
                    SupervisionExpenses = 2000m,
                    IsCentralityGrade6 = true,
                    CentralitySupplementRequestedAmount = 3000m
                }
            };
            fakeSvc.GetByIdResponse = dto;

            // Fake calc service
            var fakeCalc = new FakeGrantCalculationApplicationService();
            fakeCalc.Result = new GrantCalculationDetailsDto { IsAvailable = true, PracticeCompensationAmount = 100, LearningActivitiesAmount = 50 };

            var controller = new GrantCasesController(fakeSvc);
            // inject calc service into HttpContext services for controller usage
            // Setup controller HttpContext with RequestServices
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Alisflyt.Application.Services.IGrantCalculationApplicationService), fakeCalc);
            // Register simple TempData services required by Controller.View()
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory), typeof(TestTempDataFactory));
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, typeof(Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider), typeof(TestTempDataProvider));
            var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
            controller.ControllerContext = new ControllerContext();
            var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = provider };
            // assign via ControllerContext
            controller.ControllerContext.HttpContext = httpContext;

            var result = await controller.Details(dto.Id, default);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GrantCaseDetailsViewModel>(view.Model);

            Assert.NotNull(model.Calculation);
            Assert.True(model.Calculation.IsAvailable);
            Assert.Equal(100m, model.Calculation.PracticeCompensationAmount);
        }

        private class FakeGrantCalculationApplicationService : Alisflyt.Application.Services.IGrantCalculationApplicationService
        {
            public GrantCalculationDetailsDto Result { get; set; } = new GrantCalculationDetailsDto { IsAvailable = false };
            public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, System.Threading.CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Result);
            }
        }

        // Minimal test implementations to satisfy Controller.View() TempData dependency
        private class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new System.Collections.Generic.Dictionary<string, object>();
            public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { }
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
