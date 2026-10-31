using System.Collections.Generic;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Alisflyt.Web.Tests;

public class CoordinatorCentralityTests
{
    [Fact]
    public async Task SaveCentrality_shows_database_error_and_logs_without_success()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CENTRALITY-ERROR", now);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Lege"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2025, 1, 1),
            ExpectedCompletionDate = new DateOnly(2030, 1, 1),
            ConfirmsAlisAgreement = true,
            ConfirmsOfficialAgreementTemplate = true,
            ConfirmsNoCommercialAgencyAffiliation = true,
            AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
            FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 80m,
                    EmploymentStartDate = new DateOnly(2025, 1, 1),
                    FundingFrom = new DateOnly(2026, 1, 1),
                    FundingThrough = new DateOnly(2026, 6, 30)
                }
            ]
        }, now);
        var repository = new InMemoryGrantCaseRepository(grantCase)
        {
            SaveException = new Microsoft.EntityFrameworkCore.DbUpdateException("save failed")
        };
        var logger = new TestLogger<CoordinatorController>();
        var baseController = CoordinatorTestFactory.Create(new FakeGrantCaseApplicationService());
        var controller = new CoordinatorController(new FakeGrantCaseApplicationService(), logger)
        {
            ControllerContext = baseController.ControllerContext,
            TempData = baseController.TempData
        };

        var result = await controller.SaveCentrality(grantCase.Id, true, 1000m, repository, TimeProvider.System, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Endringen kunne ikke lagres. Prøv igjen.", controller.TempData!["Error"]);
        Assert.DoesNotContain("Success", controller.TempData.Keys);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(grantCase.Id.ToString(), entry.State);
        Assert.DoesNotContain("123456789", entry.State);
    }

    [Fact]
    public async Task SaveCentrality_still_rejects_direct_changes_after_case_is_ready_for_auditor()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CENTRALITY-LOCKED", now);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Lege"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2025, 1, 1),
            ExpectedCompletionDate = new DateOnly(2030, 1, 1),
            ConfirmsAlisAgreement = true,
            ConfirmsOfficialAgreementTemplate = true,
            ConfirmsNoCommercialAgencyAffiliation = true,
            AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
            FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 80m,
                    EmploymentStartDate = new DateOnly(2025, 1, 1),
                    FundingFrom = new DateOnly(2026, 1, 1),
                    FundingThrough = new DateOnly(2026, 6, 30)
                }
            ]
        }, now);
        grantCase.UpdateCentrality(true, 1000m, now);
        grantCase.Submit(now.AddMinutes(1));
        grantCase.StartReview(now.AddMinutes(2));
        grantCase.MarkReadyForAuditorApproval(now.AddMinutes(3));
        var repository = new InMemoryGrantCaseRepository(grantCase);
        var controller = CoordinatorTestFactory.Create(new FakeGrantCaseApplicationService());

        var result = await controller.SaveCentrality(
            grantCase.Id,
            false,
            2000m,
            repository,
            TimeProvider.System,
            default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(GrantCaseStatus.AwaitingAuditorApproval, grantCase.Status);
        Assert.Equal("Saken kan ikke redigeres i denne statusen.", controller.TempData["Error"]);
    }

    private sealed class InMemoryGrantCaseRepository : IGrantCaseRepository
    {
        private readonly GrantCase _grantCase;
        public Exception? SaveException { get; set; }

        public InMemoryGrantCaseRepository(GrantCase grantCase) => _grantCase = grantCase;

        public Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<GrantCase?>(id == _grantCase.Id ? _grantCase : null);

        public Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GrantCase>>([_grantCase]);

        public Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddGuidanceApprovalAsync(GuidanceApproval approval, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (SaveException is not null)
                throw SaveException;
            return Task.CompletedTask;
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
