using System.Globalization;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;

namespace Alisflyt.Application.Services;

// Period limits are obtained from the production calculator with uncapped expenses.
// This keeps the audit aligned with eligibility and period rules used for payment.
internal static class GrantCalculationAudit
{
    public static IReadOnlyList<GrantCalculationAuditRow> Build(GrantCalculationInput input, GrantRateSet rates, GrantCalculationResult result)
    {
        var rows = new List<GrantCalculationAuditRow>();
        static string N(decimal value) => value.ToString("0.############################", CultureInfo.GetCultureInfo("nb-NO"));
        void Add(string item, string rule, string expression, decimal amount) => rows.Add(new(item, rule, expression, amount));
        Add("Lønnssats", "Sats fra valgt satssett", "kr", rates.SalaryRate);
        Add("Praksiskompensasjonsfaktor", "Sats fra valgt satssett", "faktor", rates.PracticeCompensationRate);
        Add("Praksiskompensasjon – timer per år", "Sats fra valgt satssett", "timer", rates.PracticeCompensationMaxHours);
        Add("Læringsaktiviteter – årsgrense", "Sats fra valgt satssett", "kr", rates.LearningActivitiesMaxAmount);
        Add("Produktivitet – årsgrense", "Sats fra valgt satssett", "kr", rates.ProductivityMaxAmount);
        Add("Produktivitet – kvalifikasjonsmåneder", "Sats fra valgt satssett", "måneder", rates.ProductivityEligibilityMonths);
        Add("Veiledningsfaktor", "Sats fra valgt satssett", "faktor", rates.GuidanceRate);
        Add("Veiledning – timer per år", "Sats fra valgt satssett", "timer", rates.GuidanceHoursPerYear);
        Add("Tilretteleggingsfaktor", "Sats fra valgt satssett", "faktor", rates.FacilitationRate);
        Add("Sentralitet – årsgrense", "Sats fra valgt satssett", "kr", rates.CentralitySupplementMaxAmount);
        var calculator = new GrantCalculationService();
        var isAlisAgreement = input.GrantType == GrantType.AlisAgreementIncludingSupervision;
        decimal learningMax = 0, guidanceMax = 0, centralityMax = 0;
        var index = 0;
        foreach (var period in input.EmploymentPeriods)
        {
            index++;
            var days = GrantCalculationService.CountDays(period.FundingFrom, period.FundingThrough);
            var prefix = $"Periode {index}";
            Add(prefix + " – beregningsdager", "Inkludert fra- og til-dato; fast nevner 365 dager", $"{period.FundingFrom:dd.MM.yyyy}–{period.FundingThrough:dd.MM.yyyy}", days);
            // The calculator applies all eligibility rules to this individual period.
            var limits = calculator.Calculate(new GrantCalculationInput
            {
                GrantType = input.GrantType,
                FirstRegularGpOrLocumDate = input.FirstRegularGpOrLocumDate,
                IsCentralityGrade6 = input.IsCentralityGrade6,
                EmploymentPeriods = [period],
                AbsenceCompensation = decimal.MaxValue,
                LearningActivityExpenses = decimal.MaxValue,
                SupervisionExpenses = decimal.MaxValue,
                CentralitySupplementRequestedAmount = decimal.MaxValue
            }, rates);
            string Expression(decimal annual) => $"{N(annual)} × {N(period.PositionPercentage)} / 100 × {days} / 365";
            Add(prefix + " – praksismaks", isAlisAgreement ? "Lønnssats × praksisfaktor × timer × stillingsprosent / 100 × dager / 365" : "Ikke aktuelt for kun veiledning", !isAlisAgreement ? "Ikke beregnet" : period.PositionType == PositionType.GeneralPractitionerOutsideRegularGpScheme ? "Allmennlege utenfor FLO gir ikke praksiskompensasjon" : $"({N(rates.SalaryRate)} × {N(rates.PracticeCompensationRate)} × {N(rates.PracticeCompensationMaxHours)}) × {N(period.PositionPercentage)} / 100 × {days} / 365", limits.PracticeCompensationMaximumAmount);
            Add(prefix + " – læringsmaks", isAlisAgreement ? "Årsgrense × stillingsprosent / 100 × dager / 365" : "Ikke aktuelt for kun veiledning", isAlisAgreement ? Expression(rates.LearningActivitiesMaxAmount) : "Ikke beregnet", limits.LearningActivitiesAmount);
            Add(prefix + " – veiledningsmaks", "Lønnssats × veiledningsfaktor × timer × stillingsprosent / 100 × dager / 365", $"({N(rates.SalaryRate)} × {N(rates.GuidanceRate)} × {N(rates.GuidanceHoursPerYear)}) × {N(period.PositionPercentage)} / 100 × {days} / 365", limits.GuidanceAmount);
            var eligible = isAlisAgreement && input.FirstRegularGpOrLocumDate.HasValue && period.PositionType == PositionType.RegularGpOrLocum
                ? ProductivityEligibilityCalculator.Calculate(input.FirstRegularGpOrLocumDate.Value, rates.ProductivityEligibilityMonths, period.FundingFrom, period.FundingThrough) : null;
            var productivityDays = eligible is null ? 0 : GrantCalculationService.CountDays(eligible.From, eligible.Through);
            Add(prefix + " – produktivitet", isAlisAgreement ? "Kun fastlege/fastlegevikar innen kvalifikasjonsperioden fra første startdato" : "Ikke aktuelt for kun veiledning", !isAlisAgreement ? "Ikke beregnet" : eligible is null ? "Ingen kvalifiserende periode" : $"{eligible.From:dd.MM.yyyy}–{eligible.Through:dd.MM.yyyy}: {N(rates.ProductivityMaxAmount)} × {N(period.PositionPercentage)} / 100 × {productivityDays} / 365", limits.ProductivityAmount);
            Add(prefix + " – sentralitetsmaks", "Krever sentralitetsgrad 6 og ALIS-avtale inkl. veiledning", input.IsCentralityGrade6 && input.GrantType == GrantType.AlisAgreementIncludingSupervision ? Expression(rates.CentralitySupplementMaxAmount) : "Vilkårene er ikke oppfylt", limits.CentralitySupplementAmount);
            learningMax += limits.LearningActivitiesAmount;
            guidanceMax += limits.GuidanceAmount;
            centralityMax += limits.CentralitySupplementAmount;
        }
        void Cap(string item, decimal expense, decimal maximum, decimal amount) => Add(item, "Minste av oppgitt utgift/søkt beløp og summen av periodemaks", $"MIN({N(expense)}; {N(maximum)})", amount);
        if (isAlisAgreement)
        {
            Cap("Praksiskompensasjon", input.AbsenceCompensation, result.PracticeCompensationMaximumAmount, result.PracticeCompensationAmount);
            Cap("Læringsaktiviteter", input.LearningActivityExpenses, learningMax, result.LearningActivitiesAmount);
            Add("Produktivitet", "Sum kvalifiserende periodebeløp. Søkt beløp brukes ikke som tak.", "SUM(produktivitet per periode)", result.ProductivityAmount);
            Add("Tilrettelegging", "Sum av de fire standardelementene × tilretteleggingsfaktor. Ekstra veiledningskostnader inngår ikke i formelen.", $"({N(result.PracticeCompensationAmount)} + {N(result.LearningActivitiesAmount)} + {N(result.ProductivityAmount)} + {N(result.GuidanceAmount)}) × {N(rates.FacilitationRate)}", result.FacilitationAmount);
        }
        else
        {
            Add("Praksiskompensasjon", "Ikke aktuelt for kun veiledning", "Ikke beregnet", 0m);
            Add("Læringsaktiviteter", "Ikke aktuelt for kun veiledning", "Ikke beregnet", 0m);
            Add("Produktivitet", "Ikke aktuelt for kun veiledning", "Ikke beregnet", 0m);
            Add("Tilrettelegging", "Ikke aktuelt for kun veiledning", "Ikke beregnet", 0m);
        }
        Cap("Veiledning", input.SupervisionExpenses, guidanceMax, result.GuidanceAmount);
        if (isAlisAgreement)
        {
            Add("Registrerte veiledningstimer", "Daterte økter innenfor tilskuddsperioden", "SUM(timer)", result.RegisteredGuidanceHours);
            Add("Honorerbare veiledningstimer", "Registrerte timer × (1 + 1,5 / 4)", $"{N(result.RegisteredGuidanceHours)} × 1,375", result.GuidancePaidHours);
            Add("Veilederhonorarsats", "Lønnssats × veiledningsfaktor", $"{N(rates.SalaryRate)} × {N(rates.GuidanceRate)}", result.GuidanceHourlyRate);
            Add("Beregnet veilederhonorar", "Honorerbare timer × veilederhonorarsats", $"{N(result.GuidancePaidHours)} × {N(result.GuidanceHourlyRate)}", result.GuidanceHonorariumAmount);
            Add("Veilederhonorar – maksimalgrense", "Samme periodiserte veiledningsramme som ordinær veiledning", "Maksimum for stilling og periode", result.GuidanceHonorariumMaximumAmount);
            Add("Veilederhonorar – etter maksimalgrense", "MIN(beregnet honorar; maksimalgrense)", $"MIN({N(result.GuidanceHonorariumAmount)}; {N(result.GuidanceHonorariumMaximumAmount)})", result.GuidanceHonorariumCappedAmount);
        }
        if (isAlisAgreement)
            Add("Sentralitetstillegg", "Krever sentralitetsgrad 6 og ALIS-avtale inkl. veiledning", input.IsCentralityGrade6 && input.GrantType == GrantType.AlisAgreementIncludingSupervision ? $"{N(rates.CentralitySupplementMaxAmount)} × stillingsprosent / 100 × dager / 365" : "Vilkårene er ikke oppfylt", result.CentralitySupplementAmount);
        else
            Add("Sentralitetstillegg", "Ikke aktuelt for kun veiledning", "Ikke beregnet", 0m);
        Add("Totalt beregnet tilskudd", "Sum av alle seks beregningsposter", $"{N(result.PracticeCompensationAmount)} + {N(result.LearningActivitiesAmount)} + {N(result.ProductivityAmount)} + {N(result.FacilitationAmount)} + {N(result.GuidanceAmount)} + {N(result.CentralitySupplementAmount)}", result.TotalAmount);
        return rows;
    }
}

