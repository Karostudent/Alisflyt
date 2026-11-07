using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Infrastructure.Persistence;
using Alisflyt.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Alisflyt.IntegrationTests
{
    public class GrantCaseOverviewRepositoryIntegrationTests
    {
        [Fact]
        public async Task ListOverviewAsync_projects_metadata_without_selecting_application_data()
        {
            var dbName = "Alisflyt_Test_" + Guid.NewGuid().ToString("N");
            var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            var interceptor = new SqlCaptureInterceptor();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
                .AddInterceptors(interceptor)
                .Options;

            try
            {
                await using (var ctx = new ApplicationDbContext(options))
                {
                    ctx.Database.EnsureDeleted();
                    ctx.Database.EnsureCreated();

                    var id = Guid.NewGuid();
                    var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(
                        id,
                        "OVERVIEW-1",
                        DateTimeOffset.UtcNow);
                    grantCase.UpdateDraft("123456789", 80m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), DateTimeOffset.UtcNow);
                    await ctx.GrantCases.AddAsync(grantCase);
                    await ctx.SaveChangesAsync();

                    var approval = Alisflyt.Domain.Entities.GuidanceApproval.Create(
                        Guid.NewGuid(),
                        id,
                        "123456789",
                        "987654321",
                        "supervisor-key",
                        1,
                        "snapshot-hash",
                        DateTimeOffset.UtcNow);
                    await ctx.GuidanceApprovals.AddAsync(approval);
                    await ctx.SaveChangesAsync();

                    interceptor.Commands.Clear();
                    var repository = new GrantCaseRepository(ctx);
                    var result = await repository.ListOverviewAsync();

                    var item = Assert.Single(result);
                    Assert.Equal(id, item.Id);
                    Assert.Equal("OVERVIEW-1", item.CaseNumber);
                    Assert.Equal("123456789", item.HprNumber);
                    Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, item.GuidanceApprovalStatus);
                    Assert.DoesNotContain(interceptor.Commands, command => command.Contains("ApplicationDataJson", StringComparison.OrdinalIgnoreCase));
                }
            }
            finally
            {
                var cleanupOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;
                await using var cleanupContext = new ApplicationDbContext(cleanupOptions);
                cleanupContext.Database.EnsureDeleted();
            }
        }

        private sealed class SqlCaptureInterceptor : DbCommandInterceptor
        {
            public List<string> Commands { get; } = new();

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result)
            {
                Commands.Add(command.CommandText);
                return base.ReaderExecuting(command, eventData, result);
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                Commands.Add(command.CommandText);
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
