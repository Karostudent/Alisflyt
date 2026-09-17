using System;

namespace Alisflyt.Application.Models
{
    public sealed class CreateGrantCaseRequest
    {
        public string? HprNumber { get; init; }
        public decimal? EmploymentPercentage { get; init; }
        public DateOnly? EmploymentStartDate { get; init; }
        public DateOnly? EmploymentEndDate { get; init; }
    }
}
