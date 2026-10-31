using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Infrastructure.Persistence;
using Alisflyt.Infrastructure.Persistence.Repositories;
using Alisflyt.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public class AtomicSubmissionIntegrationTests
{
    [Fact]
    public async Task Create_resume_update_and_submit_preserves_one_case_and_creates_guidance_approval()
    {
        var connectionString = CreateConnectionString();
        try
        {
            await using var context = CreateContext(connectionString);
            context.Database.EnsureCreated();
            var repository = new GrantCaseRepository(context);
            var service = new GrantCaseApplicationService(repository, new CaseNumberGenerator(), TimeProvider.System);

            var created = await service.CreateDraftAsync(new CreateGrantCaseRequest
            {
                HprNumber = "123456789"
            });
            var id = created.Id;
            var caseNumber = created.CaseNumber;

            var reopened = await service.GetByIdAsync(id);
            Assert.Equal(id, reopened.Id);
            Assert.Equal(caseNumber, reopened.CaseNumber);
            Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Draft, reopened.Status);

            var applicationData = JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(
                CreateCaseWithGuidance(DateTimeOffset.UtcNow).ApplicationDataJson!)!;
            applicationData.DoctorName = "Anna Hansen (oppdatert)";

            var updated = await service.UpdateDraftAsync(id, new Alisflyt.Application.Models.UpdateGrantCaseDraftRequest
            {
                ApplicationData = applicationData
            });

            Assert.Equal(id, updated.Id);
            Assert.Equal(caseNumber, updated.CaseNumber);
            Assert.Equal("Anna Hansen (oppdatert)", updated.ApplicationData!.DoctorName);

            var submitted = await service.SubmitAsync(id, GuidanceRequest());
            Assert.Equal(id, submitted.Id);
            Assert.Equal(caseNumber, submitted.CaseNumber);
            Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, submitted.Status);

            var listed = await service.ListAsync();
            var listedCase = Assert.Single(listed);
            Assert.Equal(id, listedCase.Id);
            Assert.Equal(caseNumber, listedCase.CaseNumber);
            Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, listedCase.Status);

            await using var verificationContext = CreateContext(connectionString);
            Assert.Equal(1, await verificationContext.GrantCases.CountAsync());
            var persisted = await verificationContext.GrantCases
                .Include(x => x.GuidanceApprovals)
                .SingleAsync(x => x.Id == id);

            Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, persisted.Status);
            Assert.Equal("Anna Hansen (oppdatert)",
                JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(persisted.ApplicationDataJson!)!.DoctorName);
            var approval = Assert.Single(persisted.GuidanceApprovals);
            Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, approval.Status);
            Assert.DoesNotContain(await verificationContext.GrantCases.ToListAsync(), x =>
                x.Id != id && x.Status == Alisflyt.Domain.Enums.GrantCaseStatus.Draft);
        }
        finally
        {
            await using var cleanupContext = CreateContext(connectionString);
            cleanupContext.Database.EnsureDeleted();
        }
    }

    [Fact]
    public async Task Submit_with_guidance_persists_submitted_case_and_pending_approval_together()
    {
        var connectionString = CreateConnectionString();
        try
        {
            await using var context = CreateContext(connectionString);
            context.Database.EnsureCreated();
            var repository = new GrantCaseRepository(context);
            var now = DateTimeOffset.UtcNow;
            var grantCase = CreateCaseWithGuidance(now);
            await repository.AddAsync(grantCase);
            await repository.SaveChangesAsync();

            var service = new GrantCaseApplicationService(repository, new CaseNumberGenerator(), TimeProvider.System);
            await service.SubmitAsync(grantCase.Id, GuidanceRequest());

            await using var verificationContext = CreateContext(connectionString);
            var persisted = await verificationContext.GrantCases
                .Include(x => x.GuidanceApprovals)
                .SingleAsync(x => x.Id == grantCase.Id);

            Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, persisted.Status);
            var approval = Assert.Single(persisted.GuidanceApprovals);
            Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, approval.Status);
            Assert.Equal(1, approval.VersionNumber);
        }
        finally
        {
            await using var cleanupContext = CreateContext(connectionString);
            cleanupContext.Database.EnsureDeleted();
        }
    }

    [Fact]
    public async Task Submit_when_save_fails_does_not_leave_submitted_case_or_approval()
    {
        var connectionString = CreateConnectionString();
        try
        {
            await using (var context = CreateContext(connectionString))
            {
                context.Database.EnsureCreated();
                var repository = new GrantCaseRepository(context);
                var now = DateTimeOffset.UtcNow;
                var grantCase = CreateCaseWithGuidance(now);
                await repository.AddAsync(grantCase);
                await repository.SaveChangesAsync();

                var service = new GrantCaseApplicationService(repository, new CaseNumberGenerator(), TimeProvider.System);
                var request = GuidanceRequest();
                request = new SubmitGrantCaseRequest
                {
                    DoctorHprNumber = request.DoctorHprNumber,
                    SupervisorHprNumber = request.SupervisorHprNumber,
                    SupervisorName = request.SupervisorName,
                    SupervisorUserKey = new string('x', 101)
                };

                await Assert.ThrowsAsync<DbUpdateException>(() => service.SubmitAsync(grantCase.Id, request));
            }

            await using var verificationContext = CreateContext(connectionString);
            var persisted = await verificationContext.GrantCases
                .Include(x => x.GuidanceApprovals)
                .SingleAsync(x => x.Id == GetCaseId(connectionString));

            Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Draft, persisted.Status);
            Assert.Empty(persisted.GuidanceApprovals);
        }
        finally
        {
            await using var cleanupContext = CreateContext(connectionString);
            cleanupContext.Database.EnsureDeleted();
        }
    }

    private static Guid GetCaseId(string connectionString)
    {
        using var context = CreateContext(connectionString);
        return context.GrantCases.Select(x => x.Id).Single();
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options);
    }

    private static string CreateConnectionString()
        => $"Server=(localdb)\\MSSQLLocalDB;Database=Alisflyt_Atomic_{Guid.NewGuid():N};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    private static SubmitGrantCaseRequest GuidanceRequest() => new()
    {
        DoctorHprNumber = "123456789",
        SupervisorHprNumber = "987654321",
        SupervisorUserKey = "veileder-per",
        SupervisorName = "Per Olsen"
    };

    private static Alisflyt.Domain.Entities.GrantCase CreateCaseWithGuidance(DateTimeOffset now)
    {
        var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "INT-ATOMIC", now);
        grantCase.UpdateDraft("123456789", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
        grantCase.UpdateApplication(new Alisflyt.Domain.Forms.GrantApplicationData
        {
            HprNumber = "123456789",
            GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2025, 1, 1),
            ExpectedCompletionDate = new DateOnly(2028, 1, 1),
            ConfirmsAlisAgreement = true,
            ConfirmsOfficialAgreementTemplate = true,
            ConfirmsNoCommercialAgencyAffiliation = true,
            AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
            FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Allmennlege"],
            SelectedPositionTypes = [Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum],
            EmploymentPeriods = [new Alisflyt.Domain.Forms.EmploymentPeriodInputModel
            {
                PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum,
                PositionPercentage = 50m,
                EmploymentStartDate = new DateOnly(2026, 1, 1),
                FundingFrom = new DateOnly(2026, 1, 1),
                FundingThrough = new DateOnly(2026, 12, 31)
            }],
            Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel
                {
                    Date = new DateOnly(2026, 1, 10),
                    Hours = 2m,
                    Topic = "Veiledning"
                }]
            }
        }, now);
        return grantCase;
    }
}
