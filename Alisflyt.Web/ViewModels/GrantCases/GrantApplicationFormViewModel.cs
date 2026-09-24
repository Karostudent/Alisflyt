namespace Alisflyt.Web.ViewModels.GrantCases;

public class GrantApplicationFormViewModel : Alisflyt.Domain.Forms.GrantApplicationData
{
    public Guid Id { get; set; }
    public bool ReadOnly { get; set; }
}
