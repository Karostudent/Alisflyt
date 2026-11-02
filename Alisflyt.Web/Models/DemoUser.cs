using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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

    private static readonly IReadOnlyDictionary<string, string> DoctorUserKeyToSupervisorUserKeyMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["alis-anna"] = "veileder-per"
        };

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

    public static string? GetDoctorUserKeyForHprNumber(string? doctorHprNumber)
    {
        if (string.IsNullOrWhiteSpace(doctorHprNumber))
            return null;

        return DemoUsers
            .FirstOrDefault(u => string.Equals(u.HprNumber, doctorHprNumber, StringComparison.OrdinalIgnoreCase))
            ?.Key;
    }

    public static string? GetSupervisorUserKeyForDoctorUserKey(string? doctorUserKey)
    {
        if (string.IsNullOrWhiteSpace(doctorUserKey))
            return null;

        return DoctorUserKeyToSupervisorUserKeyMap.TryGetValue(doctorUserKey, out var supervisorUserKey)
            ? supervisorUserKey
            : null;
    }

    public static string? GetSupervisorUserKeyForDoctor(string? doctorHprNumber)
    {
        if (string.IsNullOrWhiteSpace(doctorHprNumber))
            return null;

        var doctorUserKey = GetDoctorUserKeyForHprNumber(doctorHprNumber);
        return GetSupervisorUserKeyForDoctorUserKey(doctorUserKey);
    }

    public static IReadOnlyList<string> GetAssignedDoctorUserKeys(string? supervisorUserKey)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserKey))
            return Array.Empty<string>();

        return DoctorUserKeyToSupervisorUserKeyMap
            .Where(kvp => string.Equals(kvp.Value, supervisorUserKey, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToArray();
    }

    public static IReadOnlyList<string> GetAssignedDoctorHprNumbers(string? supervisorUserKey)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserKey))
            return Array.Empty<string>();

        var assignedDoctorKeys = GetAssignedDoctorUserKeys(supervisorUserKey);
        return DemoUsers
            .Where(u => assignedDoctorKeys.Contains(u.Key, StringComparer.OrdinalIgnoreCase))
            .Select(u => u.HprNumber)
            .Where(hpr => !string.IsNullOrWhiteSpace(hpr))
            .ToArray();
    }

    public static bool IsCurrentSupervisorForDoctor(HttpContext? httpContext, string? doctorHprNumber)
    {
        if (httpContext is null)
            return false;

        var current = GetCurrentUser(httpContext);
        if (current is null || !string.Equals(current.Role, "Veileder", StringComparison.OrdinalIgnoreCase))
            return false;

        var doctorUserKey = GetDoctorUserKeyForHprNumber(doctorHprNumber);
        var mappedSupervisor = GetSupervisorUserKeyForDoctorUserKey(doctorUserKey);
        return !string.IsNullOrWhiteSpace(mappedSupervisor)
            && string.Equals(mappedSupervisor, current.Key, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanAccessCaseForCurrentUser(HttpContext? httpContext, string? doctorHprNumber)
    {
        if (httpContext is null)
            return true;

        var current = GetCurrentUser(httpContext);
        if (current is null)
            return false;

        if (string.Equals(current.Role, "ALIS", StringComparison.OrdinalIgnoreCase))
        {
            var assignedDoctorKey = GetDoctorUserKeyForHprNumber(doctorHprNumber);
            return string.Equals(current.Key, assignedDoctorKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.HprNumber, doctorHprNumber, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(current.Role, "Veileder", StringComparison.OrdinalIgnoreCase))
            return IsCurrentSupervisorForDoctor(httpContext, doctorHprNumber);

        return true;
    }

    public static DemoUser? GetCurrentUser(HttpContext httpContext, ILogger? logger = null)
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
        catch (Exception ex)
        {
            logger?.LogError(ex, "Demo authentication session read failed.");
            return null;
        }
    }

    public static bool SignIn(HttpContext httpContext, DemoUser user, ILogger? logger = null)
    {
        if (httpContext == null || user == null)
            return false;

        try
        {
            httpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(user));
            var role = string.Equals(user.Role, "Koordinator", StringComparison.OrdinalIgnoreCase)
                ? "Avdelingsleder"
                : user.Role;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Key),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, role)
            ], "Demo"));
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Demo authentication session write failed during sign-in.");
            return false;
        }
    }

    public static bool SignOut(HttpContext httpContext, ILogger? logger = null)
    {
        if (httpContext is null)
            return false;

        try
        {
            httpContext.Session.Remove(SessionKey);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Demo authentication session removal failed during sign-out.");
            return false;
        }
    }

    public static bool IsInRole(HttpContext httpContext, string role)
    {
        var current = GetCurrentUser(httpContext);
        return current is not null && string.Equals(current.Role, role, StringComparison.OrdinalIgnoreCase);
    }
}
