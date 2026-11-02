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

        public async Task AddGuidanceApprovalAsync(GuidanceApproval approval, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(approval);
            if (entry.State == EntityState.Detached)
            {
                await _context.GuidanceApprovals.AddAsync(approval, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.GrantCases
                .Include(c => c.GuidanceApprovals)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default)
        {
            var list = await _context.GrantCases
                .Include(c => c.GuidanceApprovals)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return list;
        }

        public async Task<GrantCase?> GetAccessibleByIdAsync(
            Guid id,
            string userId,
            bool canAccessSubmittedCases,
            CancellationToken cancellationToken = default)
        {
            return await _context.GrantCases
                .Include(c => c.GuidanceApprovals)
                .Where(c => canAccessSubmittedCases
                    ? c.OwnerUserId != null && c.Status != Alisflyt.Domain.Enums.GrantCaseStatus.Draft
                    : c.OwnerUserId == userId)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<GrantCase>> ListAccessibleAsync(
            string userId,
            bool canAccessSubmittedCases,
            CancellationToken cancellationToken = default)
        {
            return await _context.GrantCases
                .Include(c => c.GuidanceApprovals)
                .Where(c => canAccessSubmittedCases
                    ? c.OwnerUserId != null && c.Status != Alisflyt.Domain.Enums.GrantCaseStatus.Draft
                    : c.OwnerUserId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
