using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Application.Tests.Fakes
{
    internal class FakeGrantCaseRepository : IGrantCaseRepository
    {
        private readonly ConcurrentDictionary<Guid, GrantCase> _store = new();
        public int AddAsyncCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }
        public Exception? SaveChangesException { get; set; }

        public Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default)
        {
            _store[grantCase.Id] = grantCase;
            AddAsyncCallCount++;
            return Task.CompletedTask;
        }

        public Task AddGuidanceApprovalAsync(GuidanceApproval approval, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(approval.GrantCaseId, out var grantCase);
            if (grantCase is not null && !grantCase.GuidanceApprovals.Any(a => a.Id == approval.Id))
            {
                grantCase.GuidanceApprovals.Add(approval);
                _store[grantCase.Id] = grantCase;
            }
            return Task.CompletedTask;
        }

        public Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(id, out var c);
            return Task.FromResult(c);
        }

        public Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<GrantCase> list = _store.Values.ToList();
            return Task.FromResult(list);
        }

        public Task<IReadOnlyList<GrantCaseListItemDto>> ListOverviewAsync(CancellationToken cancellationToken = default)
        {
            var list = _store.Values
                .OrderByDescending(c => c.CreatedAtUtc)
                .ThenByDescending(c => c.Id)
                .Select(c => new GrantCaseListItemDto
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    ReturnReason = c.ReturnReason,
                    GuidanceApprovalStatus = c.GetCurrentGuidanceApproval()?.Status,
                    GuidanceCorrectionReason = c.GetCurrentGuidanceApproval()?.Status == GuidanceApprovalStatus.NeedsCorrection
                        ? c.GetCurrentGuidanceApproval()?.RequestedRevisionReason
                        : null,
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                })
                .ToList();
            return Task.FromResult((IReadOnlyList<GrantCaseListItemDto>)list);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            if (SaveChangesException is not null)
                throw SaveChangesException;

            return Task.CompletedTask;
        }
    }
}
