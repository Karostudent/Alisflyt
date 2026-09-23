using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Application.Abstractions
{
    public interface IGrantRateSetRepository
    {
        Task<GrantRateSet?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<GrantRateSet>> ListAsync(
            CancellationToken cancellationToken = default);

        Task AddAsync(
            GrantRateSet rateSet,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}