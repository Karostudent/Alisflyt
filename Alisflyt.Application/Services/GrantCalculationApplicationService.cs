using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Application.Services
{
    public class GrantCalculationApplicationService : IGrantCalculationApplicationService
    {
        private readonly IGrantCaseRepository _caseRepository;
        private readonly IGrantRateSetRepository _rateSetRepository;
        private readonly GrantCalculationService _calcService;

        public GrantCalculationApplicationService(
            IGrantCaseRepository caseRepository,
            IGrantRateSetRepository rateSetRepository)
        {
            _caseRepository = caseRepository ?? throw new ArgumentNullException(nameof(caseRepository));
            _rateSetRepository = rateSetRepository ?? throw new ArgumentNullException(nameof(rateSetRepository));
            _calcService = new GrantCalculationService();
        }

        public async Task<GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
        {
            var grantCase = await _caseRepository.GetByIdAsync(caseId, cancellationToken).ConfigureAwait(false);
            if (grantCase is null)
                throw new KeyNotFoundException($"GrantCase with id {caseId} not found.");

            if (string.IsNullOrWhiteSpace(grantCase.ApplicationDataJson))
            {
                return new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Ingen søknadsdata" };
            }

            var app = System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(grantCase.ApplicationDataJson);
            if (app is null)
                return new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Ugyldig søknadsdata" };

            // Map input first so mapping errors are reported as 'Ufullstendig søknad'
            GrantCalculationInput input;
            try
            {
                input = GrantCalculationInputMapper.From(app);
            }
            catch (ArgumentException ex)
            {
                return new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Ufullstendig søknad: " + ex.Message };
            }

            // If there are no employment periods, report missing date for funding period
            if (input.EmploymentPeriods == null || input.EmploymentPeriods.Count == 0)
                return new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Mangler dato for tilskuddsperiode" };

            // Determine relevant date: earliest FundingFrom in mapped employment periods
            var relevantDate = input.EmploymentPeriods.Where(p => p.FundingFrom != default).Select(p => p.FundingFrom).OrderBy(d => d).FirstOrDefault();
            if (relevantDate == default)
                return new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Mangler dato for tilskuddsperiode" };

            // Find rate sets and apply policy (only active rate sets)
            var rateSets = await _rateSetRepository.ListAsync(cancellationToken).ConfigureAwait(false);
            var matching = rateSets.Where(r => r.IsActive && r.ValidFrom <= relevantDate && r.ValidTo >= relevantDate).OrderByDescending(r => r.ValidFrom).ToList();
            if (matching.Count == 0)
            {
                return new GrantCalculationDetailsDto { IsAvailable = false, UnavailableReason = "Ingen gyldig sats for valgt periode" };
            }

            var chosen = matching.First();

            // Calculate
            var result = _calcService.Calculate(input, chosen);

            return new GrantCalculationDetailsDto
            {
                IsAvailable = true,
                PracticeCompensationAmount = result.PracticeCompensationAmount,
                LearningActivitiesAmount = result.LearningActivitiesAmount,
                ProductivityAmount = result.ProductivityAmount,
                GuidanceAmount = result.GuidanceAmount,
                FacilitationAmount = result.FacilitationAmount,
                CentralitySupplementAmount = result.CentralitySupplementAmount,
                CentralityExplanation = result.CentralityExplanation,

                RateSetId = chosen.Id,
                RateSetName = chosen.Name,
                RateSetValidFrom = chosen.ValidFrom,
                RateSetValidTo = chosen.ValidTo
            };
        }

        // Centrality explanation is provided by GrantCalculationService.Calculate(...) -> GrantCalculationResult.CentralityExplanation
    }
}
