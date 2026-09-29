using Alisflyt.Application.Models;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;

namespace Alisflyt.Application.Services;

public class GrantCalculationService
{
    public GrantCalculationResult Calculate(
        GrantCalculationInput input,
        GrantRateSet rateSet)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rateSet);

        var practiceCompensation =
            CalculatePracticeCompensation(input, rateSet);
        var learningActivities =
            CalculateLearningActivities(input, rateSet);
        var productivity =
            CalculateProductivity(input, rateSet);
        var guidance =
            CalculateGuidance(input, rateSet);
        var standardElements =
            practiceCompensation
            + learningActivities
            + productivity
            + guidance;

        var facilitation =
            standardElements * rateSet.FacilitationRate;
        
        var centralitySupplement =
            CalculateCentralitySupplement(input, rateSet);

        return new GrantCalculationResult
        {
            PracticeCompensationAmount = practiceCompensation,
            LearningActivitiesAmount = learningActivities,
            ProductivityAmount = productivity,
            GuidanceAmount = guidance,
            FacilitationAmount = facilitation,
            CentralitySupplementAmount = centralitySupplement
        };
    }

    private static decimal CalculatePracticeCompensation(
        GrantCalculationInput input,
        GrantRateSet rateSet)
    {
        decimal maximumAmount = 0m;

        foreach (var period in input.EmploymentPeriods)
        {
            // Praksiskompensasjon gjelder ikke allmennlege utenfor FLO.
            if (period.PositionType ==
                PositionType.GeneralPractitionerOutsideRegularGpScheme)
            {
                continue;
            }

            var wholeMonths = CountWholeMonths(
                period.FundingFrom,
                period.FundingThrough);

            if (wholeMonths <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                wholeMonths / 12m;

            var annualMaximum =
                rateSet.SalaryRate
                * rateSet.PracticeCompensationRate
                * rateSet.PracticeCompensationMaxHours;

            maximumAmount +=
                annualMaximum
                * positionFraction
                * periodFraction;
        }

        return Math.Min(
            input.AbsenceCompensation,
            maximumAmount);
    }

    private static decimal CalculateLearningActivities(
    GrantCalculationInput input,
    GrantRateSet rateSet)
    {
        decimal maximumAmount = 0m;

        foreach (var period in input.EmploymentPeriods)
        {
            var wholeMonths = CountWholeMonths(
                period.FundingFrom,
                period.FundingThrough);

            if (wholeMonths <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                wholeMonths / 12m;

            maximumAmount +=
                rateSet.LearningActivitiesMaxAmount
                * positionFraction
                * periodFraction;
        }

        return Math.Min(
            input.LearningActivityExpenses,
            maximumAmount);
    }

    private static decimal CalculateProductivity(
    GrantCalculationInput input,
    GrantRateSet rateSet)
    {
        if (!input.FirstRegularGpOrLocumDate.HasValue)
        {
            return 0m;
        }

        decimal amount = 0m;

        foreach (var period in input.EmploymentPeriods)
        {
            if (period.PositionType != PositionType.RegularGpOrLocum)
            {
                continue;
            }

            var eligibilityPeriod =
                ProductivityEligibilityCalculator.Calculate(
                    input.FirstRegularGpOrLocumDate.Value,
                    rateSet.ProductivityEligibilityMonths,
                    period.FundingFrom,
                    period.FundingThrough);

            if (eligibilityPeriod is null)
            {
                continue;
            }

            var wholeMonths = CountWholeMonths(
                eligibilityPeriod.From,
                eligibilityPeriod.Through);

            if (wholeMonths <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                wholeMonths / 12m;

            amount +=
                rateSet.ProductivityMaxAmount
                * positionFraction
                * periodFraction;
        }

        return amount;
    }

    private static decimal CalculateGuidance(
    GrantCalculationInput input,
    GrantRateSet rateSet)
    {
        decimal maximumAmount = 0m;

        foreach (var period in input.EmploymentPeriods)
        {
            var wholeMonths = CountWholeMonths(
                period.FundingFrom,
                period.FundingThrough);

            if (wholeMonths <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                wholeMonths / 12m;

            var annualMaximum =
                rateSet.SalaryRate
                * rateSet.GuidanceRate
                * rateSet.GuidanceHoursPerYear;

            maximumAmount +=
                annualMaximum
                * positionFraction
                * periodFraction;
        }

        return Math.Min(
            input.SupervisionExpenses,
            maximumAmount);
    }
    private static decimal CalculateCentralitySupplement(
    GrantCalculationInput input,
    GrantRateSet rateSet)
    {
        if (!input.IsCentralityGrade6)
        {
            return 0m;
        }

        if (input.GrantType != GrantType.AlisAgreementIncludingSupervision)
        {
            return 0m;
        }

        decimal maximumAmount = 0m;

        foreach (var period in input.EmploymentPeriods)
        {
            var wholeMonths = CountWholeMonths(
                period.FundingFrom,
                period.FundingThrough);

            if (wholeMonths <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                wholeMonths / 12m;

            maximumAmount +=
                rateSet.CentralitySupplementMaxAmount
                * positionFraction
                * periodFraction;
        }

        return Math.Min(
            input.CentralitySupplementRequestedAmount,
            maximumAmount);
    }

    private static int CountWholeMonths(
        DateOnly from,
        DateOnly through)
    {
        if (through < from)
        {
            throw new ArgumentException(
                "Funding through cannot be before funding from.");
        }

        var months =
            (through.Year - from.Year) * 12
            + through.Month
            - from.Month;

        if (through.Day >= from.Day)
        {
            months++;
        }

        return months;
    }
}