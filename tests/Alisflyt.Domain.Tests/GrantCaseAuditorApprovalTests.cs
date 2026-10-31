using System;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
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
}
