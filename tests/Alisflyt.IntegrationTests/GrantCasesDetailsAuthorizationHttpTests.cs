using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class GrantCasesDetailsAuthorizationHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private readonly IsolatedWebApplicationFactory _factory;

    public GrantCasesDetailsAuthorizationHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_http_request_to_grant_case_details_returns_403()
    {
        var caseId = await CreateCaseAsync();
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync($"/GrantCases/Details/{caseId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Saksopplysninger", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<Guid> CreateCaseAsync()
    {
        using var startupClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), $"HTTP-{Guid.NewGuid():N}"[..13], now);
        grantCase.UpdateDraft("123456789", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return grantCase.Id;
    }
}
