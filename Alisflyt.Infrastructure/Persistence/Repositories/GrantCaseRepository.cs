using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Infrastructure.Persistence.Repositories
{
    public class GrantCaseRepository : IGrantCaseRepository
    {
        private readonly ApplicationDbContext _context;

        public GrantCaseRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default)
        {
            await _context.GrantCases.AddAsync(grantCase, cancellationToken).ConfigureAwait(false);
        }

        public async Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.GrantCases.FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default)
        {
            var list = await _context.GrantCases.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            return list;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
