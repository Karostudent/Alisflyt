using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Enums;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public class CoordinatorExcelExportTests
{
    [Theory]
    [InlineData(GrantCaseStatus.Draft)]
    [InlineData(GrantCaseStatus.Submitted)]
    [InlineData(GrantCaseStatus.ReturnedForCorrection)]
    public async Task Export_requires_started_review(GrantCaseStatus status)
    {
        var controller = new CoordinatorController(new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto { Status = status }
        });
        Assert.IsType<BadRequestObjectResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));
    }

    [Fact]
    public async Task Export_returns_download_for_case_under_review()
    {
        var controller = new CoordinatorController(new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto { Status = GrantCaseStatus.UnderReview }
        });
        var result = Assert.IsType<FileContentResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));
        Assert.EndsWith(".xlsx", result.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.NotEmpty(result.FileContents);
    }

    [Fact]
    public async Task Missing_case_returns_not_found()
    {
        var controller = new CoordinatorController(new FakeGrantCaseApplicationService { GetByIdException = new KeyNotFoundException() });
        Assert.IsType<NotFoundResult>(await controller.ExportExcel(Guid.NewGuid(), new Calculation(), TimeProvider.System, default));
    }

    private sealed class Calculation : IGrantCalculationApplicationService
    {
        public Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
            => Task.FromResult(new GrantCalculationDetailsDto { IsAvailable = true });
    }
}
