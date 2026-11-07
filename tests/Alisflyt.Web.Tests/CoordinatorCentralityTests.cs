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
            Attachments = [new ApplicationAttachment { Kind = "Agreement", FileName = "avtale.pdf", ContentBase64 = Convert.ToBase64String("agreement"u8.ToArray()) }],
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
        grantCase.Submit(now.AddMinutes(1));
        grantCase.StartReview(now.AddMinutes(2));
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
    public async Task SaveCentrality_rejects_direct_changes_while_case_is_submitted_without_persisting()
    {
        var grantCase = CreateSubmittedCase();
        var originalData = grantCase.ApplicationDataJson;
        var repository = new InMemoryGrantCaseRepository(grantCase);
        var controller = CoordinatorTestFactory.Create(new FakeGrantCaseApplicationService());

        var result = await controller.SaveCentrality(
            grantCase.Id, true, 2000m, repository, TimeProvider.System, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(GrantCaseStatus.Submitted, grantCase.Status);
        Assert.Equal(originalData, grantCase.ApplicationDataJson);
        Assert.Equal("Saken kan ikke redigeres i denne statusen.", controller.TempData["Error"]);
    }

    [Fact]
    public async Task SaveCentrality_marks_missing_grade6_as_group_error_and_preserves_input()
    {
        var grantCase = CreateSubmittedCase();
        grantCase.StartReview(DateTimeOffset.UtcNow);
        var controller = CoordinatorTestFactory.Create(new FakeGrantCaseApplicationService());

        var result = await controller.SaveCentrality(
            grantCase.Id, null, 1250m, new InMemoryGrantCaseRepository(grantCase), TimeProvider.System, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(controller.TempData!["Error"]);
        Assert.Equal("1250", controller.TempData["CentralityAmount"]);
        Assert.Equal("Velg Ja eller Nei for sentralitetsgrad 6.", controller.TempData["CentralityGrade6Error"]);
    }

    [Fact]
    public async Task SaveCentrality_marks_missing_amount_when_grade6_is_selected()
    {
        var grantCase = CreateSubmittedCase();
        grantCase.StartReview(DateTimeOffset.UtcNow);
        var controller = CoordinatorTestFactory.Create(new FakeGrantCaseApplicationService());

        var result = await controller.SaveCentrality(
            grantCase.Id, true, null, new InMemoryGrantCaseRepository(grantCase), TimeProvider.System, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Beløpet er påkrevd når sentralitetsgrad 6 er Ja.", controller.TempData!["CentralityAmountError"]);
        Assert.Null(controller.TempData["Error"]);
    }

    [Fact]
    public async Task SaveCentrality_updates_values_during_review()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateSubmittedCase();
        grantCase.StartReview(now);
        var repository = new InMemoryGrantCaseRepository(grantCase);
        var controller = CoordinatorTestFactory.Create(new FakeGrantCaseApplicationService());

        var result = await controller.SaveCentrality(
            grantCase.Id, true, 2000m, repository, TimeProvider.System, default);

        Assert.IsType<RedirectToActionResult>(result);
        var data = System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson!)!;
        Assert.True(data.IsCentralityGrade6);
        Assert.Equal(2000m, data.CentralitySupplementRequestedAmount);
        Assert.Null(controller.TempData["Error"]);
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
            Attachments = [new ApplicationAttachment { Kind = "Agreement", FileName = "avtale.pdf", ContentBase64 = Convert.ToBase64String("agreement"u8.ToArray()) }],
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
        grantCase.Submit(now.AddMinutes(1));
        grantCase.StartReview(now.AddMinutes(2));
        grantCase.UpdateCentrality(true, 1000m, now.AddMinutes(2));
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

    private static GrantCase CreateSubmittedCase()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CENTRALITY-SUBMITTED", now);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Lege"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            Attachments = [new ApplicationAttachment { Kind = "Agreement", FileName = "avtale.pdf", ContentBase64 = Convert.ToBase64String("agreement"u8.ToArray()) }],
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
        grantCase.Submit(now.AddMinutes(1));
        return grantCase;
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

        public Task<IReadOnlyList<Alisflyt.Application.Models.GrantCaseListItemDto>> ListOverviewAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Alisflyt.Application.Models.GrantCaseListItemDto>>(Array.Empty<Alisflyt.Application.Models.GrantCaseListItemDto>());

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
