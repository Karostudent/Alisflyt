using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Infrastructure.Development
{
    public class DevelopmentDataSeeder
    {
        // fixed GUIDs and case numbers
        private static readonly (Guid Id, string CaseNumber)[] SeedCases = new[]
        {
            (new Guid("11111111-1111-1111-1111-111111111111"), "ALIS-DEM-001"),
            (new Guid("22222222-2222-2222-2222-222222222222"), "ALIS-DEM-002"),
            (new Guid("33333333-3333-3333-3333-333333333333"), "ALIS-DEM-003"),
            (new Guid("44444444-4444-4444-4444-444444444444"), "ALIS-DEM-004")
        };

        public static async Task SeedAsync(Persistence.ApplicationDbContext context, System.TimeProvider timeProvider, CancellationToken cancellationToken = default)
        {
            var now = timeProvider.GetUtcNow();

            foreach (var (id, caseNumber) in SeedCases)
            {
                if (context.GrantCases.Any(c => c.CaseNumber == caseNumber))
                    continue;

                var gc = Domain.Entities.GrantCase.Create(id, caseNumber, now);

                // first: a complete draft
                if (caseNumber.EndsWith("001"))
                {
                    gc.UpdateDraft("HPR-DEM-1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,12,31), now);
                }
                else if (caseNumber.EndsWith("002"))
                {
                    // incomplete draft
                    gc.UpdateDraft(null, null, null, null, now);
                }
                else if (caseNumber.EndsWith("003"))
                {
                    // submitted case
                    gc.UpdateDraft("HPR-DEM-3", 80m, new DateOnly(2026,2,1), new DateOnly(2026,10,31), now);
                    gc.Submit(now);
                }
                else
                {
                    // extra draft
                    gc.UpdateDraft("HPR-DEM-4", 20m, new DateOnly(2026,3,1), new DateOnly(2026,9,30), now);
                }

                await context.GrantCases.AddAsync(gc, cancellationToken).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
