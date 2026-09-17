using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Models;

namespace Alisflyt.Application.Services
{
    public interface IGrantCaseApplicationService
    {
        Task<GrantCaseDto> CreateDraftAsync(CreateGrantCaseRequest request, CancellationToken cancellationToken = default);
        Task<GrantCaseDto> UpdateDraftAsync(Guid id, UpdateGrantCaseDraftRequest request, CancellationToken cancellationToken = default);
        Task<GrantCaseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<GrantCaseListItemDto>> ListAsync(CancellationToken cancellationToken = default);
        Task SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
