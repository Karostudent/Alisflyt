using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Application.Tests
{
    public class GrantCalculationApplicationServiceTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Audit_explains_period_limits_eligibility_caps_and_total(bool centrality)
        {
            var application = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                FirstRegularGpOrLocumDate = new DateOnly(2023, 1, 1),
                AbsenceCompensation = 9000000m,
                LearningActivityExpenses = 9000000m,
                SupervisionExpenses = 100m,
                CentralitySupplementRequestedAmount = 9000000m,
                IsCentralityGrade6 = centrality,
                EmploymentPeriods =
                [
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 50, EmploymentStartDate = new(2023, 1, 1), FundingFrom = new(2023, 7, 1), FundingThrough = new(2024, 3, 31) },
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.GeneralPractitionerOutsideRegularGpScheme, PositionPercentage = 100, EmploymentStartDate = new(2023, 1, 1), FundingFrom = new(2023, 7, 1), FundingThrough = new(2023, 12, 31) }
                ]
            };
            var grantCase = GrantCase.Create(Guid.NewGuid(), "AUDIT", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(application, DateTimeOffset.UtcNow);
            var rateRepo = new FakeRateRepo();
            rateRepo.Items.Add(CreateRateSet("Audit", new(2023, 1, 1), new(2024, 12, 31)));
            var result = await new GrantCalculationApplicationService(new FakeCaseRepo { Case = grantCase }, rateRepo).CalculateForCaseAsync(grantCase.Id);

            decimal Amount(string item) => Assert.Single(result.AuditRows.Where(x => x.Item == item)).Amount;
            Assert.Equal(9m, Amount("Periode 1 – beregningsmåneder"));
            Assert.Equal(1875000m, Amount("Periode 1 – praksismaks"));
            Assert.Equal(0m, Amount("Periode 2 – praksismaks"));
            Assert.Equal(5000m, Amount("Periode 1 – produktivitet"));
            Assert.Equal(0m, Amount("Periode 2 – produktivitet"));
            Assert.Equal(8750m, Amount("Læringsaktiviteter"));
            Assert.Equal(100m, Amount("Veiledning"));
            Assert.Equal(centrality ? 4375m : 0m, Amount("Sentralitetstillegg"));
            Assert.Equal(result.TotalAmount, Amount("Totalt beregnet tilskudd"));
            Assert.Equal((result.PracticeCompensationAmount + result.LearningActivitiesAmount + result.ProductivityAmount + result.GuidanceAmount) * .05m, Amount("Tilrettelegging"));
            Assert.Contains(result.AuditRows, x => x.Item == "Periode 1 – produktivitet" && x.Substitution.Contains("31.12.2023"));
        }

        [Fact]
        public async Task Calculation_uses_values_saved_by_application_and_coordinator_updates()
        {
            var grantCase = GrantCase.Create(Guid.NewGuid(), "SAVED-VALUES", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(new GrantApplicationData
            {
                GrantType = GrantType.AlisAgreementIncludingSupervision,
                Certificate = new() { DoctorSigningPlace = "Oslo", SupervisorSigningPlace = "Oslo" },
                EmploymentPeriods =
                [
                    new() { PositionType = PositionType.RegularGpOrLocum, PositionPercentage = 100,
                        EmploymentStartDate = new(2023, 1, 1), FundingFrom = new(2023, 1, 1), FundingThrough = new(2023, 12, 31) }
                ]
            }, DateTimeOffset.UtcNow);
            grantCase.UpdateCentrality(true, 3000m, DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(new GrantApplicationData
            {
                GrantType = GrantType.AlisAgreementIncludingSupervision,
                Certificate = new() { DoctorSigningPlace = "Oslo", SupervisorSigningPlace = "Oslo" },
                EmploymentPeriods =
                [
                    new() { PositionType = PositionType.RegularGpOrLocum, PositionPercentage = 100,
                        EmploymentStartDate = new(2023, 1, 1), FundingFrom = new(2023, 1, 1), FundingThrough = new(2023, 12, 31) }
                ],
                HasAdditionalSupervisionCosts = true,
                AdditionalSupervisionCosts = 125m
            }, DateTimeOffset.UtcNow);

            var saved = System.Text.Json.JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson!)!;
            var input = GrantCalculationInputMapper.From(saved);
            Assert.True(input.IsCentralityGrade6);
            Assert.Equal(3000m, input.CentralitySupplementRequestedAmount);
            Assert.Equal(125m, input.AdditionalSupervisionCosts);

            var rateRepo = new FakeRateRepo();
            rateRepo.Items.Add(CreateRateSet("Saved values", new(2023, 1, 1), new(2023, 12, 31)));
            var result = await new GrantCalculationApplicationService(new FakeCaseRepo { Case = grantCase }, rateRepo)
                .CalculateForCaseAsync(grantCase.Id);
            Assert.True(result.IsAvailable);
            Assert.Equal(3000m, result.CentralitySupplementAmount);
        }

        private class FakeCaseRepo : IGrantCaseRepository
        {
            public Alisflyt.Domain.Entities.GrantCase? Case;
            public Task AddAsync(GrantCase grantCase, System.Threading.CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddGuidanceApprovalAsync(GuidanceApproval approval, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<GrantCase?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Case);
            public Task<IReadOnlyList<GrantCase>> ListAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<GrantCase>)new List<GrantCase>());
            public Task SaveChangesAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        private class FakeRateRepo : IGrantRateSetRepository
        {
            public List<GrantRateSet> Items = new();
            public Task AddAsync(GrantRateSet rateSet, System.Threading.CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<GrantRateSet?> GetByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
            public Task<IReadOnlyList<GrantRateSet>> ListAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult((IReadOnlyList<GrantRateSet>)Items.OrderByDescending(i => i.ValidFrom).ToList());
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
        public async Task CompleteApplicationWithMatchingRateSet_ReturnsAvailable()
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
                AbsenceCompensation = 10000m,
                LearningActivityExpenses = 5000m,
                SupervisionExpenses = 2000m,
                IsCentralityGrade6 = true,
                CentralitySupplementRequestedAmount = 3000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-1", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var rs = CreateRateSet("RS1", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            rateRepo.Items.Add(rs);

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            Assert.Equal(rs.Id, res.RateSetId);
            Assert.True(res.TotalAmount > 0);
        }

        [Fact]
        public async Task MultipleRateSets_ChooseLatestValidFrom()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                EmploymentPeriods = new List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                {
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2023,6,1), FundingFrom = new DateOnly(2023,6,1), FundingThrough = new DateOnly(2023,12,31) }
                },
                AbsenceCompensation = 10000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-2", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var rs1 = CreateRateSet("RS-old", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            var rs2 = CreateRateSet("RS-new", new DateOnly(2023,6,1), new DateOnly(2023,12,31));
            rateRepo.Items.Add(rs1);
            rateRepo.Items.Add(rs2);

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            Assert.Equal(rs2.Id, res.RateSetId);
        }

        [Fact]
        public async Task InactiveRateSet_IsIgnored()
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
                AbsenceCompensation = 10000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-4", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var rsActive = CreateRateSet("RS-active", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            var rsInactive = CreateRateSet("RS-inactive", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            // set IsActive = false via reflection since model has private setter
            var prop = typeof(Alisflyt.Domain.Entities.GrantRateSet).GetProperty("IsActive");
            prop.SetValue(rsInactive, false);

            rateRepo.Items.Add(rsInactive);
            rateRepo.Items.Add(rsActive);

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            Assert.Equal(rsActive.Id, res.RateSetId);
        }

        [Fact]
        public async Task OnlyInactiveRateSet_Matches_ReturnsUnavailable()
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
                AbsenceCompensation = 10000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-5", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var rsInactive = CreateRateSet("RS-inactive", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            var prop = typeof(Alisflyt.Domain.Entities.GrantRateSet).GetProperty("IsActive");
            prop.SetValue(rsInactive, false);

            rateRepo.Items.Add(rsInactive);

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.False(res.IsAvailable);
            Assert.Contains("mangler et gyldig satssett", res.UnavailableReason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Søknaden kan fortsatt lagres som utkast", res.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NoMatchingRateSet_ReturnsUnavailable()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                EmploymentPeriods = new List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                {
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2030,1,1), FundingFrom = new DateOnly(2030,1,1), FundingThrough = new DateOnly(2030,12,31) }
                },
                AbsenceCompensation = 10000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-3", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            // rateRepo empty
            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.False(res.IsAvailable);
            Assert.Contains("mangler et gyldig satssett", res.UnavailableReason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Søknaden kan fortsatt lagres som utkast", res.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task MissingRateSet_ReturnsClearDraftSafeMessage()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                EmploymentPeriods = new List<Alisflyt.Domain.Forms.EmploymentPeriodInputModel>
                {
                    new() { PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum, PositionPercentage = 100, EmploymentStartDate = new DateOnly(2026, 6, 1), FundingFrom = new DateOnly(2026, 6, 1), FundingThrough = new DateOnly(2026, 6, 20) }
                },
                AbsenceCompensation = 10000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-MISSING-RATE", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.False(res.IsAvailable);
            Assert.Contains("mangler et gyldig satssett", res.UnavailableReason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Søknaden kan fortsatt lagres som utkast", res.UnavailableReason, StringComparison.OrdinalIgnoreCase);
            var saved = System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(grantCase.ApplicationDataJson!)!;
            Assert.Equal(new DateOnly(2026, 6, 20), saved.EmploymentPeriods[0].FundingThrough);
        }

        [Fact]
        public async Task IncompleteApplication_ReturnsUnavailable_NoException()
        {
            var caseRepo = new FakeCaseRepo();
            var rateRepo = new FakeRateRepo();

            var appData = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                // missing EmploymentPeriods
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-4", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var rs = CreateRateSet("RS1", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            rateRepo.Items.Add(rs);

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.False(res.IsAvailable);
            Assert.Contains("Mangler dato", res.UnavailableReason);
        }

        [Fact]
        public async Task CalculationMatchesGrantCalculationServiceAmounts()
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
                AbsenceCompensation = 12000m,
                LearningActivityExpenses = 5000m,
                SupervisionExpenses = 3000m,
                IsCentralityGrade6 = true,
                CentralitySupplementRequestedAmount = 3000m
            };

            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-5", DateTimeOffset.UtcNow);
            grantCase.UpdateApplication(appData, DateTimeOffset.UtcNow);
            caseRepo.Case = grantCase;

            var rs = CreateRateSet("RS1", new DateOnly(2023,1,1), new DateOnly(2023,12,31));
            rateRepo.Items.Add(rs);

            var svc = new GrantCalculationApplicationService(caseRepo, rateRepo);

            var res = await svc.CalculateForCaseAsync(grantCase.Id);

            Assert.True(res.IsAvailable);
            // spot check that components are non-negative and total equals sum
            Assert.True(res.PracticeCompensationAmount >= 0);
            Assert.True(res.LearningActivitiesAmount >= 0);
            Assert.Equal(res.TotalAmount, res.PracticeCompensationAmount + res.LearningActivitiesAmount + res.ProductivityAmount + res.GuidanceAmount + res.FacilitationAmount + res.CentralitySupplementAmount);
        }
    }
}
