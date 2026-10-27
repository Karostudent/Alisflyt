using System;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Domain.Tests;

public class GuidanceApprovalTests
{
    [Fact]
    public void Create_sets_pending_status_and_metadata()
    {
        var now = DateTimeOffset.UtcNow;
        var approval = GuidanceApproval.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "123456789",
            "987654321",
            "veileder-per",
            1,
            "hash-version-1",
            now,
            "Per Olsen",
            "{ \"sessions\": 3 }");

        Assert.Equal(GuidanceApprovalStatus.Pending, approval.Status);
        Assert.Equal("123456789", approval.DoctorHprNumber);
        Assert.Equal("987654321", approval.SupervisorHprNumber);
        Assert.Equal("veileder-per", approval.SupervisorUserKey);
        Assert.Equal(1, approval.VersionNumber);
        Assert.Equal("hash-version-1", approval.AttestationSnapshotHash);
        Assert.Equal(now, approval.CreatedAtUtc);
        Assert.Equal(now, approval.UpdatedAtUtc);
    }

    [Fact]
    public void Approve_sets_approval_timestamp_and_approver()
    {
        var now = DateTimeOffset.UtcNow;
        var approval = GuidanceApproval.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "123456789",
            "987654321",
            "veileder-per",
            1,
            "hash-version-1",
            now);

        approval.Approve(now.AddMinutes(5), "veileder-per");

        Assert.Equal(GuidanceApprovalStatus.Approved, approval.Status);
        Assert.Equal(now.AddMinutes(5), approval.ApprovedAtUtc);
        Assert.Equal("veileder-per", approval.ApprovedBy);
    }

    [Fact]
    public void RequestCorrection_sets_reason_and_needs_correction_status()
    {
        var now = DateTimeOffset.UtcNow;
        var approval = GuidanceApproval.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "123456789",
            "987654321",
            "veileder-per",
            1,
            "hash-version-1",
            now);

        approval.RequestCorrection("Timer for uke 12 er korrigert.", now.AddMinutes(2));

        Assert.Equal(GuidanceApprovalStatus.NeedsCorrection, approval.Status);
        Assert.Equal("Timer for uke 12 er korrigert.", approval.RequestedRevisionReason);
        Assert.Equal(now.AddMinutes(2), approval.UpdatedAtUtc);
    }

    [Fact]
    public void MarkSuperseded_marks_old_version_as_replaced()
    {
        var now = DateTimeOffset.UtcNow;
        var approval = GuidanceApproval.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "123456789",
            "987654321",
            "veileder-per",
            1,
            "hash-version-1",
            now);

        approval.MarkSuperseded(now.AddMinutes(3));

        Assert.Equal(GuidanceApprovalStatus.Superseded, approval.Status);
        Assert.Equal(now.AddMinutes(3), approval.UpdatedAtUtc);
    }

    [Fact]
    public void MatchesVersion_requires_same_version_and_snapshot_hash()
    {
        var approval = GuidanceApproval.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "123456789",
            "987654321",
            "veileder-per",
            2,
            "hash-version-2",
            DateTimeOffset.UtcNow);

        Assert.True(approval.MatchesVersion(2, "hash-version-2"));
        Assert.False(approval.MatchesVersion(2, "hash-version-3"));
        Assert.False(approval.MatchesVersion(3, "hash-version-2"));
    }

    [Fact]
    public void RequestCorrection_rejects_empty_reason()
    {
        var approval = GuidanceApproval.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "123456789",
            "987654321",
            "veileder-per",
            1,
            "hash-version-1",
            DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => approval.RequestCorrection("   ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void GrantCase_request_guidance_approval_creates_versioned_pending_record()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-100", now);
        grantCase.UpdateApplication(CreateValidApplicationData(), now.AddMinutes(1));

        var approval = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");

        Assert.Equal(1, approval.VersionNumber);
        Assert.Equal(GuidanceApprovalStatus.Pending, approval.Status);
        Assert.Equal("veileder-per", approval.SupervisorUserKey);
        Assert.Equal(1, grantCase.GuidanceApprovals.Count);
    }

    [Fact]
    public void GrantCase_update_application_invalidates_previous_guidance_approval_for_new_attestation_version()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-101", now);
        var original = CreateValidApplicationData();
        grantCase.UpdateApplication(original, now.AddMinutes(1));

        var firstApproval = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");
        firstApproval.Approve(now.AddMinutes(3), "veileder-per");

        var changed = CreateValidApplicationData();
        changed.Certificate.Sessions[0].Hours = 4m;
        grantCase.UpdateApplication(changed, now.AddMinutes(4));

        Assert.Equal(GuidanceApprovalStatus.Superseded, firstApproval.Status);
        Assert.Null(grantCase.GetCurrentGuidanceApproval());

        var replacementApproval = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(5), "Per Olsen");
        Assert.Equal(2, replacementApproval.VersionNumber);
        Assert.Equal(GuidanceApprovalStatus.Pending, replacementApproval.Status);
    }

    private static GrantApplicationData CreateValidApplicationData()
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
}
