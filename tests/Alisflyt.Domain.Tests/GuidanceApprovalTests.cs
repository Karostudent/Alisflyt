using System;
using System.Text.Json;
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

    [Theory]
    [InlineData(GrantCaseStatus.Submitted)]
    [InlineData(GrantCaseStatus.UnderReview)]
    public void UpdateGuidanceSessions_allows_correction_for_submitted_or_under_review_case(GrantCaseStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateCaseWithCorrectionRequested(status, now);
        var sessions = new List<SupervisionSessionInputModel>
        {
            new() { Date = new DateOnly(2026, 2, 10), Hours = 4m, Topic = "Korrigert veiledning" }
        };

        var replacement = grantCase.UpdateGuidanceSessions(sessions, now.AddMinutes(5));

        Assert.Equal(GuidanceApprovalStatus.Superseded, grantCase.GuidanceApprovals.Single(a => a.VersionNumber == 1).Status);
        Assert.Equal(GuidanceApprovalStatus.Pending, replacement.Status);
        Assert.Equal(2, replacement.VersionNumber);
        Assert.Equal("veileder-per", replacement.SupervisorUserKey);
        Assert.Equal("987654321", replacement.SupervisorHprNumber);
        Assert.Equal("Per Olsen", replacement.SupervisorName);
        Assert.Equal(status, grantCase.Status);
        Assert.Equal(2, grantCase.GuidanceApprovals.Count);
    }

    [Fact]
    public void UpdateGuidanceSessions_changes_only_sessions_and_preserves_case_data()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateCaseWithCorrectionRequested(GrantCaseStatus.Submitted, now);
        var id = grantCase.Id;
        var caseNumber = grantCase.CaseNumber;
        var before = JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson!)!;

        grantCase.UpdateGuidanceSessions(
            [new SupervisionSessionInputModel { Date = new DateOnly(2026, 2, 10), Hours = 4m, Topic = "Korrigert veiledning" }],
            now.AddMinutes(5));

        var after = JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson!)!;
        Assert.Equal(id, grantCase.Id);
        Assert.Equal(caseNumber, grantCase.CaseNumber);
        Assert.Equal(GrantCaseStatus.Submitted, grantCase.Status);
        Assert.Equal(before.HprNumber, after.HprNumber);
        Assert.Equal(before.DoctorName, after.DoctorName);
        Assert.Equal(before.GrantType, after.GrantType);
        Assert.Equal(before.EmploymentPeriods[0].PositionPercentage, after.EmploymentPeriods[0].PositionPercentage);
        Assert.Equal(before.Attachments.Count, after.Attachments.Count);
        Assert.Equal(before.Certificate.SupervisorHprNumber, after.Certificate.SupervisorHprNumber);
        Assert.Equal(before.Certificate.SupervisorName, after.Certificate.SupervisorName);
        Assert.Equal(4m, after.Certificate.Sessions[0].Hours);
        Assert.Equal("Korrigert veiledning", after.Certificate.Sessions[0].Topic);
    }

    [Fact]
    public void UpdateGuidanceSessions_rejects_awaiting_auditor_approval()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateCaseWithCorrectionRequested(GrantCaseStatus.UnderReview, now);
        grantCase.GetCurrentGuidanceApproval()!.Approve(now.AddMinutes(5), "veileder-per");
        grantCase.MarkReadyForAuditorApproval(now.AddMinutes(6));
        grantCase.GetCurrentGuidanceApproval()!.RequestCorrection("Ny korrigering", now.AddMinutes(7));

        Assert.Throws<InvalidOperationException>(() => grantCase.UpdateGuidanceSessions(
            [new SupervisionSessionInputModel { Date = new DateOnly(2026, 2, 10), Hours = 4m, Topic = "Korrigert veiledning" }],
            now.AddMinutes(8)));
        Assert.Equal(GrantCaseStatus.AwaitingAuditorApproval, grantCase.Status);
    }

    [Theory]
    [InlineData(GuidanceApprovalStatus.Pending)]
    [InlineData(GuidanceApprovalStatus.Approved)]
    public void UpdateGuidanceSessions_rejects_when_current_approval_is_not_needs_correction(GuidanceApprovalStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateSubmittedCaseWithGuidance(now);
        var approval = grantCase.GetCurrentGuidanceApproval()!;
        if (status == GuidanceApprovalStatus.Approved)
            approval.Approve(now.AddMinutes(4), "veileder-per");

        Assert.Throws<InvalidOperationException>(() => grantCase.UpdateGuidanceSessions(
            [new SupervisionSessionInputModel { Date = new DateOnly(2026, 2, 10), Hours = 4m, Topic = "Korrigert veiledning" }],
            now.AddMinutes(5)));
        Assert.Equal(1, grantCase.GuidanceApprovals.Count);
    }

    [Fact]
    public void UpdateGuidanceSessions_with_unchanged_snapshot_does_not_create_duplicate_approval()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateCaseWithCorrectionRequested(GrantCaseStatus.Submitted, now);
        var current = grantCase.GetCurrentGuidanceApproval()!;
        var sessions = JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson!)!.Certificate.Sessions;

        var result = grantCase.UpdateGuidanceSessions(sessions, now.AddMinutes(5));

        Assert.Same(current, result);
        Assert.Equal(GuidanceApprovalStatus.NeedsCorrection, result.Status);
        Assert.Single(grantCase.GuidanceApprovals);
    }

    [Fact]
    public void Returned_case_with_unchanged_guidance_reuses_existing_approval()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateSubmittedCaseWithGuidance(now);
        var approval = grantCase.GetCurrentGuidanceApproval()!;
        approval.Approve(now.AddMinutes(4), "veileder-per");
        grantCase.StartReview(now.AddMinutes(5));
        grantCase.ReturnForCorrection("Korriger andre opplysninger.", now.AddMinutes(6));

        var requested = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(7), "Per Olsen");

        Assert.Same(approval, requested);
        Assert.Equal(GuidanceApprovalStatus.Approved, requested.Status);
        Assert.Single(grantCase.GuidanceApprovals);
    }

    [Fact]
    public void Returned_case_with_changed_guidance_creates_one_pending_version_and_blocks_finalization()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateSubmittedCaseWithGuidance(now);
        var oldApproval = grantCase.GetCurrentGuidanceApproval()!;
        oldApproval.Approve(now.AddMinutes(4), "veileder-per");
        grantCase.StartReview(now.AddMinutes(5));
        grantCase.ReturnForCorrection("Korriger veiledningen.", now.AddMinutes(6));

        var changed = CreateValidApplicationData();
        changed.Certificate.Sessions.Add(new SupervisionSessionInputModel
        {
            Date = new DateOnly(2026, 2, 10),
            Hours = 1m,
            Topic = "Ny veiledning"
        });
        grantCase.UpdateApplication(changed, now.AddMinutes(7));
        var replacement = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(8), "Per Olsen");

        Assert.Equal(GuidanceApprovalStatus.Superseded, oldApproval.Status);
        Assert.Equal(GuidanceApprovalStatus.Pending, replacement.Status);
        Assert.Equal(2, grantCase.GuidanceApprovals.Count);
        Assert.Throws<InvalidOperationException>(() => grantCase.MarkReadyForAuditorApproval(now.AddMinutes(9)));
    }

    [Fact]
    public void UpdateGuidanceSessions_makes_old_approval_insufficient_for_auditor_finalization()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateCaseWithCorrectionRequested(GrantCaseStatus.UnderReview, now);
        var oldApproval = grantCase.GetCurrentGuidanceApproval()!;

        grantCase.UpdateGuidanceSessions(
            [new SupervisionSessionInputModel { Date = new DateOnly(2026, 2, 10), Hours = 4m, Topic = "Korrigert veiledning" }],
            now.AddMinutes(5));

        Assert.Equal(GuidanceApprovalStatus.Superseded, oldApproval.Status);
        Assert.Throws<InvalidOperationException>(() => grantCase.MarkReadyForAuditorApproval(now.AddMinutes(6)));
        Assert.Equal(GrantCaseStatus.UnderReview, grantCase.Status);
    }

    private static GrantCase CreateCaseWithCorrectionRequested(GrantCaseStatus status, DateTimeOffset now)
    {
        var grantCase = CreateSubmittedCaseWithGuidance(now);
        if (status == GrantCaseStatus.UnderReview)
            grantCase.StartReview(now.AddMinutes(4));
        grantCase.GetCurrentGuidanceApproval()!.RequestCorrection("Korriger veiledningen.", now.AddMinutes(5));
        return grantCase;
    }

    private static GrantCase CreateSubmittedCaseWithGuidance(DateTimeOffset now)
    {
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-CORRECTION", now);
        grantCase.UpdateApplication(CreateValidApplicationData(), now.AddMinutes(1));
        grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");
        grantCase.Submit(now.AddMinutes(3));
        return grantCase;
    }

    private static GrantApplicationData CreateValidApplicationData()
    {
        return new GrantApplicationData
        {
            HprNumber = "123456789",
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Allmennlege"],
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2025, 1, 1),
            ExpectedCompletionDate = new DateOnly(2028, 1, 1),
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
