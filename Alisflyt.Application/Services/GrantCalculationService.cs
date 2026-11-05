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

        var isAlisAgreement =
            input.GrantType == GrantType.AlisAgreementIncludingSupervision;
        var practiceCompensation = isAlisAgreement
            ? CalculatePracticeCompensation(input, rateSet)
            : (Amount: 0m, Maximum: 0m);
        var learningActivities = isAlisAgreement
            ? CalculateLearningActivities(input, rateSet)
            : 0m;
        var productivity = isAlisAgreement
            ? CalculateProductivity(input, rateSet)
            : 0m;
        var guidance =
            CalculateGuidance(input, rateSet);
        var standardElements =
            practiceCompensation.Amount
            + learningActivities
            + productivity
            + guidance;

        var facilitation = isAlisAgreement
            ? standardElements * rateSet.FacilitationRate
            : 0m;

        var centralitySupplement =
            CalculateCentralitySupplement(input, rateSet);

        return new GrantCalculationResult
        {
            PracticeCompensationAmount = practiceCompensation.Amount,
            PracticeCompensationMaximumAmount = practiceCompensation.Maximum,
            IsPracticeCompensationCapped = isAlisAgreement
                && input.AbsenceCompensation > practiceCompensation.Maximum,
            LearningActivitiesAmount = learningActivities,
            ProductivityAmount = productivity,
            GuidanceAmount = guidance,
            FacilitationAmount = facilitation,
            CentralitySupplementAmount = centralitySupplement,
            CentralityExplanation = DetermineCentralityExplanation(input, centralitySupplement)
        };
    }

    private static string? DetermineCentralityExplanation(GrantCalculationInput input, decimal centralitySupplement)
    {
        // Mirror the same conditions used by CalculateCentralitySupplement
        if (!input.IsCentralityGrade6)
            return "Sentralitetstillegg er ikke aktuelt fordi kommunen ikke er registrert med sentralitetsgrad 6.";

        if (input.GrantType != Domain.Forms.GrantType.AlisAgreementIncludingSupervision)
            return "Sentralitetstillegg er bare aktuelt ved tilskudd til ALIS-avtale inkl. veiledning.";

        if (input.CentralitySupplementRequestedAmount <= 0)
            return "Sentralitetstillegg kan ikke beregnes fordi søkt beløp ikke er oppgitt.";

        // If supplement computed to a positive value, provide a short explanation
        if (centralitySupplement > 0)
        {
            if (input.CentralitySupplementRequestedAmount >= centralitySupplement)
            {
                // Requested amount used (or equal to capped amount)
                return "Sentralitetstillegg basert på søkt beløp eller maksgrense for perioden.";
            }
            else
            {
                // Capped by maximum
                return "Sentralitetstillegg er begrenset av maksimalgrensen for perioden.";
            }
        }

        // Otherwise no specific explanation available here
        return null;
    }

    private static (decimal Amount, decimal Maximum) CalculatePracticeCompensation(
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

            var periodDays = CountDays(
                period.FundingFrom,
                period.FundingThrough);

            if (periodDays <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                periodDays / 365m;

            var annualMaximum =
                rateSet.SalaryRate
                * rateSet.PracticeCompensationRate
                * rateSet.PracticeCompensationMaxHours;

            maximumAmount +=
                annualMaximum
                * positionFraction
                * periodFraction;
        }

        return (Math.Min(input.AbsenceCompensation, maximumAmount), maximumAmount);
    }

    private static decimal CalculateLearningActivities(
    GrantCalculationInput input,
    GrantRateSet rateSet)
    {
        decimal maximumAmount = 0m;

        foreach (var period in input.EmploymentPeriods)
        {
            var periodDays = CountDays(
                period.FundingFrom,
                period.FundingThrough);

            if (periodDays <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                periodDays / 365m;

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

            var periodDays = CountDays(
                eligibilityPeriod.From,
                eligibilityPeriod.Through);

            if (periodDays <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                periodDays / 365m;

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
            var periodDays = CountDays(
                period.FundingFrom,
                period.FundingThrough);

            if (periodDays <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                periodDays / 365m;

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
            var periodDays = CountDays(
                period.FundingFrom,
                period.FundingThrough);

            if (periodDays <= 0)
            {
                continue;
            }

            var positionFraction =
                period.PositionPercentage / 100m;

            var periodFraction =
                periodDays / 365m;

            maximumAmount +=
                rateSet.CentralitySupplementMaxAmount
                * positionFraction
                * periodFraction;
        }

        return Math.Min(
            input.CentralitySupplementRequestedAmount,
            maximumAmount);
    }

    internal static int CountDays(
        DateOnly from,
        DateOnly through)
    {
        if (through < from)
        {
            throw new ArgumentException(
                "Funding through cannot be before funding from.");
        }

        return through.DayNumber - from.DayNumber + 1;
    }
}
