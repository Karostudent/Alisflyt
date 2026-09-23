using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Alisflyt.Infrastructure.Persistence.Repositories
{
    public class GrantRateSetRepository : IGrantRateSetRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public GrantRateSetRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext
                ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<GrantRateSet?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.GrantRateSets
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<GrantRateSet>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.GrantRateSets
                .OrderByDescending(x => x.ValidFrom)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            GrantRateSet rateSet,
            CancellationToken cancellationToken = default)
        {
            await _dbContext.GrantRateSets
                .AddAsync(rateSet, cancellationToken);
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}