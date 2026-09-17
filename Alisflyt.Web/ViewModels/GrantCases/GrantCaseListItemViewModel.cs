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
        public DateTimeOffset LastModifiedAtUtc { get; init; }
    }
}
