using System.Security.Claims;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Infrastructure.Identity;
using Alisflyt.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public sealed class GuidanceControllerTests
{
    [Fact]
    public async Task Applications_only_returns_submitted_cases_for_current_supervisor()
    {
        var matchingDraft = CreateCase("DRAFT", "987654321", GrantCaseStatus.Draft);
        var matchingSubmitted = CreateCase("SUBMITTED", "987654321", GrantCaseStatus.Submitted);
        var unrelatedSubmitted = CreateCase("OTHER", "111111111", GrantCaseStatus.Submitted);
        var repository = new StubRepository(matchingDraft, matchingSubmitted, unrelatedSubmitted);
        var controller = CreateController(repository, CreateUser("veileder-id", "987654321"));

        var result = Assert.IsType<ViewResult>(await controller.Applications());
        var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.GuidanceApplicationsViewModel>(result.Model);

        var item = Assert.Single(model.Cases);
        Assert.Equal("SUBMITTED", item.CaseNumber);
    }

    [Fact]
    public async Task Applications_forbids_identity_supervisor_without_hpr_number()
    {
        var controller = CreateController(new StubRepository(), CreateUser("veileder-id", null));

        var result = await controller.Applications();

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    private static GuidanceController CreateController(StubRepository repository, AlisflytUser user)
    {
        var userManager = new UserManager<AlisflytUser>(
            new StubUserStore(user),
            null,
            new PasswordHasher<AlisflytUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            null);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Role, "Veileder")
            ], "Identity"))
        };
        return new GuidanceController(repository, userManager)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static AlisflytUser CreateUser(string id, string? hprNumber) =>
        new() { Id = id, UserName = "Veileder", HprNumber = hprNumber };

    private static GrantCase CreateCase(string caseNumber, string supervisorHpr, GrantCaseStatus status)
    {
        var grantCase = GrantCase.Create(Guid.NewGuid(), caseNumber, DateTimeOffset.UtcNow);
        grantCase.UpdateApplication(new GrantApplicationData
        {
            HprNumber = "123456789",
            DoctorName = "Testlege",
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2024, 1, 1),
            ExpectedCompletionDate = new DateOnly(2027, 1, 1),
            GrantType = GrantType.SupervisionOnly,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 1, 1),
            Attachments = [],
            DoctorProfessions = [],
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 100,
                    EmploymentStartDate = new DateOnly(2024, 1, 1),
                    FundingFrom = new DateOnly(2024, 1, 1),
                    FundingThrough = new DateOnly(2024, 12, 31)
                }
            ],
            Certificate = new SupervisionCertificateViewModel
            {
                SupervisorHprNumber = supervisorHpr,
                Sessions = []
            }
        }, DateTimeOffset.UtcNow);
        if (status != GrantCaseStatus.Draft)
            grantCase.Submit(DateTimeOffset.UtcNow);
        return grantCase;
    }

    private sealed class StubRepository(params GrantCase[] cases) : Alisflyt.Application.Abstractions.IGrantCaseRepository
    {
        public Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GrantCase>>(cases);

        public Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(cases.SingleOrDefault(item => item.Id == id));

        public Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddGuidanceApprovalAsync(GuidanceApproval approval, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubUserStore(AlisflytUser user) : IUserStore<AlisflytUser>
    {
        public Task<AlisflytUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id == userId ? user : null);
        public Task<AlisflytUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName == normalizedUserName ? user : null);
        public Task<string> GetUserIdAsync(AlisflytUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(AlisflytUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task<string?> GetNormalizedUserNameAsync(AlisflytUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetUserNameAsync(AlisflytUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task SetNormalizedUserNameAsync(AlisflytUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(AlisflytUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(AlisflytUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AlisflytUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
    }
}
