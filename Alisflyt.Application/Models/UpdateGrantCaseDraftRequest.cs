using System;

namespace Alisflyt.Application.Models
{
    public sealed class UpdateGrantCaseDraftRequest
    {
        public Alisflyt.Domain.Forms.GrantApplicationData? ApplicationData { get; init; }
        public string? HprNumber { get; init; }
        public decimal? EmploymentPercentage { get; init; }
        public DateOnly? EmploymentStartDate { get; init; }
        public DateOnly? EmploymentEndDate { get; init; }
    }
}
