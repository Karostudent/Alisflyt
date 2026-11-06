using System;
using System.Collections.Generic;
using System.Text;

namespace Alisflyt.Application.Models;

public sealed class GrantCalculationResult
{
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

    public decimal FacilitationAmount { get; init; }

    public decimal CentralitySupplementAmount { get; init; }
    public string? CentralityExplanation { get; init; }

    public decimal TotalAmount =>
        PracticeCompensationAmount
        + LearningActivitiesAmount
        + ProductivityAmount
        + GuidanceAmount
        + FacilitationAmount
        + CentralitySupplementAmount;
}
