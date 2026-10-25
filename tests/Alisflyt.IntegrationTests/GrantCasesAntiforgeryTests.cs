using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Alisflyt.IntegrationTests;

public class GrantCasesAntiforgeryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GrantCasesAntiforgeryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_form_contains_antiforgery_token_and_valid_post_saves_draft()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await client.GetAsync("/Account/Login?role=ALIS");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var form = await client.GetAsync("/GrantCases/Create");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        var html = await form.Content.ReadAsStringAsync();
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);

        var token = ExtractAntiForgeryToken(html);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var payload = new
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = new[] { "Lege" },
            GrantType = 1,
            ConfirmsSpecialization = true,
            SpecializationStartDate = "2024-01-01",
            ExpectedCompletionDate = "2030-01-01",
            SelectedPositionTypes = new[] { 1 },
            EmploymentPeriods = new[]
            {
                new
                {
                    PositionType = 1,
                    PositionPercentage = 50m,
                    EmploymentStartDate = "2024-01-01",
                    FundingFrom = "2024-01-01",
                    FundingThrough = "2024-12-31"
                }
            },
            AbsenceCompensation = 100m,
            LearningActivityExpenses = 250m,
            SupervisionExpenses = 500m,
            HasAdditionalSupervisionCosts = false,
            AdditionalSupervisionCosts = (decimal?)null,
            Certificate = new
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                DoctorSigningPlace = "Oslo",
                SupervisorSigningPlace = "Oslo",
                DoctorSigningDate = "2024-06-01",
                SupervisorSigningDate = "2024-06-02",
                Sessions = Array.Empty<object>()
            },
            Attachments = Array.Empty<object>(),
            Id = Guid.Empty
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/GrantCases/SaveApplication")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("id", out var id));
        Assert.False(string.IsNullOrWhiteSpace(id.GetString()));
        Assert.True(json.RootElement.TryGetProperty("message", out var message));
        Assert.Equal("Utkastet er lagret.", message.GetString());
    }

    [Fact]
    public async Task Post_without_valid_antiforgery_token_is_rejected()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var login = await client.GetAsync("/Account/Login?role=ALIS");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        await client.GetAsync("/GrantCases/Create");

        var payload = new
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = new[] { "Lege" },
            GrantType = 1,
            ConfirmsSpecialization = true,
            SpecializationStartDate = "2024-01-01",
            ExpectedCompletionDate = "2030-01-01",
            SelectedPositionTypes = new[] { 1 },
            EmploymentPeriods = new[]
            {
                new
                {
                    PositionType = 1,
                    PositionPercentage = 50m,
                    EmploymentStartDate = "2024-01-01",
                    FundingFrom = "2024-01-01",
                    FundingThrough = "2024-12-31"
                }
            },
            AbsenceCompensation = 100m,
            LearningActivityExpenses = 250m,
            SupervisionExpenses = 500m,
            HasAdditionalSupervisionCosts = false,
            AdditionalSupervisionCosts = (decimal?)null,
            Certificate = new
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                DoctorSigningPlace = "Oslo",
                SupervisorSigningPlace = "Oslo",
                DoctorSigningDate = "2024-06-01",
                SupervisorSigningDate = "2024-06-02",
                Sessions = Array.Empty<object>()
            },
            Attachments = Array.Empty<object>(),
            Id = Guid.Empty
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/GrantCases/SaveApplication")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", "not-valid");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
