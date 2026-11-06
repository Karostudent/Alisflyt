using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;

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
                .OrderByDescending(c => c.CreatedAtUtc)
                .ThenByDescending(c => c.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return list;
        }

        public async Task<IReadOnlyList<GrantCaseListItemDto>> ListOverviewAsync(CancellationToken cancellationToken = default)
        {
            return await _context.GrantCases
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAtUtc)
                .ThenByDescending(c => c.Id)
                .Select(c => new GrantCaseListItemDto
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    ReturnReason = c.ReturnReason,
                    GuidanceApprovalStatus = c.GuidanceApprovals
                        .Where(a => a.Status != GuidanceApprovalStatus.Superseded)
                        .OrderByDescending(a => a.VersionNumber)
                        .ThenByDescending(a => a.CreatedAtUtc)
                        .Select(a => (GuidanceApprovalStatus?)a.Status)
                        .FirstOrDefault(),
                    GuidanceCorrectionReason = c.GuidanceApprovals
                        .Where(a => a.Status != GuidanceApprovalStatus.Superseded)
                        .OrderByDescending(a => a.VersionNumber)
                        .ThenByDescending(a => a.CreatedAtUtc)
                        .Select(a => a.Status == GuidanceApprovalStatus.NeedsCorrection
                            ? a.RequestedRevisionReason
                            : null)
                        .FirstOrDefault(),
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
