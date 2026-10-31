using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Alisflyt.Infrastructure.Persistence;
using Alisflyt.Infrastructure.Persistence.Repositories;
using Alisflyt.Infrastructure.Development;
using Alisflyt.Infrastructure.Services;

namespace Alisflyt.IntegrationTests
{
    public class GrantCaseRepositoryIntegrationTests
    {
        private static string CreateConnectionString(string dbName)
        {
            return $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        [Fact]
        public async Task GrantCase_can_be_saved_and_retrieved()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var repo = new GrantCaseRepository(ctx);

                var id = Guid.NewGuid();
                var caseNumber = "INT-" + Guid.NewGuid().ToString("N").Substring(0,8);
                var now = DateTimeOffset.UtcNow;

                var gc = Alisflyt.Domain.Entities.GrantCase.Create(id, caseNumber, now);
                gc.UpdateDraft("HPR-INT", 60m, new DateOnly(2026,1,1), new DateOnly(2026,12,31), now);

                await repo.AddAsync(gc);
                await repo.SaveChangesAsync();

                var fetched = await repo.GetByIdAsync(id);
                Assert.NotNull(fetched);
                Assert.Equal(caseNumber, fetched!.CaseNumber);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task ListAsync_returns_saved_cases()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var repo = new GrantCaseRepository(ctx);

                var id = Guid.NewGuid();
                var caseNumber = "INT-" + Guid.NewGuid().ToString("N").Substring(0,8);
                var now = DateTimeOffset.UtcNow;

                var gc = Alisflyt.Domain.Entities.GrantCase.Create(id, caseNumber, now);
                await repo.AddAsync(gc);
                await repo.SaveChangesAsync();

                var list = await repo.ListAsync();
                Assert.Contains(list, x => x.Id == id);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task Tracked_changes_are_persisted_with_SaveChangesAsync()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var repo = new GrantCaseRepository(ctx);

                var id = Guid.NewGuid();
                var caseNumber = "INT-" + Guid.NewGuid().ToString("N").Substring(0,8);
                var now = DateTimeOffset.UtcNow;

                var gc = Alisflyt.Domain.Entities.GrantCase.Create(id, caseNumber, now);
                gc.UpdateDraft("HPR-INT", 60m, new DateOnly(2026,1,1), new DateOnly(2026,12,31), now);

                await repo.AddAsync(gc);
                await repo.SaveChangesAsync();

                var fetched = await repo.GetByIdAsync(id);
                fetched!.UpdateDraft("HPR-UPDATED", 60m, new DateOnly(2026,1,1), new DateOnly(2026,12,31), DateTimeOffset.UtcNow);
                await repo.SaveChangesAsync();

                await using var verifyCtx = new ApplicationDbContext(options);
                var verifyRepo = new GrantCaseRepository(verifyCtx);
                var verify = await verifyRepo.GetByIdAsync(id);
                Assert.Equal("HPR-UPDATED", verify!.HprNumber);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task CaseNumber_must_be_unique()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var repo = new GrantCaseRepository(ctx);

                var now = DateTimeOffset.UtcNow;
                var caseNumber = "INT-UNIQ" + Guid.NewGuid().ToString("N").Substring(0,6);

                var a = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), caseNumber, now);
                var b = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), caseNumber, now);

                await repo.AddAsync(a);
                await repo.SaveChangesAsync();

                await repo.AddAsync(b);
                await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(async () => await repo.SaveChangesAsync());
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task Submit_and_pending_guidance_approval_are_persisted_without_concurrency_exception()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var repo = new GrantCaseRepository(ctx);
                var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
                var id = Guid.NewGuid();
                var gc = Alisflyt.Domain.Entities.GrantCase.Create(id, "INT-APPROVAL", now);
                gc.UpdateApplication(new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    HprNumber = "123456789",
                    DoctorName = "Anna Hansen",
                    DoctorProfessions = ["Lege"],
                    GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                    ConfirmsSpecialization = true,
                    SpecializationStartDate = new DateOnly(2025, 1, 1),
                    ExpectedCompletionDate = new DateOnly(2030, 1, 1),
                    ConfirmsAlisAgreement = true,
                    ConfirmsOfficialAgreementTemplate = true,
                    ConfirmsNoCommercialAgencyAffiliation = true,
                    AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
                    FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
                    SelectedPositionTypes = [Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum],
                    EmploymentPeriods =
                    [
                        new Alisflyt.Domain.Forms.EmploymentPeriodInputModel
                        {
                            PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum,
                            PositionPercentage = 80m,
                            EmploymentStartDate = new DateOnly(2025, 1, 1),
                            FundingFrom = new DateOnly(2026, 1, 1),
                            FundingThrough = new DateOnly(2026, 6, 30)
                        }
                    ],
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        DoctorName = "Anna Hansen",
                        SupervisorHprNumber = "987654321",
                        SupervisorName = "Per Olsen",
                        DoctorSigningPlace = "Oslo",
                        SupervisorSigningPlace = "Oslo",
                        DoctorSigningDate = new DateOnly(2026, 6, 30),
                        SupervisorSigningDate = new DateOnly(2026, 6, 30),
                        Sessions =
                        [
                            new Alisflyt.Domain.Forms.SupervisionSessionInputModel
                            {
                                Date = new DateOnly(2026, 1, 15),
                                Hours = 2m,
                                Topic = "Veiledning"
                            }
                        ]
                    }
                }, now.AddMinutes(1));

                await repo.AddAsync(gc);
                await repo.SaveChangesAsync();

                var persisted = await repo.GetByIdAsync(id);
                Assert.NotNull(persisted);
                persisted!.Submit(now.AddMinutes(2));
                var approval = persisted.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(3), "Per Olsen");
                await repo.AddGuidanceApprovalAsync(approval);
                await repo.SaveChangesAsync();

                await using var verifyCtx = new ApplicationDbContext(options);
                var verifyRepo = new GrantCaseRepository(verifyCtx);
                var verify = await verifyRepo.GetByIdAsync(id);

                Assert.NotNull(verify);
                Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, verify!.Status);
                Assert.Single(verify.GuidanceApprovals);
                Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, verify.GuidanceApprovals.Single().Status);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task GuidanceApproval_tracking_uses_added_state_and_persists_single_insert()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var repo = new GrantCaseRepository(ctx);
                var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
                var id = Guid.NewGuid();
                var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(id, "INT-TRACKING", now);
                grantCase.UpdateApplication(new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    HprNumber = "123456789",
                    DoctorName = "Anna Hansen",
                    DoctorProfessions = ["Lege"],
                    GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                    ConfirmsSpecialization = true,
                    SpecializationStartDate = new DateOnly(2025, 1, 1),
                    ExpectedCompletionDate = new DateOnly(2030, 1, 1),
                    ConfirmsAlisAgreement = true,
                    ConfirmsOfficialAgreementTemplate = true,
                    ConfirmsNoCommercialAgencyAffiliation = true,
                    AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
                    FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
                    SelectedPositionTypes = [Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum],
                    EmploymentPeriods =
                    [
                        new Alisflyt.Domain.Forms.EmploymentPeriodInputModel
                        {
                            PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum,
                            PositionPercentage = 80m,
                            EmploymentStartDate = new DateOnly(2025, 1, 1),
                            FundingFrom = new DateOnly(2026, 1, 1),
                            FundingThrough = new DateOnly(2026, 6, 30)
                        }
                    ],
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        DoctorName = "Anna Hansen",
                        SupervisorHprNumber = "987654321",
                        SupervisorName = "Per Olsen",
                        DoctorSigningPlace = "Oslo",
                        SupervisorSigningPlace = "Oslo",
                        DoctorSigningDate = new DateOnly(2026, 6, 30),
                        SupervisorSigningDate = new DateOnly(2026, 6, 30),
                        Sessions =
                        [
                            new Alisflyt.Domain.Forms.SupervisionSessionInputModel
                            {
                                Date = new DateOnly(2026, 1, 10),
                                Hours = 2m,
                                Topic = "Veiledning"
                            }
                        ]
                    }
                }, now.AddMinutes(1));

                await repo.AddAsync(grantCase);
                await repo.SaveChangesAsync();

                var trackedCase = await repo.GetByIdAsync(id);
                var approval = trackedCase!.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(3), "Per Olsen");

                Assert.Equal(Microsoft.EntityFrameworkCore.EntityState.Detached, ctx.Entry(approval).State);
                Assert.Equal(1, trackedCase.GuidanceApprovals.Count);

                await repo.AddGuidanceApprovalAsync(approval);
                Assert.Equal(Microsoft.EntityFrameworkCore.EntityState.Added, ctx.Entry(approval).State);
                await repo.SaveChangesAsync();

                Assert.Equal(1, await ctx.GuidanceApprovals.CountAsync());

                var persisted = await ctx.GuidanceApprovals.SingleAsync();
                Assert.Equal(1, persisted.VersionNumber);
                Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, persisted.Status);
                Assert.Equal("veileder-per", persisted.SupervisorUserKey);

                await using var verifyCtx = new ApplicationDbContext(options);
                var verify = await new GrantCaseRepository(verifyCtx).GetByIdAsync(id);
                Assert.Single(verify!.GuidanceApprovals);
                Assert.Equal(1, verify.GuidanceApprovals.Single().VersionNumber);
                Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, verify.GuidanceApprovals.Single().Status);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task Ready_for_auditor_approval_status_and_timestamp_are_persisted()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();
                var repo = new GrantCaseRepository(ctx);
                var now = DateTimeOffset.UtcNow;
                var readyAt = now.AddMinutes(3);
                var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "INT-AUDITOR", now);
                grantCase.UpdateDraft("HPR-INT", 60m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
                grantCase.Submit(now.AddMinutes(1));
                grantCase.StartReview(now.AddMinutes(2));
                grantCase.MarkReadyForAuditorApproval(readyAt);

                await repo.AddAsync(grantCase);
                await repo.SaveChangesAsync();

                await using var verifyCtx = new ApplicationDbContext(options);
                var persisted = await new GrantCaseRepository(verifyCtx).GetByIdAsync(grantCase.Id);
                Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.AwaitingAuditorApproval, persisted!.Status);
                Assert.Equal(readyAt, persisted.ReadyForAuditorApprovalAtUtc);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }

        [Fact]
        public async Task DevelopmentDataSeeder_is_idempotent()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var tp = System.TimeProvider.System;

                await DevelopmentDataSeeder.SeedAsync(ctx, tp);
                var count1 = (await ctx.GrantCases.AsNoTracking().ToListAsync()).Count;

                await DevelopmentDataSeeder.SeedAsync(ctx, tp);
                var count2 = (await ctx.GrantCases.AsNoTracking().ToListAsync()).Count;

                Assert.Equal(count1, count2);
                Assert.InRange(count1, 1, 4);
            }
            finally
            {
                var optionsCleanup = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(optionsCleanup);
                cleanupCtx.Database.EnsureDeleted();
            }
        }
    }
}
