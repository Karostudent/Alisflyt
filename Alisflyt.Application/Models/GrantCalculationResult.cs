using System;
using System.Collections.Generic;
using System.Text;

namespace Alisflyt.Application.Models;

public sealed class GrantCalculationResult
{
    public decimal PracticeCompensationAmount { get; init; }

    public decimal LearningActivitiesAmount { get; init; }

    public decimal ProductivityAmount { get; init; }

    public decimal GuidanceAmount { get; init; }

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
