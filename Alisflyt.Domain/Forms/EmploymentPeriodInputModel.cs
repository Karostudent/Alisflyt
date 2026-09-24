

namespace Alisflyt.Domain.Forms;

public class EmploymentPeriodInputModel
{
    public PositionType? PositionType { get; set; }
    public decimal? PositionPercentage { get; set; }
    public DateOnly? EmploymentStartDate { get; set; }
    public DateOnly? FundingFrom { get; set; }
    public DateOnly? FundingThrough { get; set; }
}
