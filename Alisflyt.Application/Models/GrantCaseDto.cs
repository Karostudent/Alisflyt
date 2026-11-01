using System;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Application.Models
{
    public sealed class GuidanceCorrectionResult
    {
        public GuidanceCorrectionResult(GrantCaseDto grantCase, bool createdNewApproval)
        {
            GrantCase = grantCase ?? throw new ArgumentNullException(nameof(grantCase));
            CreatedNewApproval = createdNewApproval;
        }

        public GrantCaseDto GrantCase { get; }
        public bool CreatedNewApproval { get; }
    }

    public sealed class GrantCaseDto
    {
        public Alisflyt.Domain.Forms.GrantApplicationData? ApplicationData { get; init; }
        public Guid Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public string? HprNumber { get; init; }
        public decimal? EmploymentPercentage { get; init; }
        public DateOnly? EmploymentStartDate { get; init; }
        public DateOnly? EmploymentEndDate { get; init; }
        public GrantCaseStatus Status { get; init; }
        public string? ReturnReason { get; init; }
        public DateTimeOffset? ReturnedAtUtc { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset LastModifiedAtUtc { get; init; }
        public DateTimeOffset? ReadyForAuditorApprovalAtUtc { get; init; }
        public bool GuidanceApprovalRequired { get; init; }
        public bool GuidanceApprovalApproved { get; init; }
    }
}
