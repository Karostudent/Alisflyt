using Alisflyt.Domain.Forms;

namespace Alisflyt.Application.Models;

public sealed class GrantCalculationInput
{
    public GrantType GrantType { get; init; }
    public DateOnly? FirstRegularGpOrLocumDate { get; init; }

    public IReadOnlyList<GrantCalculationEmploymentPeriod>
        EmploymentPeriods
    { get; init; } = [];

    public decimal AbsenceCompensation { get; init; }

    public decimal LearningActivityExpenses { get; init; }

    public decimal SupervisionExpenses { get; init; }

    public decimal AdditionalSupervisionCosts { get; init; }

    public bool IsCentralityGrade6 { get; init; }
    // Har saken i det hele tatt rett til sentralitetstillegg?

    public decimal CentralitySupplementRequestedAmount { get; init; }
    // Hvor mye av tilleggselementet søker kommunen faktisk om?
}