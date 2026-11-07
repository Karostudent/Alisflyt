using System;
using System.Net;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class GrantCasesApplicationAuthorizationHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private readonly IsolatedWebApplicationFactory _factory;

    public GrantCasesApplicationAuthorizationHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Coordinator_can_open_application_through_http()
    {
        var caseId = await CreateCaseAsync();
        using var client = CreateClient();
        var login = await client.GetAsync("/Account/Login?role=Koordinator");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync($"/GrantCases/Application/{caseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sensitiv lege", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_user_cannot_open_application_through_http()
    {
        var caseId = await CreateCaseAsync();
        using var client = CreateClient();

        var response = await client.GetAsync($"/GrantCases/Application/{caseId}");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Sensitiv lege", content, StringComparison.Ordinal);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<Guid> CreateCaseAsync()
    {
        using var startupClient = CreateClient();
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), $"HTTP-{Guid.NewGuid():N}"[..13], now);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = "123456789",
            DoctorName = "Sensitiv lege"
        }, now);
        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return grantCase.Id;
    }
}
