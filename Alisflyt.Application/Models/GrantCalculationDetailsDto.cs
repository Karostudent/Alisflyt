using System;

namespace Alisflyt.Application.Models
{
    public sealed class GrantCalculationDetailsDto
    {
        public IReadOnlyList<GrantCalculationAuditRow> AuditRows { get; init; } = [];
        public bool IsAvailable { get; init; }
        public string? UnavailableReason { get; init; }
        public string? CentralityExplanation { get; init; }

        public decimal PracticeCompensationAmount { get; init; }
        public decimal PracticeCompensationMaximumAmount { get; init; }
        public bool IsPracticeCompensationCapped { get; init; }
        public decimal LearningActivitiesAmount { get; init; }
        public decimal ProductivityAmount { get; init; }
        public decimal GuidanceAmount { get; init; }
        public decimal RegisteredGuidanceHours { get; init; }
        public decimal GuidancePaidHours { get; init; }
        public decimal GuidanceHourlyRate { get; init; }
        public decimal GuidanceHonorariumAmount { get; init; }
        public decimal GuidanceHonorariumMaximumAmount { get; init; }
        public decimal GuidanceHonorariumCappedAmount { get; init; }
        public Alisflyt.Domain.Enums.GuidanceApprovalStatus? GuidanceHonorariumApprovalStatus { get; init; }
        public decimal FacilitationAmount { get; init; }
        public decimal CentralitySupplementAmount { get; init; }

        public decimal TotalAmount =>
            PracticeCompensationAmount
            + LearningActivitiesAmount
            + ProductivityAmount
            + GuidanceAmount
            + FacilitationAmount
            + CentralitySupplementAmount;

        public Guid? RateSetId { get; init; }
        public string? RateSetName { get; init; }
        public DateOnly? RateSetValidFrom { get; init; }
        public DateOnly? RateSetValidTo { get; init; }
    }
}
