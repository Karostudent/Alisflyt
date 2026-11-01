using System;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Web.ViewModels.GrantCases
{
    public sealed class GrantCaseListItemViewModel
    {
        public Guid Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public string? HprNumber { get; init; }
        public GrantCaseStatus Status { get; init; }
        public GuidanceApprovalStatus? GuidanceApprovalStatus { get; init; }
        public string? GuidanceCorrectionReason { get; init; }
        public bool NeedsGuidanceCorrection => GuidanceApprovalStatus == Alisflyt.Domain.Enums.GuidanceApprovalStatus.NeedsCorrection
            && Status is GrantCaseStatus.Submitted or GrantCaseStatus.UnderReview;
        public DateTimeOffset LastModifiedAtUtc { get; init; }
    }
}
