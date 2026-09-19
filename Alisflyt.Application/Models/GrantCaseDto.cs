using System;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Application.Models
{
    public sealed class GrantCaseDto
    {
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
    }
}
