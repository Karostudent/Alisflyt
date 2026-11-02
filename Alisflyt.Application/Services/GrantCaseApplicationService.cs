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
        private readonly GrantCaseAccessContext? _accessContext;

        public GrantCaseApplicationService(IGrantCaseRepository repository, ICaseNumberGenerator caseNumberGenerator, TimeProvider timeProvider, GrantCaseAccessContext? accessContext = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _caseNumberGenerator = caseNumberGenerator ?? throw new ArgumentNullException(nameof(caseNumberGenerator));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _accessContext = accessContext;
        }

        public async Task<GrantCaseDto> CreateDraftAsync(CreateGrantCaseRequest request, CancellationToken cancellationToken = default)
        {
            if (_accessContext is not null && !_accessContext.IsAlis)
                throw new UnauthorizedAccessException("Only ALIS users can create grant cases.");

            var caseNumber = await _caseNumberGenerator.GenerateAsync(cancellationToken).ConfigureAwait(false);
            var now = _timeProvider.GetUtcNow();
            var id = Guid.NewGuid();

            var grantCase = Domain.Entities.GrantCase.Create(id, caseNumber, now, _accessContext?.UserId);
            // allow incomplete draft per rules
            if (request.ApplicationData is not null)
                grantCase.UpdateApplication(request.ApplicationData, now);
            else
                grantCase.UpdateDraft(request.HprNumber, request.EmploymentPercentage, request.EmploymentStartDate, request.EmploymentEndDate, now);

            await _repository.AddAsync(grantCase, cancellationToken).ConfigureAwait(false);
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return MapToDto(grantCase);
        }

        public async Task<GrantCaseDto> UpdateDraftAsync(Guid id, UpdateGrantCaseDraftRequest request, CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            var now = _timeProvider.GetUtcNow();

            if (request.ApplicationData is not null)
                grantCase.UpdateApplication(request.ApplicationData, now);
            else
                grantCase.UpdateDraft(request.HprNumber, request.EmploymentPercentage, request.EmploymentStartDate, request.EmploymentEndDate, now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return MapToDto(grantCase);
        }

        public async Task<GrantCaseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            return MapToDto(grantCase);
        }

        public async Task<GuidanceCorrectionResult> UpdateGuidanceAsync(
            Guid id,
            IReadOnlyCollection<Alisflyt.Domain.Forms.SupervisionSessionInputModel> sessions,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sessions);

            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            var previousApproval = grantCase.GetCurrentGuidanceApproval();
            var approval = grantCase.UpdateGuidanceSessions(sessions, _timeProvider.GetUtcNow());
            var createdNewApproval = previousApproval is null || previousApproval.Id != approval.Id;
            if (createdNewApproval)
                await _repository.AddGuidanceApprovalAsync(approval, cancellationToken).ConfigureAwait(false);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new GuidanceCorrectionResult(MapToDto(grantCase), createdNewApproval);
        }

        public async Task StartReviewAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            var now = _timeProvider.GetUtcNow();

            grantCase.StartReview(now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task MarkReadyForAuditorApprovalAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            grantCase.MarkReadyForAuditorApproval(_timeProvider.GetUtcNow());
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task ReturnForCorrectionAsync(Guid id, ReturnForCorrectionRequest request, CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            var now = _timeProvider.GetUtcNow();

            grantCase.ReturnForCorrection(request.Reason, now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<GrantCaseListItemDto>> ListAsync(CancellationToken cancellationToken = default)
        {
            var list = _accessContext is null
                ? await _repository.ListAsync(cancellationToken).ConfigureAwait(false)
                : _accessContext.CanAccessGrantCases
                    ? await _repository.ListAccessibleAsync(_accessContext.UserId, _accessContext.IsDepartmentManager, cancellationToken).ConfigureAwait(false)
                    : Array.Empty<GrantCase>();
            return list.Select(c =>
            {
                var currentApproval = c.GetCurrentGuidanceApproval();
                return new GrantCaseListItemDto
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    GuidanceApprovalStatus = currentApproval?.Status,
                    GuidanceCorrectionReason = currentApproval?.Status == Alisflyt.Domain.Enums.GuidanceApprovalStatus.NeedsCorrection
                        ? currentApproval.RequestedRevisionReason
                        : null,
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                };
            }).ToList();
        }

        public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            var applicationData = grantCase.ApplicationDataJson is null
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(grantCase.ApplicationDataJson);

            if (applicationData?.Certificate?.Sessions?.Count > 0)
                throw new InvalidOperationException("Søknader med veiledningstimer må sendes inn med nødvendig veiledergodkjenning.");

            await SubmitAsync(id, guidanceRequest: null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<GrantCaseDto> SubmitAsync(
            Guid id,
            SubmitGrantCaseRequest? guidanceRequest,
            CancellationToken cancellationToken = default)
        {
            var grantCase = await GetAccessibleByIdAsync(id, cancellationToken);
            var now = _timeProvider.GetUtcNow();
            var applicationData = grantCase.ApplicationDataJson is null
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(grantCase.ApplicationDataJson);
            if (guidanceRequest is null && applicationData?.Certificate?.Sessions?.Count > 0)
                throw new InvalidOperationException("Søknader med veiledningstimer må sendes inn med nødvendig veiledergodkjenning.");

            if (guidanceRequest is not null)
            {
                var approval = grantCase.RequestGuidanceApproval(
                    guidanceRequest.DoctorHprNumber ?? string.Empty,
                    guidanceRequest.SupervisorHprNumber ?? string.Empty,
                    guidanceRequest.SupervisorUserKey ?? string.Empty,
                    now,
                    guidanceRequest.SupervisorName);

                await _repository.AddGuidanceApprovalAsync(approval, cancellationToken).ConfigureAwait(false);
            }

            grantCase.Submit(now);
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapToDto(grantCase);
        }

        private static GrantCaseDto MapToDto(GrantCase c)
        {
            return new GrantCaseDto
            {
                ApplicationData = c.ApplicationDataJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(c.ApplicationDataJson),
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
                LastModifiedAtUtc = c.LastModifiedAtUtc,
                ReadyForAuditorApprovalAtUtc = c.ReadyForAuditorApprovalAtUtc,
                GuidanceApprovalRequired = c.RequiresGuidanceApproval(),
                GuidanceApprovalApproved = c.HasApprovedCurrentGuidanceApproval()
            };
        }

        private async Task<GrantCase> GetAccessibleByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            if (_accessContext is not null && !_accessContext.CanAccessGrantCases)
                throw new KeyNotFoundException($"GrantCase with id {id} not found.");

            var grantCase = _accessContext is null
                ? await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                : await _repository.GetAccessibleByIdAsync(
                    id,
                    _accessContext.UserId,
                    _accessContext.IsDepartmentManager,
                    cancellationToken).ConfigureAwait(false);
            return grantCase ?? throw new KeyNotFoundException($"GrantCase with id {id} not found.");
        }
    }
}
