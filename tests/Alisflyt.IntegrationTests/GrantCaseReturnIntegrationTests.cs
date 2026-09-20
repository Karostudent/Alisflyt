using System;
using System.Threading.Tasks;
using Alisflyt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests
{
    public class GrantCaseReturnIntegrationTests
    {
        private static string CreateConnectionString(string dbName)
        {
            return $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        [Fact]
        public async Task CreateSubmitStartReviewReturn_reload_preserves_return_fields()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var conn = CreateConnectionString(dbName);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;

            try
            {
                await using var ctx = new ApplicationDbContext(options);
                ctx.Database.EnsureDeleted();
                ctx.Database.EnsureCreated();

                var id = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;

                var grant = Domain.Entities.GrantCase.Create(id, "CASE-INT-1", now);
                grant.UpdateDraft("HPR1", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), now.AddMinutes(1));
                await ctx.GrantCases.AddAsync(grant);
                await ctx.SaveChangesAsync();

                // submit
                grant.Submit(now.AddMinutes(2));
                await ctx.SaveChangesAsync();

                // start review
                grant.StartReview(now.AddMinutes(3));
                await ctx.SaveChangesAsync();

                // return for correction
                var returnTime = now.AddMinutes(4);
                grant.ReturnForCorrection("Integration reason", returnTime);
                await ctx.SaveChangesAsync();

                // new context to simulate reload
                await using var reloadCtx = new ApplicationDbContext(options);
                var reloaded = await reloadCtx.GrantCases.FirstOrDefaultAsync(c => c.Id == id);

                Assert.NotNull(reloaded);
                Assert.Equal(Domain.Enums.GrantCaseStatus.ReturnedForCorrection, reloaded!.Status);
                Assert.Equal("Integration reason", reloaded.ReturnReason);
                Assert.Equal(returnTime, reloaded.ReturnedAtUtc);
            }
            finally
            {
                var cleanupOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(conn).Options;
                await using var cleanupCtx = new ApplicationDbContext(cleanupOptions);
                cleanupCtx.Database.EnsureDeleted();
            }
        }
    }
}
