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
