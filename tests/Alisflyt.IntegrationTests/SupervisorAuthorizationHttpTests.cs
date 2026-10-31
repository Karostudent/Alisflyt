using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class SupervisorAuthorizationHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private readonly IsolatedWebApplicationFactory _factory;

    public SupervisorAuthorizationHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Alis_cannot_approve_guidance_through_http()
    {
        var caseId = await CreatePendingApprovalCaseAsync("123456789");
        using var client = CreateClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Login?role=ALIS")).StatusCode);

        var form = await client.GetAsync("/GrantCases/Create");
        var token = ExtractAntiForgeryToken(await form.Content.ReadAsStringAsync());
        var response = await PostAsync(client, caseId, token, "/Supervisor/Approve");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertApprovalStatusAsync(caseId, Domain.Enums.GuidanceApprovalStatus.Pending);
    }

    [Fact]
    public async Task Unassigned_supervisor_cannot_request_correction_through_http()
    {
        var assignedCaseId = await CreatePendingApprovalCaseAsync("123456789");
        var otherCaseId = await CreatePendingApprovalCaseAsync("999999999");
        using var client = CreateClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Login?role=Veileder")).StatusCode);

        var assignedDetails = await client.GetAsync($"/Supervisor/Details/{assignedCaseId}");
        var token = ExtractAntiForgeryToken(await assignedDetails.Content.ReadAsStringAsync());
        var response = await PostAsync(client, otherCaseId, token, "/Supervisor/RequestCorrection", ("reason", "Feil attest"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertApprovalStatusAsync(otherCaseId, Domain.Enums.GuidanceApprovalStatus.Pending);
    }

    [Fact]
    public async Task Assigned_supervisor_can_approve_guidance_through_http()
    {
        var caseId = await CreatePendingApprovalCaseAsync("123456789");
        using var client = CreateClient();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Login?role=Veileder")).StatusCode);

        var details = await client.GetAsync($"/Supervisor/Details/{caseId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var token = ExtractAntiForgeryToken(await details.Content.ReadAsStringAsync());
        var response = await PostAsync(client, caseId, token, "/Supervisor/Approve", ("confirmsAccuracy", "true"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await AssertApprovalStatusAsync(caseId, Domain.Enums.GuidanceApprovalStatus.Approved);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid caseId, string token, string path, params (string key, string value)[] extra)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("id", caseId.ToString()),
            new("__RequestVerificationToken", token)
        };
        foreach (var item in extra)
            values.Add(new(item.key, item.value));
        return await client.PostAsync(path, new FormUrlEncodedContent(values));
    }

    private async Task<Guid> CreatePendingApprovalCaseAsync(string hprNumber)
    {
        using var startupClient = CreateClient();
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), $"HTTP-{Guid.NewGuid():N}"[..13], now);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = hprNumber,
            DoctorName = "Testlege",
            Attachments = [],
            EmploymentPeriods = [],
            DoctorProfessions = [],
            SelectedPositionTypes = [],
            Certificate = new SupervisionCertificateViewModel
            {
                DoctorName = "Testlege",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                Sessions = [new SupervisionSessionInputModel
                {
                    Date = new DateOnly(2026, 1, 1),
                    Hours = 2m,
                    Topic = "Veiledning"
                }]
            }
        }, now);
        grantCase.RequestGuidanceApproval(hprNumber, "987654321", "veileder-per", now, "Per Olsen");
        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return grantCase.Id;
    }

    private async Task AssertApprovalStatusAsync(Guid caseId, Domain.Enums.GuidanceApprovalStatus expected)
    {
        await using var context = _factory.CreateDbContext();
        var grantCase = await context.GrantCases.Include(c => c.GuidanceApprovals).SingleAsync(c => c.Id == caseId);
        Assert.Equal(expected, grantCase.GetCurrentGuidanceApproval()?.Status);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
