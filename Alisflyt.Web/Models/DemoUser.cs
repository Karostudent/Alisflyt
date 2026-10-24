using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Alisflyt.Web.Models;

public sealed class DemoUser
{
    public string Key { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HprNumber { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Role : Name;
}

public static class DemoAuthentication
{
    private const string SessionKey = "Alisflyt.DemoUser";

    public static IReadOnlyList<DemoUser> DemoUsers { get; } =
    [
        new DemoUser { Key = "alis-anna", Role = "ALIS", Name = "Anna Hansen", HprNumber = "123456789" },
        new DemoUser { Key = "koordinator-marta", Role = "Koordinator", Name = "Marta Solberg", HprNumber = "000000000" },
        new DemoUser { Key = "veileder-per", Role = "Veileder", Name = "Per Olsen", HprNumber = "987654321" },
        new DemoUser { Key = "administrator-ole", Role = "Administrator", Name = "Ole Hovland", HprNumber = "999999999" }
    ];

    public static DemoUser? GetUserByRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return null;

        return DemoUsers.FirstOrDefault(u => string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase));
    }

    public static DemoUser? GetCurrentUser(HttpContext httpContext)
    {
        if (httpContext == null)
            return null;

        try
        {
            var serialized = httpContext.Session.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(serialized))
                return null;

            return JsonSerializer.Deserialize<DemoUser>(serialized);
        }
        catch
        {
            return null;
        }
    }

    public static void SignIn(HttpContext httpContext, DemoUser user)
    {
        if (httpContext == null || user == null)
            return;

        try
        {
            httpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(user));
        }
        catch
        {
            // Session is not configured in some test contexts; ignore the demo login in those cases.
        }
    }

    public static void SignOut(HttpContext httpContext)
    {
        try
        {
            httpContext?.Session.Remove(SessionKey);
        }
        catch
        {
            // Session is not configured in some test contexts; ignore.
        }
    }

    public static bool IsInRole(HttpContext httpContext, string role)
    {
        var current = GetCurrentUser(httpContext);
        return current is not null && string.Equals(current.Role, role, StringComparison.OrdinalIgnoreCase);
    }
}
