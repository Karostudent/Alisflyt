using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alisflyt.Application.Services;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;
using Xunit;

namespace Alisflyt.Application.Tests
{
    public class GrantCalculationCentralityExplanationTests
    {
        private class FakeCaseRepo : IGrantCaseRepository
        {
            public GrantCase? Case;
            public Task AddAsync(GrantCase grantCase, System.Threading.CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<GrantCase?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Case);
            public Task<IReadOnlyList<GrantCase>> ListAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<GrantCase>)new List<GrantCase>());
            public Task SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeRateRepo : IGrantRateSetRepository
        {
            public List<GrantRateSet> Items = new();
            public Task AddAsync(GrantRateSet rateSet, System.Threading.CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<GrantRateSet?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Items.Find(i => i.Id == id));
            public Task<IReadOnlyList<GrantRateSet>> ListAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<GrantRateSet>)Items);
            public Task SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private static GrantRateSet CreateRateSet(string name, DateOnly validFrom, DateOnly validTo)
        {
            return GrantRateSet.Create(Guid.NewGuid(), name, validFrom, validTo,
                salaryRate: 500000m,
                practiceCompensationRate: 0.1m,
                practiceCompensationMaxHours: 100m,
                learningActivitiesMaxAmount: 10000m,
                productivityMaxAmount: 20000m,
                productivityEligibilityMonths: 12,
                guidanceRate: 0.05m,
                guidanceHoursPerYear: 10m,
                facilitationRate: 0.05m,
                centralitySupplementMaxAmount: 5000m,
                createdAtUtc: DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task Centrality_Explanation_For_NonAlisGrantType()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.SupervisionOnly,
                EmploymentPeriods = new List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                {
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2023,1,1), FundingFrom = new DateOnly(2023,1,1), FundingThrough = new DateOnly(2023,12,31) }
                },
                IsCentralityGrade6 = true,
                CentralitySupplementRequestedAmount = 1000m
            };

            var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-C1", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            rateRepo.Items.Add(CreateRateSet("RS", new DateOnly(2023,1,1), new DateOnly(2023,12,31)));

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);
            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            Assert.Equal(0m, res.CentralitySupplementAmount);
            Assert.False(string.IsNullOrWhiteSpace(res.CentralityExplanation));
        }

        [Fact]
        public async Task Centrality_Explanation_For_NotGrade6()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                EmploymentPeriods = new List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                {
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2023,1,1), FundingFrom = new DateOnly(2023,1,1), FundingThrough = new DateOnly(2023,12,31) }
                },
                IsCentralityGrade6 = false,
                CentralitySupplementRequestedAmount = 1000m
            };

            var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-C2", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            rateRepo.Items.Add(CreateRateSet("RS", new DateOnly(2023,1,1), new DateOnly(2023,12,31)));

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);
            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            Assert.Equal(0m, res.CentralitySupplementAmount);
            Assert.False(string.IsNullOrWhiteSpace(res.CentralityExplanation));
        }

        [Fact]
        public async Task Centrality_NoExplanation_When_Amount_Positive()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                EmploymentPeriods = new List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                {
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2023,1,1), FundingFrom = new DateOnly(2023,1,1), FundingThrough = new DateOnly(2023,12,31) }
                },
                IsCentralityGrade6 = true,
                CentralitySupplementRequestedAmount = 5000m
            };

            var grantCase = GrantCase.Create(Guid.NewGuid(), "CASE-C3", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            rateRepo.Items.Add(CreateRateSet("RS", new DateOnly(2023,1,1), new DateOnly(2023,12,31)));

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);
            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            Assert.True(res.CentralitySupplementAmount > 0);
            Assert.True(string.IsNullOrWhiteSpace(res.CentralityExplanation));
        }
    }
}
