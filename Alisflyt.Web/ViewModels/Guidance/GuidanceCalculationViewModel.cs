namespace Alisflyt.Web.ViewModels.Guidance;

public sealed class GuidanceCalculationViewModel
{
    public bool IsAvailable { get; init; }
    public decimal GuidanceAmount { get; init; }
    public decimal RegisteredGuidanceHours { get; init; }
    public decimal GuidancePaidHours { get; init; }
    public decimal GuidanceHourlyRate { get; init; }
    public decimal GuidanceHonorariumAmount { get; init; }
    public decimal GuidanceHonorariumMaximumAmount { get; init; }
    public decimal GuidanceHonorariumCappedAmount { get; init; }
    public Alisflyt.Domain.Enums.GuidanceApprovalStatus? GuidanceHonorariumApprovalStatus { get; init; }
    public string? RateSetName { get; init; }
    public DateOnly? RateSetValidFrom { get; init; }
    public DateOnly? RateSetValidTo { get; init; }
    public string? UnavailableReason { get; init; }
}
