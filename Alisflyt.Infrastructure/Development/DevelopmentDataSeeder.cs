using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Microsoft.EntityFrameworkCore;

namespace Alisflyt.Infrastructure.Development
{
    public class DevelopmentDataSeeder
    {
        // fixed GUIDs and case numbers
        private static readonly (Guid Id, string CaseNumber)[] SeedCases = new[]
        {
            (new Guid("11111111-1111-1111-1111-111111111111"), "ALIS-DEM-001"),
            (new Guid("22222222-2222-2222-2222-222222222222"), "ALIS-DEM-002"),
            (new Guid("33333333-3333-3333-3333-333333333333"), "ALIS-DEM-003"),
            (new Guid("44444444-4444-4444-4444-444444444444"), "ALIS-DEM-004")
        };

        private static readonly Guid GrantRateSet2025_2026Id =
            new("55555555-5555-5555-5555-555555555555");

        public static async Task SeedAsync(Persistence.ApplicationDbContext context, System.TimeProvider timeProvider, CancellationToken cancellationToken = default)
        {
            var now = timeProvider.GetUtcNow();
            if (!await context.GrantRateSets.AnyAsync(x => x.Id == GrantRateSet2025_2026Id, cancellationToken))
            {
                var rateSet = GrantRateSet.Create(
                    GrantRateSet2025_2026Id,
                    "Tilskudd 2025/2026",
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2026, 5, 31),
                    1375m,     // SalaryRate
                    0.60m,     // PracticeCompensationRate
                    160m,      // PracticeCompensationMaxHours
                    14000m,    // LearningActivitiesMaxAmount
                    125000m,   // ProductivityMaxAmount
                    24,         // ProductivityEligibilityMonths
                    1.15m,     // GuidanceRate
                    57.75m,    // GuidanceHoursPerYear
                    0.05m,     // FacilitationRate
                    200000m,   // CentralitySupplementMaxAmount
                    now);

                await context.GrantRateSets
                    .AddAsync(rateSet, cancellationToken)
                    .ConfigureAwait(false);
            }

            // Replace the four fixed demo cases on startup, including older seeded data.
            foreach (var (id, caseNumber) in SeedCases)
            {
                var existingCase = await context.GrantCases.SingleOrDefaultAsync(
                    c => c.Id == id && c.CaseNumber == caseNumber, cancellationToken);
                if (existingCase is null && await context.GrantCases.AnyAsync(
                    c => c.Id == id || c.CaseNumber == caseNumber, cancellationToken))
                    continue;

                var gc = GrantCase.Create(id, caseNumber, existingCase?.CreatedAtUtc ?? now);

                var application = CreateApplication(caseNumber);
                gc.UpdateApplication(application, now);

                if (caseNumber.EndsWith("003") || caseNumber.EndsWith("004"))
                {
                    gc.ApproveCertificate(now);
                    gc.Submit(now);
                }
                if (caseNumber.EndsWith("004"))
                    gc.StartReview(now);

                if (existingCase is null)
                    await context.GrantCases.AddAsync(gc, cancellationToken).ConfigureAwait(false);
                else
                    context.Entry(existingCase).CurrentValues.SetValues(gc);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private static GrantApplicationData CreateApplication(string caseNumber)
        {
            var isMaximum = caseNumber.EndsWith("003");
            var isIntroductory = caseNumber.EndsWith("002");
            var isSupervisionOnly = caseNumber.EndsWith("004");
            var name = caseNumber[^3..] switch
            {
                "001" => "Emma Hansen",
                "002" => "Sara Berg",
                "003" => "Martin Nilsen",
                _ => "Jonas Dahl"
            };
            var positionType = isSupervisionOnly
                ? PositionType.GeneralPractitionerOutsideRegularGpScheme
                : isIntroductory ? PositionType.IntroductoryDoctor : PositionType.RegularGpOrLocum;
            var from = isMaximum ? new DateOnly(2025, 6, 1) : new DateOnly(2025, 12, 1);
            var through = new DateOnly(2026, 5, 31);
            var agreement = !isSupervisionOnly;

            var data = new GrantApplicationData
            {
                HprNumber = $"HPR-DEM-{caseNumber[^1]}",
                DoctorName = name,
                DoctorProfessions = ["Lege"],
                GrantType = agreement ? GrantType.AlisAgreementIncludingSupervision : GrantType.SupervisionOnly,
                ConfirmsSpecialization = true,
                SpecializationStartDate = from,
                ExpectedCompletionDate = from.AddYears(5),
                ConfirmsAlisAgreement = agreement,
                ConfirmsOfficialAgreementTemplate = agreement,
                ConfirmsNoCommercialAgencyAffiliation = agreement,
                AgreementEffectiveFrom = agreement ? from : null,
                FirstRegularGpOrLocumDate = positionType == PositionType.RegularGpOrLocum ? from : null,
                SelectedPositionTypes = [positionType],
                EmploymentPeriods =
                [
                    new()
                    {
                        PositionType = positionType,
                        PositionPercentage = isMaximum ? 100m : isSupervisionOnly ? 80m : 50m,
                        EmploymentStartDate = from,
                        FundingFrom = from,
                        FundingThrough = through
                    }
                ],
                // Full-year, full-time expenses exceed the caps to demonstrate the limits.
                AbsenceCompensation = isMaximum ? 150000m : isSupervisionOnly ? 0m : 70000m,
                LearningActivityExpenses = isSupervisionOnly ? 0m : 70000m,
                ProductivityRequestedAmount = isMaximum ? 125000m : positionType == PositionType.RegularGpOrLocum ? 70000m : 0m,
                SupervisionExpenses = isMaximum ? 100000m : 70000m,
                HasAdditionalSupervisionCosts = false,
                IsCentralityGrade6 = false,
                CentralitySupplementRequestedAmount = null,
                Certificate = new()
                {
                    DoctorName = name,
                    SupervisorHprNumber = "HPR-DEM-VEILEDER",
                    SupervisorName = "Anne Solberg",
                    DoctorSigningPlace = "Kristiansand",
                    SupervisorSigningPlace = "Kristiansand",
                    DoctorSigningDate = through,
                    SupervisorSigningDate = through,
                    Sessions = Enumerable.Range(0, isMaximum ? 20 : isIntroductory ? 10 : isSupervisionOnly ? 16 : 12)
                        .Select(session => new SupervisionSessionInputModel
                        {
                            Date = from.AddMonths(session / 4).AddDays(6 * (session % 4) + 3),
                            Hours = (session % 4) switch { 0 => 1m, 1 => 1m, 2 => 2m, _ => 3m },
                            Topic = session % 2 == 0 ? "Kliniske problemstillinger og pasientoppfølging" : "Læringsmål og faglig utvikling"
                        }).ToList()
                }
            };

            data.ValidateSubmission();

            return data;
        }
    }
}
