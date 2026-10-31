using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class GrantCasesSaveAuthorizationHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private readonly IsolatedWebApplicationFactory _factory;

    public GrantCasesSaveAuthorizationHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Alis_cannot_update_another_alis_case_through_http()
    {
        var caseId = await CreateCaseAsync("999999999");
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetAsync("/Account/Login?role=ALIS");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var form = await client.GetAsync("/GrantCases/Create");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        var token = ExtractAntiForgeryToken(await form.Content.ReadAsStringAsync());

        var payload = JsonSerializer.Serialize(new { Id = caseId, HprNumber = "123456789" });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/GrantCases/SaveApplication")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("RequestVerificationToken", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var verificationContext = _factory.CreateDbContext();
        var persisted = await verificationContext.GrantCases.SingleAsync(x => x.Id == caseId);
        Assert.Equal("999999999", persisted.HprNumber);
    }

    private async Task<Guid> CreateCaseAsync(string hprNumber)
    {
        using var startupClient = _factory.CreateClient();
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), $"HTTP-{Guid.NewGuid():N}"[..13], now);
        grantCase.UpdateDraft(hprNumber, 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return grantCase.Id;
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}
