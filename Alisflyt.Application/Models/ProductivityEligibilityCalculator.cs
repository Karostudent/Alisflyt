using System;
using System.Collections.Generic;
using System.Text;

namespace Alisflyt.Application.Models;

public static class ProductivityEligibilityCalculator
{
    public static ProductivityEligibilityPeriod? Calculate(
        DateOnly firstRegularGpOrLocumDate,
        int eligibilityMonths,
        DateOnly fundingFrom,
        DateOnly fundingThrough)
    {
        if (eligibilityMonths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eligibilityMonths));
        }

        if (fundingThrough < fundingFrom)
        {
            throw new ArgumentException(
                "Funding through cannot be before funding from.",
                nameof(fundingThrough));
        }

        var eligibilityFrom = firstRegularGpOrLocumDate;

        var eligibilityThrough =
            firstRegularGpOrLocumDate
                .AddMonths(eligibilityMonths)
                .AddDays(-1);

        var overlapFrom =
            fundingFrom > eligibilityFrom
                ? fundingFrom
                : eligibilityFrom;

        var overlapThrough =
            fundingThrough < eligibilityThrough
                ? fundingThrough
                : eligibilityThrough;

        if (overlapThrough < overlapFrom)
        {
            return null;
        }

        return new ProductivityEligibilityPeriod(
            overlapFrom,
            overlapThrough);
    }
}