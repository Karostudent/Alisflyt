using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;

namespace Alisflyt.Application.Services
{
    public class GuidanceApprovalService(IGrantCaseRepository repository, TimeProvider timeProvider) : IGuidanceApprovalService
    {
        private readonly IGrantCaseRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        public async Task<GuidanceApproval> RequestApprovalAsync(
            Guid grantCaseId,
            string doctorHprNumber,
            string supervisorHprNumber,
            string supervisorUserKey,
            string? supervisorName,
            CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(grantCaseId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"GrantCase with id {grantCaseId} not found.");

            var approval = grantCase.RequestGuidanceApproval(
                doctorHprNumber,
                supervisorHprNumber,
                supervisorUserKey,
                _timeProvider.GetUtcNow(),
                supervisorName);

            await _repository.AddGuidanceApprovalAsync(approval, cancellationToken).ConfigureAwait(false);
            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return approval;
        }

        public async Task<GuidanceApproval> ApproveAsync(
            Guid grantCaseId,
            Guid guidanceApprovalId,
            string approvedByUserKey,
            CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(grantCaseId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"GrantCase with id {grantCaseId} not found.");

            var approval = grantCase.GuidanceApprovals.FirstOrDefault(a => a.Id == guidanceApprovalId)
                ?? throw new KeyNotFoundException($"Guidance approval with id {guidanceApprovalId} not found.");

            if (approval.Status != GuidanceApprovalStatus.Pending)
                throw new InvalidOperationException("Only a pending guidance approval can be approved.");

            if (!string.Equals(approval.SupervisorUserKey, approvedByUserKey, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only the assigned supervisor can approve this guidance approval.");

            var now = _timeProvider.GetUtcNow();
            approval.Approve(now, approvedByUserKey);
            grantCase.SynchronizeLegacySupervisorApproval(now, now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return approval;
        }

        public async Task<GuidanceApproval> RequestCorrectionAsync(
            Guid grantCaseId,
            Guid guidanceApprovalId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(grantCaseId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"GrantCase with id {grantCaseId} not found.");

            var approval = grantCase.GuidanceApprovals.FirstOrDefault(a => a.Id == guidanceApprovalId)
                ?? throw new KeyNotFoundException($"Guidance approval with id {guidanceApprovalId} not found.");

            if (approval.Status != GuidanceApprovalStatus.Pending)
                throw new InvalidOperationException("Only a pending guidance approval can be returned for correction.");

            var now = _timeProvider.GetUtcNow();
            approval.RequestCorrection(reason, now);
            grantCase.SynchronizeLegacySupervisorApproval(null, now);

            await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return approval;
        }

        public async Task<GuidanceApproval?> GetLatestApprovalAsync(Guid grantCaseId, CancellationToken cancellationToken = default)
        {
            var grantCase = await _repository.GetByIdAsync(grantCaseId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"GrantCase with id {grantCaseId} not found.");

            return grantCase.GetCurrentGuidanceApproval();
        }
    }
}
