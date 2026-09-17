using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Application.Abstractions
{
    public interface IGrantCaseRepository
    {
        Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default);
        Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
