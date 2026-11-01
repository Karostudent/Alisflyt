using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class CoordinatorAuthorizationHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private readonly IsolatedWebApplicationFactory _factory;

    public CoordinatorAuthorizationHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ALIS")]
    [InlineData("Veileder")]
    public async Task Unauthorized_http_users_receive_403_for_coordinator_details(string? role)
    {
        using var client = CreateClient();
        if (role is not null)
        {
            var login = await client.GetAsync($"/Account/Login?role={role}");
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        }

        var response = await client.GetAsync($"/Coordinator/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Saksopplysninger", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coordinator_can_finalize_under_review_case_through_http()
    {
        var caseId = await CreateUnderReviewCaseAsync();
        using var client = CreateClient();
        await LoginAsCoordinatorAsync(client);

        var details = await client.GetAsync($"/Coordinator/Details/{caseId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        var token = ExtractAntiForgeryToken(await details.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(token));

        var response = await PostFinalizationAsync(client, caseId, token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/Coordinator/Details/{caseId}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        await using var verificationContext = _factory.CreateDbContext();
        var persisted = await verificationContext.GrantCases.SingleAsync(x => x.Id == caseId);
        Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.AwaitingAuditorApproval, persisted.Status);
        Assert.NotNull(persisted.ReadyForAuditorApprovalAtUtc);
    }

    [Fact]
    public async Task Coordinator_cannot_finalize_case_with_pending_guidance_approval_through_http()
    {
        var caseId = await CreateUnderReviewCaseWithPendingGuidanceAsync();
        using var client = CreateClient();
        await LoginAsCoordinatorAsync(client);

        var details = await client.GetAsync($"/Coordinator/Details/{caseId}");
        var detailsHtml = await details.Content.ReadAsStringAsync();
        Assert.Contains("disabled", detailsHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("før veiledningen er godkjent av veileder", detailsHtml, StringComparison.OrdinalIgnoreCase);
        var token = ExtractAntiForgeryToken(detailsHtml);
        var response = await PostFinalizationAsync(client, caseId, token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verificationContext = _factory.CreateDbContext();
        var persisted = await verificationContext.GrantCases.SingleAsync(x => x.Id == caseId);
        Assert.Equal(GrantCaseStatus.UnderReview, persisted.Status);
        Assert.Null(persisted.ReadyForAuditorApprovalAtUtc);
    }

    [Fact]
    public async Task Alis_can_submit_guidance_correction_and_persist_new_pending_approval_through_http()
    {
        var caseId = await CreateUnderReviewCaseWithCorrectionRequestedAsync();
        using var client = CreateClient();
        await LoginAsAlisAsync(client);

        var edit = await client.GetAsync($"/GrantCases/EditGuidance/{caseId}");
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var token = ExtractAntiForgeryToken(await edit.Content.ReadAsStringAsync());

        using var request = new HttpRequestMessage(HttpMethod.Post, "/GrantCases/UpdateGuidance")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = caseId.ToString(),
                ["CaseId"] = caseId.ToString(),
                ["Sessions[0].Date"] = "2026-02-10",
                ["Sessions[0].Hours"] = "4",
                ["Sessions[0].Topic"] = "Korrigert veiledning",
                ["__RequestVerificationToken"] = token
            })
        };
        var response = await client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Redirect, $"Expected redirect but got {response.StatusCode}: {responseBody}");
        Assert.Contains($"/GrantCases/Details/{caseId}", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);

        await using var verificationContext = _factory.CreateDbContext();
        var persisted = await verificationContext.GrantCases
            .Include(x => x.GuidanceApprovals)
            .SingleAsync(x => x.Id == caseId);
        Assert.Equal(GrantCaseStatus.UnderReview, persisted.Status);
        Assert.Equal(2, persisted.GuidanceApprovals.Count);
        Assert.Equal(GuidanceApprovalStatus.Superseded, persisted.GuidanceApprovals.Single(x => x.VersionNumber == 1).Status);
        Assert.Equal(GuidanceApprovalStatus.Pending, persisted.GuidanceApprovals.Single(x => x.VersionNumber == 2).Status);
        var data = System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(persisted.ApplicationDataJson!)!;
        Assert.Equal(4m, data.Certificate.Sessions.Single().Hours);
        Assert.Equal("Korrigert veiledning", data.Certificate.Sessions.Single().Topic);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task Finalization_without_valid_antiforgery_token_returns_400_and_preserves_case(string? token)
    {
        var caseId = await CreateUnderReviewCaseAsync();
        using var client = CreateClient();
        await LoginAsCoordinatorAsync(client);

        var details = await client.GetAsync($"/Coordinator/Details/{caseId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);

        var values = new Dictionary<string, string> { ["id"] = caseId.ToString() };
        if (token is not null)
            values["__RequestVerificationToken"] = token;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Coordinator/MarkReadyForAuditorApproval")
        {
            Content = new FormUrlEncodedContent(values)
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verificationContext = _factory.CreateDbContext();
        var persisted = await verificationContext.GrantCases.SingleAsync(x => x.Id == caseId);
        Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.UnderReview, persisted.Status);
        Assert.Null(persisted.ReadyForAuditorApprovalAtUtc);
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task LoginAsCoordinatorAsync(HttpClient client)
    {
        var login = await client.GetAsync("/Account/Login?role=Koordinator");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private async Task<Guid> CreateUnderReviewCaseAsync()
    {
        using var startupClient = CreateClient();
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), $"HTTP-{Guid.NewGuid():N}"[..13], now);
        grantCase.UpdateDraft("123456789", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        grantCase.Submit(now.AddMinutes(1));
        grantCase.StartReview(now.AddMinutes(2));
        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return grantCase.Id;
    }

    private static async Task LoginAsAlisAsync(HttpClient client)
    {
        var login = await client.GetAsync("/Account/Login?role=ALIS");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private async Task<Guid> CreateUnderReviewCaseWithCorrectionRequestedAsync()
    {
        using var startupClient = CreateClient();
        var caseId = await CreateUnderReviewCaseWithPendingGuidanceAsync();
        await using var context = _factory.CreateDbContext();
        var grantCase = await context.GrantCases
            .Include(x => x.GuidanceApprovals)
            .SingleAsync(x => x.Id == caseId);
        grantCase.GetCurrentGuidanceApproval()!.RequestCorrection("Korriger timer.", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
        return caseId;
    }

    private async Task<Guid> CreateUnderReviewCaseWithPendingGuidanceAsync()
    {
        await using var context = _factory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var grantCase = GrantCase.Create(Guid.NewGuid(), $"HTTP-{Guid.NewGuid():N}"[..13], now);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Allmennlege"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2025, 1, 1),
            ExpectedCompletionDate = new DateOnly(2028, 1, 1),
            ConfirmsAlisAgreement = true,
            ConfirmsOfficialAgreementTemplate = true,
            ConfirmsNoCommercialAgencyAffiliation = true,
            AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
            FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [new EmploymentPeriodInputModel
            {
                PositionType = PositionType.RegularGpOrLocum,
                PositionPercentage = 50m,
                EmploymentStartDate = new DateOnly(2026, 1, 1),
                FundingFrom = new DateOnly(2026, 1, 1),
                FundingThrough = new DateOnly(2026, 12, 31)
            }],
            Certificate = new SupervisionCertificateViewModel
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                Sessions = [new SupervisionSessionInputModel
                {
                    Date = new DateOnly(2026, 1, 10),
                    Hours = 2m,
                    Topic = "Veiledning"
                }]
            }
        }, now);
        grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(1), "Per Olsen");
        grantCase.Submit(now.AddMinutes(2));
        grantCase.StartReview(now.AddMinutes(3));
        context.GrantCases.Add(grantCase);
        await context.SaveChangesAsync();
        return grantCase.Id;
    }

    private static async Task<HttpResponseMessage> PostFinalizationAsync(HttpClient client, Guid caseId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Coordinator/MarkReadyForAuditorApproval")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = caseId.ToString(),
                ["__RequestVerificationToken"] = token
            })
        };
        return await client.SendAsync(request);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
}

public sealed class IsolatedWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Server=(localdb)\\MSSQLLocalDB;Database=Alisflyt_Http_{Guid.NewGuid():N};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public IsolatedWebApplicationFactory()
    {
        TestDatabaseGuard.AssertNotDevelopmentDatabase(_connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["SeedDemoData"] = "false"
        }));
    }

    public ApplicationDbContext CreateDbContext()
    {
        TestDatabaseGuard.AssertNotDevelopmentDatabase(_connectionString);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            using var context = CreateDbContext();
            context.Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }
}
