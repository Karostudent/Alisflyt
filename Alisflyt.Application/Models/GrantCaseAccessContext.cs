namespace Alisflyt.Application.Models;

public sealed record GrantCaseAccessContext(string UserId, string Role)
{
    public bool IsAlis => string.Equals(Role, "ALIS", StringComparison.OrdinalIgnoreCase);
    public bool IsDepartmentManager => string.Equals(Role, "Avdelingsleder", StringComparison.OrdinalIgnoreCase);
    public bool CanAccessGrantCases => IsAlis || IsDepartmentManager;
}
