using System;

namespace Alisflyt.Application.Models
{
    public sealed class GrantCalculationDetailsDto
    {
        public bool IsAvailable { get; init; }
        public string? UnavailableReason { get; init; }

        public decimal PracticeCompensationAmount { get; init; }
        public decimal LearningActivitiesAmount { get; init; }
        public decimal ProductivityAmount { get; init; }
        public decimal GuidanceAmount { get; init; }
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
