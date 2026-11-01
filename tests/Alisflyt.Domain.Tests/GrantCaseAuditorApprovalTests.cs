using System;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Domain.Tests;

public class GrantCaseAuditorApprovalTests
{
    [Fact]
    public void MarkReadyForAuditorApproval_changes_status_and_records_time()
    {
        var created = DateTimeOffset.UtcNow;
        var readyAt = created.AddHours(1);
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-1", created);
        grantCase.UpdateDraft("HPR-1", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), created);
        grantCase.Submit(created);
        grantCase.StartReview(created.AddMinutes(1));

        grantCase.MarkReadyForAuditorApproval(readyAt);

        Assert.Equal(GrantCaseStatus.AwaitingAuditorApproval, grantCase.Status);
        Assert.Equal(readyAt, grantCase.ReadyForAuditorApprovalAtUtc);
        Assert.Equal(readyAt, grantCase.LastModifiedAtUtc);
    }

    [Fact]
    public void MarkReadyForAuditorApproval_allows_case_without_guidance_sessions()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateUnderReviewCase(now);

        grantCase.MarkReadyForAuditorApproval(now.AddMinutes(1));

        Assert.Equal(GrantCaseStatus.AwaitingAuditorApproval, grantCase.Status);
    }

    [Theory]
    [InlineData(GuidanceApprovalStatus.Pending)]
    [InlineData(GuidanceApprovalStatus.NeedsCorrection)]
    [InlineData(GuidanceApprovalStatus.Superseded)]
    public void MarkReadyForAuditorApproval_rejects_missing_current_approved_guidance(GuidanceApprovalStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateUnderReviewCaseWithGuidance(now);
        var approval = grantCase.GetCurrentGuidanceApproval()!;
        if (status == GuidanceApprovalStatus.NeedsCorrection)
            approval.RequestCorrection("Korriger veiledningen", now.AddMinutes(1));
        else if (status == GuidanceApprovalStatus.Superseded)
            approval.MarkSuperseded(now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() => grantCase.MarkReadyForAuditorApproval(now.AddMinutes(2)));
        Assert.Equal(GrantCaseStatus.UnderReview, grantCase.Status);
        Assert.Null(grantCase.ReadyForAuditorApprovalAtUtc);
    }

    [Fact]
    public void MarkReadyForAuditorApproval_requires_approval_for_current_guidance_version()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateDraftCaseWithGuidance(now);
        var approval = grantCase.GetCurrentGuidanceApproval()!;
        approval.Approve(now.AddMinutes(1), "veileder-per");

        var changed = CreateApplicationData();
        changed.Certificate.Sessions[0].Hours = 4m;
        grantCase.UpdateApplication(changed, now.AddMinutes(2));
        grantCase.Submit(now.AddMinutes(3));
        grantCase.StartReview(now.AddMinutes(4));

        Assert.Throws<InvalidOperationException>(() => grantCase.MarkReadyForAuditorApproval(now.AddMinutes(5)));
        Assert.Equal(GrantCaseStatus.UnderReview, grantCase.Status);
    }

    [Fact]
    public void MarkReadyForAuditorApproval_allows_approved_current_guidance()
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = CreateUnderReviewCaseWithGuidance(now);
        grantCase.GetCurrentGuidanceApproval()!.Approve(now.AddMinutes(1), "veileder-per");

        grantCase.MarkReadyForAuditorApproval(now.AddMinutes(2));

        Assert.Equal(GrantCaseStatus.AwaitingAuditorApproval, grantCase.Status);
    }

    [Theory]
    [InlineData(GrantCaseStatus.Draft)]
    [InlineData(GrantCaseStatus.Submitted)]
    [InlineData(GrantCaseStatus.ReturnedForCorrection)]
    public void MarkReadyForAuditorApproval_rejects_invalid_statuses(GrantCaseStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-2", now);
        if (status != GrantCaseStatus.Draft)
        {
            grantCase.UpdateDraft("HPR-1", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
            grantCase.Submit(now);
        }
        if (status is GrantCaseStatus.UnderReview or GrantCaseStatus.AwaitingAuditorApproval)
            grantCase.StartReview(now);
        if (status == GrantCaseStatus.ReturnedForCorrection)
        {
            grantCase.StartReview(now);
            grantCase.ReturnForCorrection("fix", now);
        }

        Assert.Throws<InvalidOperationException>(() => grantCase.MarkReadyForAuditorApproval(now.AddMinutes(1)));
    }

    private static GrantCase CreateUnderReviewCase(DateTimeOffset now)
    {
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-AUDITOR", now);
        grantCase.UpdateDraft("HPR-1", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        grantCase.Submit(now.AddMinutes(1));
        grantCase.StartReview(now.AddMinutes(2));
        return grantCase;
    }

    private static GrantCase CreateDraftCaseWithGuidance(DateTimeOffset now)
    {
        var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-GUIDANCE", now);
        grantCase.UpdateApplication(CreateApplicationData(), now.AddMinutes(1));
        grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");
        return grantCase;
    }

    private static GrantCase CreateUnderReviewCaseWithGuidance(DateTimeOffset now)
    {
        var grantCase = CreateDraftCaseWithGuidance(now);
        grantCase.Submit(now.AddMinutes(3));
        grantCase.StartReview(now.AddMinutes(4));
        return grantCase;
    }

    private static GrantApplicationData CreateApplicationData() => new()
    {
        HprNumber = "123456789",
        DoctorName = "Anna Hansen",
        DoctorProfessions = ["Allmennlege"],
        GrantType = GrantType.AlisAgreementIncludingSupervision,
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
            Sessions =
            [new SupervisionSessionInputModel
            {
                Date = new DateOnly(2026, 1, 10),
                Hours = 2m,
                Topic = "Veiledning"
            }]
        }
    };
}
