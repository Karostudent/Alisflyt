using System;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Application.Services
{
    public interface IGuidanceApprovalService
    {
        Task<GuidanceApproval> RequestApprovalAsync(
            Guid grantCaseId,
            string doctorHprNumber,
            string supervisorHprNumber,
            string supervisorUserKey,
            string? supervisorName,
            CancellationToken cancellationToken = default);

        Task<GuidanceApproval> ApproveAsync(
            Guid grantCaseId,
            Guid guidanceApprovalId,
            string approvedByUserKey,
            CancellationToken cancellationToken = default);

        Task<GuidanceApproval> RequestCorrectionAsync(
            Guid grantCaseId,
            Guid guidanceApprovalId,
            string reason,
            CancellationToken cancellationToken = default);

        Task<GuidanceApproval?> GetLatestApprovalAsync(Guid grantCaseId, CancellationToken cancellationToken = default);
    }
}
