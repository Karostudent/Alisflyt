using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Application.Tests.Fakes
{
    internal class FakeGrantCaseRepository : IGrantCaseRepository
    {
        private readonly ConcurrentDictionary<Guid, GrantCase> _store = new();
        public int AddAsyncCallCount { get; private set; }
        public int SaveChangesCallCount { get; private set; }

        public Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default)
        {
            _store[grantCase.Id] = grantCase;
            AddAsyncCallCount++;
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

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // in-memory store already updated
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
