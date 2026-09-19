using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Services;
using Alisflyt.Application.Models;

namespace Alisflyt.Web.Tests.Fakes
{
    internal class FakeGrantCaseApplicationService : IGrantCaseApplicationService
    {
        // Configurable responses
        public GrantCaseDto? GetByIdResponse { get; set; }
        public IReadOnlyList<GrantCaseListItemDto>? ListResponse { get; set; }
        public GrantCaseDto? CreateDraftResponse { get; set; }
        public GrantCaseDto? UpdateDraftResponse { get; set; }

        // Configurable exceptions
        public Exception? CreateDraftException { get; set; }
        public Exception? UpdateDraftException { get; set; }
        public Exception? GetByIdException { get; set; }
        public Exception? ListException { get; set; }
        public Exception? SubmitException { get; set; }

        // Call recording
        public List<(CreateGrantCaseRequest request, CancellationToken ct)> CreateDraftCalls { get; } = new();
        public List<(Guid id, UpdateGrantCaseDraftRequest request, CancellationToken ct)> UpdateDraftCalls { get; } = new();
        public List<(Guid id, CancellationToken ct)> GetByIdCalls { get; } = new();
        public List<CancellationToken> ListCalls { get; } = new();
        public List<(Guid id, CancellationToken ct)> SubmitCalls { get; } = new();

        public Task<GrantCaseDto> CreateDraftAsync(CreateGrantCaseRequest request, CancellationToken cancellationToken = default)
        {
            CreateDraftCalls.Add((request, cancellationToken));
            if (CreateDraftException != null) throw CreateDraftException;
            if (CreateDraftResponse != null) return Task.FromResult(CreateDraftResponse);
            throw new InvalidOperationException("CreateDraftResponse was not configured for this test.");
        }

        public Task<GrantCaseDto> UpdateDraftAsync(Guid id, UpdateGrantCaseDraftRequest request, CancellationToken cancellationToken = default)
        {
            UpdateDraftCalls.Add((id, request, cancellationToken));
            if (UpdateDraftException != null) throw UpdateDraftException;
            if (UpdateDraftResponse != null) return Task.FromResult(UpdateDraftResponse);
            throw new InvalidOperationException("UpdateDraftResponse was not configured for this test.");
        }

        public Task<GrantCaseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCalls.Add((id, cancellationToken));
            if (GetByIdException != null) throw GetByIdException;
            if (GetByIdResponse != null) return Task.FromResult(GetByIdResponse);
            throw new InvalidOperationException("GetByIdResponse was not configured for this test.");
        }

        public Task<IReadOnlyList<GrantCaseListItemDto>> ListAsync(CancellationToken cancellationToken = default)
        {
            ListCalls.Add(cancellationToken);
            if (ListException != null) throw ListException;
            if (ListResponse != null) return Task.FromResult(ListResponse);
            // Safe default: empty list
            var list = new List<GrantCaseListItemDto>();
            return Task.FromResult((IReadOnlyList<GrantCaseListItemDto>)list);
        }

        public Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            SubmitCalls.Add((id, cancellationToken));
            if (SubmitException != null) throw SubmitException;
            return Task.CompletedTask;
        }
    }
}
