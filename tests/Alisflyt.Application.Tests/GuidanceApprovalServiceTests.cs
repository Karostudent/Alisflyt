using System;
using System.Threading.Tasks;
using Alisflyt.Application.Services;
using Alisflyt.Application.Tests.Fakes;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Application.Tests;

public class GuidanceApprovalServiceTests
{
    [Fact]
    public async Task RequestApprovalAsync_creates_pending_guidance_approval_for_current_attestation()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-201", now);
        grantCase.UpdateApplication(CreateApplicationData(), now.AddMinutes(1));

        var repository = new FakeGrantCaseRepository();
        await repository.AddAsync(grantCase);

        var service = new GuidanceApprovalService(repository, new FakeTimeProvider(now.AddMinutes(2)));
        var approval = await service.RequestApprovalAsync(grantCase.Id, "123456789", "987654321", "veileder-per", "Per Olsen");

        Assert.Equal(GuidanceApprovalStatus.Pending, approval.Status);
        Assert.Equal(grantCase.Id, approval.GrantCaseId);
        Assert.Equal(1, approval.VersionNumber);
        Assert.Equal("veileder-per", approval.SupervisorUserKey);
    }

    [Fact]
    public async Task ApproveAsync_marks_approval_as_approved_and_sets_legacy_flag()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-202", now);
        grantCase.UpdateApplication(CreateApplicationData(), now.AddMinutes(1));

        var repository = new FakeGrantCaseRepository();
        await repository.AddAsync(grantCase);

        var service = new GuidanceApprovalService(repository, new FakeTimeProvider(now.AddMinutes(2)));
        var approval = await service.RequestApprovalAsync(grantCase.Id, "123456789", "987654321", "veileder-per", "Per Olsen");

        var approved = await service.ApproveAsync(grantCase.Id, approval.Id, "veileder-per");

        Assert.Equal(GuidanceApprovalStatus.Approved, approved.Status);
        Assert.Equal("veileder-per", approved.ApprovedBy);
        Assert.Equal(now.AddMinutes(2), approved.ApprovedAtUtc);
        Assert.Equal(now.AddMinutes(2), grantCase.ApplicationDataJson is null ? default : JsonPayloadData(grantCase).SupervisorApprovedAtUtc);
    }

    [Fact]
    public async Task ApproveAsync_rejects_wrong_supervisor_user_key()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-203", now);
        grantCase.UpdateApplication(CreateApplicationData(), now.AddMinutes(1));

        var repository = new FakeGrantCaseRepository();
        await repository.AddAsync(grantCase);

        var service = new GuidanceApprovalService(repository, new FakeTimeProvider(now.AddMinutes(2)));
        var approval = await service.RequestApprovalAsync(grantCase.Id, "123456789", "987654321", "veileder-per", "Per Olsen");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(grantCase.Id, approval.Id, "wrong-user"));
    }

    [Fact]
    public async Task RequestCorrectionAsync_requires_reason_and_marks_needs_correction()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-204", now);
        grantCase.UpdateApplication(CreateApplicationData(), now.AddMinutes(1));

        var repository = new FakeGrantCaseRepository();
        await repository.AddAsync(grantCase);

        var service = new GuidanceApprovalService(repository, new FakeTimeProvider(now.AddMinutes(2)));
        var approval = await service.RequestApprovalAsync(grantCase.Id, "123456789", "987654321", "veileder-per", "Per Olsen");

        await Assert.ThrowsAsync<ArgumentException>(() => service.RequestCorrectionAsync(grantCase.Id, approval.Id, "   "));

        var corrected = await service.RequestCorrectionAsync(grantCase.Id, approval.Id, "Timer må etterkontrolleres.");
        Assert.Equal(GuidanceApprovalStatus.NeedsCorrection, corrected.Status);
        Assert.Equal("Timer må etterkontrolleres.", corrected.RequestedRevisionReason);
    }

    [Fact]
    public async Task RequestApprovalAsync_when_attestation_changes_creates_new_version_and_keeps_history()
    {
        var now = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-205", now);
        grantCase.UpdateApplication(CreateApplicationData(), now.AddMinutes(1));

        var repository = new FakeGrantCaseRepository();
        await repository.AddAsync(grantCase);

        var service = new GuidanceApprovalService(repository, new FakeTimeProvider(now.AddMinutes(2)));
        var first = await service.RequestApprovalAsync(grantCase.Id, "123456789", "987654321", "veileder-per", "Per Olsen");
        await service.ApproveAsync(grantCase.Id, first.Id, "veileder-per");

        var changed = CreateApplicationData();
        changed.Certificate.Sessions[0].Hours = 4m;
        grantCase.UpdateApplication(changed, now.AddMinutes(3));

        var next = await service.RequestApprovalAsync(grantCase.Id, "123456789", "987654321", "veileder-per", "Per Olsen");

        Assert.Equal(2, next.VersionNumber);
        Assert.Equal(GuidanceApprovalStatus.Pending, next.Status);
        Assert.Equal(GuidanceApprovalStatus.Superseded, first.Status);
        Assert.Equal(2, grantCase.GuidanceApprovals.Count);
    }

    private static GrantApplicationData CreateApplicationData()
    {
        return new GrantApplicationData
        {
            HprNumber = "123456789",
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Allmennlege"],
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 50m,
                    EmploymentStartDate = new DateOnly(2026, 1, 1),
                    FundingFrom = new DateOnly(2026, 1, 1),
                    FundingThrough = new DateOnly(2026, 12, 31)
                }
            ],
            Certificate = new SupervisionCertificateViewModel
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                DoctorSigningPlace = "Oslo",
                SupervisorSigningPlace = "Oslo",
                DoctorSigningDate = new DateOnly(2026, 1, 2),
                SupervisorSigningDate = new DateOnly(2026, 1, 3),
                Sessions =
                [
                    new SupervisionSessionInputModel
                    {
                        Date = new DateOnly(2026, 1, 10),
                        Hours = 2m,
                        Topic = "Veiledning"
                    }
                ]
            },
            Attachments = [],
            ProductivityRequestedAmount = 0m,
            AbsenceCompensation = 0m,
            LearningActivityExpenses = 0m,
            SupervisionExpenses = 100m,
            HasAdditionalSupervisionCosts = false,
            AdditionalSupervisionCosts = 0m,
            IsCentralityGrade6 = false,
            CentralitySupplementRequestedAmount = 0m
        };
    }

    private static GrantApplicationData JsonPayloadData(GrantCase grantCase)
    {
        return System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson!)!;
    }
}
