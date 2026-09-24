

namespace Alisflyt.Domain.Forms;

// Editable form data is kept separate from persistence and authenticated identity.
// Draft fields remain nullable. Submission validation belongs in a dedicated service.
public class GrantApplicationData
{
    public void ValidateDraft()
    {
        if (Certificate is null || Certificate.Sessions is null || EmploymentPeriods is null
            || DoctorProfessions is null || SelectedPositionTypes is null)
            throw new ArgumentException("Skjemaet mangler opplysninger.");
        if (HprNumber?.Length > 50 || (GrantType.HasValue && !Enum.IsDefined(GrantType.Value))
            || SelectedPositionTypes.Any(p => !Enum.IsDefined(p)))
            throw new ArgumentException("Kontroller HPR-nummer og valgte typer.");
        if (AbsenceCompensation < 0 || LearningActivityExpenses < 0 || SupervisionExpenses < 0 || AdditionalSupervisionCosts < 0)
            throw new ArgumentException("Beløp kan ikke være negative.");
        foreach (var period in EmploymentPeriods)
        {
            if (period is null || period.PositionPercentage is <= 0 or > 100
                || (period.PositionType.HasValue && !Enum.IsDefined(period.PositionType.Value))
                || period.FundingThrough < period.FundingFrom || period.FundingThrough < period.EmploymentStartDate)
                throw new ArgumentException("Kontroller stillingsprosent og datoer i stillingsperiodene.");
        }
        if (Certificate.Sessions.Any(s => s is null || s.Hours is <= 0 or > 24))
            throw new ArgumentException("Veiledningstimer må være større enn 0 og høyst 24.");
        if (ExpectedCompletionDate < SpecializationStartDate)
            throw new ArgumentException("Avslutning av spesialisering kan ikke være før startdato.");
    }

    public void ValidateSubmission()
    {
        ValidateDraft();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(HprNumber)) errors.Add("Oppgi HPR-nummer for legen.");
        if (!ConfirmsSpecialization) errors.Add("Bekreft at legen er under spesialisering i allmennmedisin i ny ordning.");
        if (!SpecializationStartDate.HasValue) errors.Add("Oppgi når legen startet spesialiseringsløpet.");
        if (!ExpectedCompletionDate.HasValue) errors.Add("Oppgi når legen forventes å avslutte spesialiseringsløpet.");
        if (!GrantType.HasValue) errors.Add("Velg hva tilskuddet gjelder: ALIS-avtale eller veiledning av ALIS.");
        if (!HasAdditionalSupervisionCosts.HasValue) errors.Add("Svar ja eller nei på om dere har hatt merkostnader for økt antall veiledningstimer.");
        if (SelectedPositionTypes.Count == 0) errors.Add("Velg hvilke stillingstyper legen har hatt i perioden.");
        if (EmploymentPeriods.Count == 0) errors.Add("Legg til minst én stillingsperiode under «Stilling i kommunen».");
        if (GrantType == Forms.GrantType.AlisAgreementIncludingSupervision)
        {
            if (!ConfirmsAlisAgreement) errors.Add("Bekreft at det er inngått ALIS-avtale mellom legen og kommunen.");
            if (!ConfirmsOfficialAgreementTemplate) errors.Add("Bekreft at Helsedirektoratets mal for ALIS-avtale er benyttet.");
            if (!ConfirmsNoCommercialAgencyAffiliation) errors.Add("Bekreft at legen ikke er ansatt eller tilknyttet vikarbyrå eller annen privat kommersiell aktør innen medisinsk virksomhet.");
            if (!AgreementEffectiveFrom.HasValue) errors.Add("Oppgi når ALIS-avtalen gjelder fra.");
        }
        if (HasAdditionalSupervisionCosts == true && !AdditionalSupervisionCosts.HasValue)
            errors.Add("Oppgi beløpet for merkostnader for veiledning.");
        for (var i = 0; i < EmploymentPeriods.Count; i++)
        {
            var p = EmploymentPeriods[i];
            var row = $"Stilling i kommunen, rad {i + 1}: ";
            if (!p.PositionType.HasValue) errors.Add(row + "Velg stillingstype.");
            else if (!SelectedPositionTypes.Contains(p.PositionType.Value)) errors.Add(row + "Stillingstypen må også være krysset av under hvilke stillinger legen har hatt.");
            if (!p.PositionPercentage.HasValue) errors.Add(row + "Oppgi stillingsprosent.");
            if (!p.EmploymentStartDate.HasValue) errors.Add(row + "Oppgi dato for tiltredelse.");
            if (!p.FundingFrom.HasValue) errors.Add(row + "Oppgi «Tilskudd fra».");
            if (!p.FundingThrough.HasValue) errors.Add(row + "Oppgi «Tilskudd til og med».");
        }
        foreach (var type in SelectedPositionTypes.Distinct().Where(t => !EmploymentPeriods.Any(p => p.PositionType == t)))
        {
            var label = type switch
            {
                PositionType.RegularGpOrLocum => "Fastlege/fastlegevikar",
                PositionType.IntroductoryDoctor => "Introduksjonslege",
                _ => "Allmennlege utenfor FLO"
            };
            errors.Add($"Legg til en stillingsperiode for den valgte stillingstypen «{label}».");
        }
        if (errors.Count > 0) throw new SubmissionValidationException(errors);
    }
    public SupervisionCertificateViewModel Certificate { get; set; } = new();
    public string? HprNumber { get; set; }

    // Entered manually until HPR lookup is implemented; not registry-verified.
    public string? DoctorName { get; set; }
    public List<string> DoctorProfessions { get; set; } = [];

    public GrantType? GrantType { get; set; }
    public bool ConfirmsSpecialization { get; set; }
    public DateOnly? SpecializationStartDate { get; set; }
    public DateOnly? ExpectedCompletionDate { get; set; }

    public bool ConfirmsAlisAgreement { get; set; }
    public bool ConfirmsOfficialAgreementTemplate { get; set; }
    public bool ConfirmsNoCommercialAgencyAffiliation { get; set; }
    public DateOnly? AgreementEffectiveFrom { get; set; }

    // UI selections must be reconciled with employment rows before persistence.
    public List<PositionType> SelectedPositionTypes { get; set; } = [];
    public List<EmploymentPeriodInputModel> EmploymentPeriods { get; set; } = [];

    public decimal? AbsenceCompensation { get; set; }
    public decimal? LearningActivityExpenses { get; set; }
    public decimal? SupervisionExpenses { get; set; }
    public bool? HasAdditionalSupervisionCosts { get; set; }
    public decimal? AdditionalSupervisionCosts { get; set; }
}
