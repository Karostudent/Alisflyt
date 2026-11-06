using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class GrantCasesReadAuthorizationHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private readonly IsolatedWebApplicationFactory _factory;

    public GrantCasesReadAuthorizationHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("ALIS")]
    [InlineData("Veileder")]
    public async Task Unauthorized_users_cannot_read_other_doctors_calculation_or_attachment(string role)
    {
        var created = await CreateCaseAsync("999999999", includeAttachment: true);
        using var client = CreateClient();
        var login = await client.GetAsync($"/Account/Login?role={role}");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var calculation = await client.GetAsync($"/GrantCases/CalculationPreview/{created.caseId}");
        var attachment = await client.GetAsync($"/GrantCases/DownloadAttachment/{created.caseId}?attachmentId={created.attachmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, calculation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, attachment.StatusCode);
        Assert.DoesNotContain("Saksopplysninger", await calculation.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("application/pdf", await attachment.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coordinator_can_download_attachment_from_available_case_and_invalid_urls_are_controlled()
    {
        var created = await CreateCaseAsync("123456789", includeAttachment: true);
        using var client = CreateClient();
        var login = await client.GetAsync("/Account/Login?role=Koordinator");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var download = await client.GetAsync($"/GrantCases/DownloadAttachment/{created.caseId}?attachmentId={created.attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, await download.Content.ReadAsByteArrayAsync());

        var missingAttachment = await client.GetAsync($"/GrantCases/DownloadAttachment/{created.caseId}?attachmentId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingAttachment.StatusCode);

        var missingCase = await client.GetAsync($"/GrantCases/DownloadAttachment/{Guid.NewGuid()}?attachmentId={created.attachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, missingCase.StatusCode);
    }

    [Fact]
    public async Task Alis_overviews_contain_only_cases_owned_by_logged_in_alis()
    {
        await CreateCaseAsync("123456789", includeAttachment: false, caseNumber: "OWN-HTTP");
        await CreateCaseAsync("999999999", includeAttachment: false, caseNumber: "OTHER-HTTP");
        using var client = CreateClient();
        var login = await client.GetAsync("/Account/Login?role=ALIS");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var grantCases = await client.GetAsync("/GrantCases");
        var alisDashboard = await client.GetAsync("/Alis");
        var grantCasesHtml = await grantCases.Content.ReadAsStringAsync();
        var alisDashboardHtml = await alisDashboard.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, grantCases.StatusCode);
        Assert.Equal(HttpStatusCode.OK, alisDashboard.StatusCode);
        Assert.Contains("OWN-HTTP", grantCasesHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("OTHER-HTTP", grantCasesHtml, StringComparison.Ordinal);
        Assert.Contains("OWN-HTTP", alisDashboardHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("OTHER-HTTP", alisDashboardHtml, StringComparison.Ordinal);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(Guid caseId, Guid attachmentId)> CreateCaseAsync(string hprNumber, bool includeAttachment, string? caseNumber = null)
    {
        using var startupClient = CreateClient();
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), caseNumber ?? $"HTTP-{Guid.NewGuid():N}"[..13], now);
        var attachmentId = Guid.NewGuid();
        if (includeAttachment)
        {
            grantCase.UpdateApplication(new GrantApplicationData
            {
                HprNumber = hprNumber,
                Attachments =
                [new ApplicationAttachment
                {
                    Id = attachmentId,
                    FileName = "private.pdf",
                    ContentBase64 = Convert.ToBase64String([1, 2, 3])
                }]
            }, now);
        }
        else
        {
            grantCase.UpdateDraft(hprNumber, 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        }

        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return (grantCase.Id, attachmentId);
    }
}
