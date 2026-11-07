using System;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Alisflyt.Web.ViewModels.Coordinator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Alisflyt.Web.Tests;

public sealed class CoordinatorCalculationErrorTests
{
    [Fact]
    public async Task Details_Shows_General_Error_And_Logs_When_Calculation_Fails()
    {
        var id = Guid.NewGuid();
        var service = new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto
            {
                Id = id,
                CaseNumber = "CASE-COORDINATOR-ERROR",
                HprNumber = "123456789",
                Status = GrantCaseStatus.UnderReview,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastModifiedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var logger = new TestLogger<CoordinatorController>();
        var controller = CoordinatorTestFactory.Create(service);
        var controllerWithLogger = new CoordinatorController(service, logger)
        {
            ControllerContext = controller.ControllerContext,
            TempData = controller.TempData
        };
        var services = new ServiceCollection();
        services.AddSingleton<IGrantCalculationApplicationService>(new FailingCalculationService());
        controllerWithLogger.HttpContext.RequestServices = services.BuildServiceProvider();

        var result = Assert.IsType<ViewResult>(await controllerWithLogger.Details(id, default));
        var model = Assert.IsType<CoordinatorCaseDetailsViewModel>(result.Model);

        Assert.Null(model.Calculation);
        Assert.Equal("Beregningen kunne ikke lastes på grunn av en teknisk feil.", controllerWithLogger.ViewData["CalculationError"]);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(nameof(CoordinatorController.Details), entry.State);
        Assert.Contains(id.ToString(), entry.State);
        Assert.NotNull(entry.Exception);
        Assert.DoesNotContain("123456789", entry.State);
    }

    private sealed class FailingCalculationService : IGrantCalculationApplicationService
    {
        public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("internal calculation failure");
    }
}
