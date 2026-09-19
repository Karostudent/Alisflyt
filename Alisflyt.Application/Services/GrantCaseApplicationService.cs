using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Application.Services
{
    public class GrantCaseApplicationService : IGrantCaseApplicationService
    {
        private readonly IGrantCaseRepository _repository;
        private readonly ICaseNumberGenerator _caseNumberGenerator;
        private readonly TimeProvider _timeProvider;

        public GrantCaseApplicationService(IGrantCaseRepository repository, ICaseNumberGenerator caseNumberGenerator, TimeProvider timeProvider)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _caseNumberGenerator = caseNumberGenerator ?? throw new ArgumentNullException(nameof(caseNumberGenerator));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<GrantCaseDto> CreateDraftAsync(CreateGrantCaseRequest request, CancellationToken cancellationToken = default)
        {
            var caseNumber = await _caseNumberGenerator.GenerateAsync(cancellationToken).ConfigureAwait(false);
            var now = _timeProvider.GetUtcNow();
            var id = Guid.NewGuid();

            var grantCase = Domain.Entities.GrantCase.Create(id, caseNumber, now);
            // allow incomplete draft per rules
            grantCase.UpdateDraft(request.HprNumber, request.EmploymentPercentage, request.EmploymentStartDate, request.EmploymentEndDate, now);

            await _repository.AddAsync(grantCase, cancellationToken).ConfigureAwait(false);
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return MapToDto(grantCase);
        }

        public async Task<GrantCaseDto> UpdateDraftAsync(Guid id, UpdateGrantCaseDraftRequest request, CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"GrantCase with id {id} not found.");
            var now = _timeProvider.GetUtcNow();

            grantCase.UpdateDraft(request.HprNumber, request.EmploymentPercentage, request.EmploymentStartDate, request.EmploymentEndDate, now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return MapToDto(grantCase);
        }

        public async Task<GrantCaseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"GrantCase with id {id} not found.");
            return MapToDto(grantCase);
        }

        public async Task StartReviewAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"GrantCase with id {id} not found.");
            var now = _timeProvider.GetUtcNow();

            grantCase.StartReview(now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ReturnForCorrectionAsync(Guid id, ReturnForCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"GrantCase with id {id} not found.");
            var now = _timeProvider.GetUtcNow();

            grantCase.ReturnForCorrection(request.Reason, now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<GrantCaseListItemDto>> ListAsync(CancellationToken cancellationToken = default)
        {
            var list = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
            return list.Select(c => new GrantCaseListItemDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                HprNumber = c.HprNumber,
                Status = c.Status,
                LastModifiedAtUtc = c.LastModifiedAtUtc
            }).ToList();
        }

        public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"GrantCase with id {id} not found.");
            var now = _timeProvider.GetUtcNow();

            grantCase.Submit(now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private static GrantCaseDto MapToDto(GrantCase c)
        {
            return new GrantCaseDto
            {
                Id = c.Id,
                CaseNumber = c.CaseNumber,
                HprNumber = c.HprNumber,
                EmploymentPercentage = c.EmploymentPercentage,
                EmploymentStartDate = c.EmploymentStartDate,
                EmploymentEndDate = c.EmploymentEndDate,
                ReturnReason = c.ReturnReason,
                ReturnedAtUtc = c.ReturnedAtUtc,
                Status = c.Status,
                CreatedAtUtc = c.CreatedAtUtc,
                LastModifiedAtUtc = c.LastModifiedAtUtc
            };
        }
    }
}
