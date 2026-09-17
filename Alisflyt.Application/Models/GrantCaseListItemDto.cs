using System;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Application.Models
{
    public sealed class GrantCaseListItemDto
    {
        public Guid Id { get; init; }
        public string CaseNumber { get; init; } = string.Empty;
        public string? HprNumber { get; init; }
        public GrantCaseStatus Status { get; init; }
        public DateTimeOffset LastModifiedAtUtc { get; init; }
    }
}
