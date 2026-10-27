namespace Alisflyt.Web.ViewModels.Guidance;

public sealed class GuidanceCalculationViewModel
{
    public bool IsAvailable { get; init; }
    public decimal GuidanceAmount { get; init; }
    public string? RateSetName { get; init; }
    public DateOnly? RateSetValidFrom { get; init; }
    public DateOnly? RateSetValidTo { get; init; }
    public string? UnavailableReason { get; init; }
}
