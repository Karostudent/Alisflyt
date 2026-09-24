using Alisflyt.Domain.Forms;

namespace Alisflyt.Application.Models;

public sealed class GrantCalculationInput
{
    public GrantType GrantType { get; init; }

    public IReadOnlyList<GrantCalculationEmploymentPeriod>
        EmploymentPeriods
    { get; init; } = [];

    public decimal AbsenceCompensation { get; init; }

    public decimal LearningActivityExpenses { get; init; }

    public decimal SupervisionExpenses { get; init; }

    public decimal AdditionalSupervisionCosts { get; init; }
}