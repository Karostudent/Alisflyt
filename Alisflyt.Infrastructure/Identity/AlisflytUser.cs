using Microsoft.AspNetCore.Identity;

namespace Alisflyt.Infrastructure.Identity;

public sealed class AlisflytUser : IdentityUser
{
    public string? HprNumber { get; set; }
}
