using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Infrastructure.Persistence;
using Alisflyt.Web.ViewModels.GrantCases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public sealed class GrantCaseReturnResubmissionHttpTests : IClassFixture<IsolatedWebApplicationFactory>
{
    private static readonly Guid AgreementAttachmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BasisAttachmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly IsolatedWebApplicationFactory _factory;

    public GrantCaseReturnResubmissionHttpTests(IsolatedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Alis_case_can_be_returned_edited_resubmitted_and_reviewed_without_duplication()
    {
        var caseId = Guid.NewGuid();
        var caseNumber = $"RETURN-{Guid.NewGuid():N}"[..15];
        var now = DateTimeOffset.UtcNow;
        using var startupClient = CreateClient();

        await using (var context = _factory.CreateDbContext())
        {
            var grantCase = GrantCase.Create(caseId, caseNumber, now);
            grantCase.UpdateApplication(CreateValidApplicationData(), now);
            context.GrantCases.Add(grantCase);
            await context.SaveChangesAsync();
        }

        var initialCaseCount = await CountCasesAsync();
        using var alisClient = CreateClient();
        await LoginAsync(alisClient, "ALIS");

        var initialDetails = await alisClient.GetAsync($"/GrantCases/Details/{caseId}");
        var submitResponse = await PostFormAsync(
            alisClient,
            "/GrantCases/Submit",
            new Dictionary<string, string>
            {
                ["id"] = caseId.ToString(),
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await initialDetails.Content.ReadAsStringAsync())
            });
        Assert.Equal(HttpStatusCode.Redirect, submitResponse.StatusCode);

        await AssertCaseStateAsync(caseId, GrantCaseStatus.Submitted, caseNumber, 1);

        using var coordinatorClient = CreateClient();
        await LoginAsync(coordinatorClient, "Koordinator");

        var submittedDetails = await coordinatorClient.GetAsync($"/Coordinator/Details/{caseId}");
        var startReviewResponse = await PostFormAsync(
            coordinatorClient,
            "/Coordinator/StartReview",
            new Dictionary<string, string>
            {
                ["id"] = caseId.ToString(),
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await submittedDetails.Content.ReadAsStringAsync())
            });
        Assert.Equal(HttpStatusCode.Redirect, startReviewResponse.StatusCode);
        await AssertCaseStateAsync(caseId, GrantCaseStatus.UnderReview, caseNumber, 1);

        var returnPage = await coordinatorClient.GetAsync($"/Coordinator/ReturnForCorrection/{caseId}");
        const string returnReason = "Kontroller ansettelsesperioden.";
        var returnResponse = await PostFormAsync(
            coordinatorClient,
            "/Coordinator/ReturnForCorrection",
            new Dictionary<string, string>
            {
                ["Id"] = caseId.ToString(),
                ["CaseNumber"] = caseNumber,
                ["Reason"] = returnReason,
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await returnPage.Content.ReadAsStringAsync())
            });
        Assert.Equal(HttpStatusCode.Redirect, returnResponse.StatusCode);

        await AssertCaseStateAsync(caseId, GrantCaseStatus.ReturnedForCorrection, caseNumber, 1, returnReason);

        var alisOverview = await alisClient.GetAsync("/Alis");
        var overviewHtml = await alisOverview.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, alisOverview.StatusCode);
        Assert.Contains("Returnert for korrigering", overviewHtml, StringComparison.Ordinal);
        Assert.Contains(returnReason, overviewHtml, StringComparison.Ordinal);

        var editPage = await alisClient.GetAsync($"/GrantCases/Edit/{caseId}");
        Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);
        var editToken = ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());

        await using (var context = _factory.CreateDbContext())
        {
            var persisted = await context.GrantCases.SingleAsync(c => c.Id == caseId);
            var form = JsonSerializer.Deserialize<GrantApplicationFormViewModel>(persisted.ApplicationDataJson!)!;
            form.Id = caseId;
            form.DoctorName = "Anna Hansen – korrigert";

            using var request = new HttpRequestMessage(HttpMethod.Post, "/GrantCases/SaveApplication")
            {
                Content = new StringContent(JsonSerializer.Serialize(form), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("RequestVerificationToken", editToken);
            var saveResponse = await alisClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        }

        await AssertCaseStateAsync(caseId, GrantCaseStatus.ReturnedForCorrection, caseNumber, 1, returnReason, "Anna Hansen – korrigert");

        var resubmitPage = await alisClient.GetAsync($"/GrantCases/Details/{caseId}");
        var resubmitResponse = await PostFormAsync(
            alisClient,
            "/GrantCases/Submit",
            new Dictionary<string, string>
            {
                ["id"] = caseId.ToString(),
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await resubmitPage.Content.ReadAsStringAsync())
            });
        Assert.Equal(HttpStatusCode.Redirect, resubmitResponse.StatusCode);
        await AssertCaseStateAsync(caseId, GrantCaseStatus.Submitted, caseNumber, 1, returnReason, "Anna Hansen – korrigert");

        var resumedDetails = await coordinatorClient.GetAsync($"/Coordinator/Details/{caseId}");
        var resumeReviewResponse = await PostFormAsync(
            coordinatorClient,
            "/Coordinator/StartReview",
            new Dictionary<string, string>
            {
                ["id"] = caseId.ToString(),
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(await resumedDetails.Content.ReadAsStringAsync())
            });
        Assert.Equal(HttpStatusCode.Redirect, resumeReviewResponse.StatusCode);
        await AssertCaseStateAsync(caseId, GrantCaseStatus.UnderReview, caseNumber, 1, returnReason, "Anna Hansen – korrigert");
        await AssertAttachmentsAsync(caseId);

        var agreementDownload = await coordinatorClient.GetAsync($"/GrantCases/DownloadAttachment/{caseId}?attachmentId={AgreementAttachmentId}");
        Assert.Equal(HttpStatusCode.OK, agreementDownload.StatusCode);
        Assert.Equal(Encoding.UTF8.GetBytes("agreement"), await agreementDownload.Content.ReadAsByteArrayAsync());

        var basisDownload = await coordinatorClient.GetAsync($"/GrantCases/DownloadAttachment/{caseId}?attachmentId={BasisAttachmentId}");
        Assert.Equal(HttpStatusCode.OK, basisDownload.StatusCode);
        Assert.Equal(Encoding.UTF8.GetBytes("basis"), await basisDownload.Content.ReadAsByteArrayAsync());

        Assert.Equal(initialCaseCount, await CountCasesAsync());
    }

    private HttpClient CreateClient()
        => _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task LoginAsync(HttpClient client, string role)
    {
        var response = await client.GetAsync($"/Account/Login?role={role}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> values)
        => await client.PostAsync(url, new FormUrlEncodedContent(values));

    private async Task<int> CountCasesAsync()
    {
        await using var context = _factory.CreateDbContext();
        return await context.GrantCases.CountAsync();
    }

    private async Task AssertAttachmentsAsync(Guid id)
    {
        await using var context = _factory.CreateDbContext();
        var persisted = await context.GrantCases.SingleAsync(c => c.Id == id);
        var data = JsonSerializer.Deserialize<GrantApplicationData>(persisted.ApplicationDataJson!)!;

        Assert.Equal(2, data.Attachments.Count);
        Assert.Equal(AgreementAttachmentId, data.Attachments[0].Id);
        Assert.Equal("agreement", Encoding.UTF8.GetString(Convert.FromBase64String(data.Attachments[0].ContentBase64)));
        Assert.Equal(BasisAttachmentId, data.Attachments[1].Id);
        Assert.Equal("basis", Encoding.UTF8.GetString(Convert.FromBase64String(data.Attachments[1].ContentBase64)));
    }

    private async Task AssertCaseStateAsync(
        Guid id,
        GrantCaseStatus status,
        string caseNumber,
        int expectedCaseCount,
        string? returnReason = null,
        string? doctorName = null)
    {
        await using var context = _factory.CreateDbContext();
        var persisted = await context.GrantCases.SingleAsync(c => c.Id == id);
        Assert.Equal(status, persisted.Status);
        Assert.Equal(id, persisted.Id);
        Assert.Equal(caseNumber, persisted.CaseNumber);
        Assert.Equal(expectedCaseCount, await context.GrantCases.CountAsync());
        if (returnReason is not null)
            Assert.Equal(returnReason, persisted.ReturnReason);
        if (doctorName is not null)
        {
            var data = JsonSerializer.Deserialize<GrantApplicationData>(persisted.ApplicationDataJson!)!;
            Assert.Equal(doctorName, data.DoctorName);
        }
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static GrantApplicationData CreateValidApplicationData() => new()
    {
        HprNumber = "123456789",
        GrantType = GrantType.AlisAgreementIncludingSupervision,
        DoctorName = "Anna Hansen",
        DoctorProfessions = ["Allmennlege"],
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
        [
            new EmploymentPeriodInputModel
            {
                PositionType = PositionType.RegularGpOrLocum,
                PositionPercentage = 50m,
                EmploymentStartDate = new DateOnly(2026, 1, 1),
                FundingFrom = new DateOnly(2026, 1, 1),
                FundingThrough = new DateOnly(2026, 12, 31)
            }
        ],
        Certificate = new SupervisionCertificateViewModel(),
        Attachments =
        [
            new ApplicationAttachment
            {
                Id = AgreementAttachmentId,
                Kind = "Agreement",
                FileName = "avtale.pdf",
                ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("agreement"))
            },
            new ApplicationAttachment
            {
                Id = BasisAttachmentId,
                Kind = "CalculationBasis",
                FileName = "grunnlag.pdf",
                ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("basis"))
            }
        ],
        ProductivityRequestedAmount = 0m,
        AbsenceCompensation = 0m,
        LearningActivityExpenses = 0m,
        SupervisionExpenses = 0m,
        HasAdditionalSupervisionCosts = false,
        AdditionalSupervisionCosts = 0m,
        IsCentralityGrade6 = false,
        CentralitySupplementRequestedAmount = 0m
    };
}
