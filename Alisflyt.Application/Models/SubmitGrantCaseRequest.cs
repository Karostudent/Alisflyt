namespace Alisflyt.Application.Models;

public sealed class SubmitGrantCaseRequest
{
    public string? DoctorHprNumber { get; init; }
    public string? SupervisorHprNumber { get; init; }
    public string? SupervisorUserKey { get; init; }
    public string? SupervisorName { get; init; }
}
