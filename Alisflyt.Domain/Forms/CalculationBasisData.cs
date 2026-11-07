namespace Alisflyt.Domain.Forms;

public sealed class CalculationBasisData
{
    public List<CalculationBasisActivityInputModel> LearningActivities { get; set; } = [];
    public List<CalculationBasisActivityInputModel> StudyAndSpecializationDays { get; set; } = [];
    public List<CalculationBasisExpenseInputModel> Expenses { get; set; } = [];
    public decimal? IndividualGuidanceHours { get; set; }
    public decimal? GroupGuidanceHours { get; set; }

    public void Validate()
    {
        if (LearningActivities.Any(activity => activity is null)
            || StudyAndSpecializationDays.Any(activity => activity is null)
            || Expenses.Any(expense => expense is null))
            throw new ArgumentException("Beregningsgrunnlaget inneholder ugyldige rader.");

        if (IndividualGuidanceHours < 0 || GroupGuidanceHours < 0)
            throw new ArgumentException("Veiledningstimer kan ikke være negative.");

        foreach (var activity in LearningActivities.Concat(StudyAndSpecializationDays))
        {
            if (activity.Hours < 0 || activity.Days < 0)
                throw new ArgumentException("Timer og dager kan ikke være negative.");
        }

        foreach (var expense in Expenses)
        {
            if (expense.TravelExpense < 0 || expense.CourseExpense < 0)
                throw new ArgumentException("Utgifter kan ikke være negative.");
        }
    }
}

public sealed class CalculationBasisActivityInputModel
{
    public string Description { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public decimal? Hours { get; set; }
    public decimal? Days { get; set; }
}

public sealed class CalculationBasisExpenseInputModel
{
    public string Description { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public decimal? TravelExpense { get; set; }
    public decimal? CourseExpense { get; set; }
}
