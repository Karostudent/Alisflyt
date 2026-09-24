using Alisflyt.Domain.Forms;

namespace Alisflyt.Application.Models;

public static class GrantCalculationInputMapper
{
    public static GrantCalculationInput From(
        GrantApplicationData applicationData)
    {
        ArgumentNullException.ThrowIfNull(applicationData);

        var grantType = applicationData.GrantType
            ?? throw new ArgumentException(
                "Grant type is required for calculation.",
                nameof(applicationData));

        var employmentPeriods = applicationData.EmploymentPeriods
            .Select(period => new GrantCalculationEmploymentPeriod(
                period.PositionType
                    ?? throw new ArgumentException(
                        "Position type is required for calculation."),
                period.PositionPercentage
                    ?? throw new ArgumentException(
                        "Position percentage is required for calculation."),
                period.EmploymentStartDate
                    ?? throw new ArgumentException(
                        "Employment start date is required for calculation."),
                period.FundingFrom
                    ?? throw new ArgumentException(
                        "Funding start date is required for calculation."),
                period.FundingThrough
                    ?? throw new ArgumentException(
                        "Funding end date is required for calculation.")))
            .ToList();

        return new GrantCalculationInput
        {
            GrantType = grantType,
            EmploymentPeriods = employmentPeriods,

            AbsenceCompensation =
                applicationData.AbsenceCompensation ?? 0m,

            LearningActivityExpenses =
                applicationData.LearningActivityExpenses ?? 0m,

            SupervisionExpenses =
                applicationData.SupervisionExpenses ?? 0m,

            AdditionalSupervisionCosts =
                applicationData.AdditionalSupervisionCosts ?? 0m
        };
    }
}