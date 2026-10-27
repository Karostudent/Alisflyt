using System.Text.Json;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public class GrantApplicationPersistenceTests
{
    [Fact]
    public async Task Migration_and_fresh_context_preserve_both_form_steps()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=Alisflyt_FormTest_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        var id = Guid.NewGuid();
        var data = new GrantApplicationData { DoctorName = "Test Lege", HprNumber = "1234567",
            EmploymentPeriods = [new() { PositionPercentage = 80 }, new() { PositionPercentage = 20 }],
            Certificate = new() { SupervisorName = "Test Veileder", Sessions = [new() { Hours = 1.5m, Topic = "Tema" }] } };
        try
        {
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                var entity = GrantCase.Create(id, "FORM-TEST", DateTimeOffset.UtcNow);
                entity.UpdateApplication(data, DateTimeOffset.UtcNow);
                db.Add(entity);
                await db.SaveChangesAsync();
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var entity = await db.Set<GrantCase>().SingleAsync(c => c.Id == id);
                Assert.Equal(JsonSerializer.Serialize(data), entity.ApplicationDataJson);
                Assert.Equal("1234567", entity.HprNumber);
                Assert.Equal(80m, entity.EmploymentPercentage);
            }
        }
        finally
        {
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task GuidanceApproval_can_persist_with_case_relation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=Alisflyt_GuidanceApproval_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        var caseId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        try
        {
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                var grantCase = GrantCase.Create(caseId, "GUIDANCE-APPROVAL", DateTimeOffset.UtcNow);
                grantCase.UpdateDraft("123456789", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), DateTimeOffset.UtcNow);
                var approval = GuidanceApproval.Create(
                    approvalId,
                    caseId,
                    "123456789",
                    "987654321",
                    "veileder-per",
                    1,
                    "hash-version-1",
                    DateTimeOffset.UtcNow);

                db.GrantCases.Add(grantCase);
                db.GuidanceApprovals.Add(approval);
                await db.SaveChangesAsync();
            }

            await using (var db = new ApplicationDbContext(options))
            {
                var approval = await db.GuidanceApprovals.SingleAsync(x => x.Id == approvalId);
                Assert.Equal(caseId, approval.GrantCaseId);
                Assert.Equal(GuidanceApprovalStatus.Pending, approval.Status);
                Assert.Equal("hash-version-1", approval.AttestationSnapshotHash);
            }
        }
        finally
        {
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
