using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.ViewModels.GrantCases;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            var sessionContext = GrantCasesTestContext.CreateAlis("123");
            httpContext.Session = sessionContext.Session;
            // assign via ControllerContext
            controller.ControllerContext.HttpContext = httpContext;

            var result = await controller.Details(dto.Id, default);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GrantCaseDetailsViewModel>(view.Model);

            Assert.NotNull(model.Calculation);
            Assert.True(model.Calculation.IsAvailable);
            Assert.Equal(100m, model.Calculation.PracticeCompensationAmount);
        }

        [Fact]
        public async Task Details_Preserves_Approved_Guidance_Status_When_Calculation_Is_Attached()
        {
            var id = Guid.NewGuid();
            var fakeSvc = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new GrantCaseDto
                {
                    Id = id,
                    CaseNumber = "CASE-APPROVED-GUIDANCE",
                    HprNumber = "123",
                    Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    LastModifiedAtUtc = DateTimeOffset.UtcNow,
                    GuidanceApprovalRequired = true,
                    GuidanceApprovalApproved = true,
                    GuidanceApprovalStatus = Alisflyt.Domain.Enums.GuidanceApprovalStatus.Approved,
                    ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                    {
                        Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                        {
                            Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel()]
                        }
                    }
                }
            };
            var controller = new GrantCasesController(fakeSvc)
            {
                ControllerContext = new ControllerContext()
            };
            var services = new ServiceCollection();
            services.AddSingleton<IGrantCalculationApplicationService>(new FakeGrantCalculationApplicationService
            {
                Result = new GrantCalculationDetailsDto { IsAvailable = true }
            });
            services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory, TestTempDataFactory>();
            services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider, TestTempDataProvider>();
            var httpContext = GrantCasesTestContext.CreateAlis("123");
            httpContext.RequestServices = services.BuildServiceProvider();
            controller.ControllerContext.HttpContext = httpContext;

            var result = Assert.IsType<ViewResult>(await controller.Details(id, default));
            var model = Assert.IsType<GrantCaseDetailsViewModel>(result.Model);

            Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Approved, model.GuidanceApprovalStatus);
            Assert.True(model.GuidanceApprovalRequired);
            Assert.True(model.GuidanceApprovalApproved);
        }

        [Fact]
        public async Task Details_Shows_General_Error_And_Logs_When_Calculation_Fails()
        {
            var id = Guid.NewGuid();
            var fakeSvc = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new GrantCaseDto
                {
                    Id = id,
                    CaseNumber = "CASE-ERROR",
                    HprNumber = "123",
                    Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    LastModifiedAtUtc = DateTimeOffset.UtcNow
                }
            };
            var logger = new TestLogger<GrantCasesController>();
            var fakeCalc = new FakeGrantCalculationApplicationService { Exception = new InvalidOperationException("internal calculation failure") };
            var controller = new GrantCasesController(fakeSvc, logger: logger)
            {
                ControllerContext = new ControllerContext()
            };
            var services = new ServiceCollection();
            services.AddSingleton<Alisflyt.Application.Services.IGrantCalculationApplicationService>(fakeCalc);
            services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory, TestTempDataFactory>();
            services.AddSingleton<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider, TestTempDataProvider>();
            var httpContext = GrantCasesTestContext.CreateAlis("123");
            httpContext.RequestServices = services.BuildServiceProvider();
            controller.ControllerContext.HttpContext = httpContext;

            var result = Assert.IsType<ViewResult>(await controller.Details(id, default));
            var model = Assert.IsType<GrantCaseDetailsViewModel>(result.Model);

            Assert.Null(model.Calculation);
            Assert.Equal("Beregningen kunne ikke lastes på grunn av en teknisk feil.", controller.ViewData["CalculationError"]);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Contains(nameof(GrantCasesController.Details), entry.State);
            Assert.Contains(id.ToString(), entry.State);
            Assert.NotNull(entry.Exception);
            Assert.DoesNotContain("123", entry.State);
        }

        private class FakeGrantCalculationApplicationService : Alisflyt.Application.Services.IGrantCalculationApplicationService
        {
            public GrantCalculationDetailsDto Result { get; set; } = new GrantCalculationDetailsDto { IsAvailable = false };
            public Exception? Exception { get; set; }
            public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, System.Threading.CancellationToken cancellationToken = default)
            {
                if (Exception is not null) throw Exception;
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
