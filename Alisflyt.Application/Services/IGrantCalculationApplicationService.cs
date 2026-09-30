using System;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Models;

namespace Alisflyt.Application.Services
{
    public interface IGrantCalculationApplicationService
    {
        Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default);
    }
}
