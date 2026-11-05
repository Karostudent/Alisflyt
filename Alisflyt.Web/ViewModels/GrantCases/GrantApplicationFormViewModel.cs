namespace Alisflyt.Web.ViewModels.GrantCases;

public sealed class GrantApplicationFieldError
{
    public string? Field { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Code { get; init; }
}

public class GrantApplicationFormViewModel : Alisflyt.Domain.Forms.GrantApplicationData
{
    public Guid Id { get; set; }
    public bool ReadOnly { get; set; }
    public List<GrantApplicationFieldError> InitialErrors { get; set; } = [];
}
