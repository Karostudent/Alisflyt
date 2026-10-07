using System;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Domain.Entities
{
    public class GrantCase
    {
        public string? ApplicationDataJson { get; private set; }
        public Guid Id { get; private set; }
        public string CaseNumber { get; private set; }
        public string? HprNumber { get; private set; }
        public decimal? EmploymentPercentage { get; private set; }
        public DateOnly? EmploymentStartDate { get; private set; }
        public DateOnly? EmploymentEndDate { get; private set; }
        public string? ReturnReason { get; private set; }
        public DateTimeOffset? ReturnedAtUtc { get; private set; }
        public GrantCaseStatus Status { get; private set; }
        public DateTimeOffset CreatedAtUtc { get; private set; }
        public DateTimeOffset LastModifiedAtUtc { get; private set; }

        private GrantCase(Guid id, string caseNumber, DateTimeOffset createdAtUtc)
        {
            Id = id;
            CaseNumber = caseNumber ?? throw new ArgumentNullException(nameof(caseNumber));
            CreatedAtUtc = createdAtUtc;
            LastModifiedAtUtc = createdAtUtc;
            Status = GrantCaseStatus.Draft;
        }

        public static GrantCase Create(Guid id, string caseNumber, DateTimeOffset createdAtUtc)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id must not be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(caseNumber)) throw new ArgumentException("CaseNumber must be provided.", nameof(caseNumber));

            return new GrantCase(id, caseNumber, createdAtUtc);
        }

        public void UpdateDraft(string? hprNumber, decimal? employmentPercentage, DateOnly? employmentStartDate, DateOnly? employmentEndDate, DateTimeOffset now)
        {
            if (Status != GrantCaseStatus.Draft && Status != GrantCaseStatus.ReturnedForCorrection)
                throw new InvalidOperationException("Can only update draft cases.");

            if (employmentPercentage.HasValue)
            {
                if (employmentPercentage.Value <= 0 || employmentPercentage.Value > 100)
                    throw new ArgumentOutOfRangeException(nameof(employmentPercentage), "EmploymentPercentage must be > 0 and <= 100.");
            }

            if (employmentStartDate.HasValue && employmentEndDate.HasValue)
            {
                if (employmentEndDate.Value < employmentStartDate.Value)
                    throw new ArgumentException("EmploymentEndDate cannot be before EmploymentStartDate.", nameof(employmentEndDate));
            }

            HprNumber = hprNumber;
            EmploymentPercentage = employmentPercentage;
            EmploymentStartDate = employmentStartDate;
            EmploymentEndDate = employmentEndDate;
            LastModifiedAtUtc = now;
        }

        public void UpdateApplication(Alisflyt.Domain.Forms.GrantApplicationData data, DateTimeOffset now)
        {
            var existing = ApplicationDataJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson);
            if (existing is not null)
            {
                data.IsCentralityGrade6 = existing.IsCentralityGrade6;
                data.CentralitySupplementRequestedAmount = existing.CentralitySupplementRequestedAmount;
                data.SupervisionExpenses = existing.SupervisionExpenses;
                data.HasAdditionalSupervisionCosts = existing.HasAdditionalSupervisionCosts;
                data.AdditionalSupervisionCosts = existing.AdditionalSupervisionCosts;
            }
            data.SupervisorApprovedAtUtc = existing is not null
                && System.Text.Json.JsonSerializer.Serialize(existing.Certificate) == System.Text.Json.JsonSerializer.Serialize(data.Certificate)
                ? existing.SupervisorApprovedAtUtc : null;
            data.ValidateDraft();
            var first = data.EmploymentPeriods.FirstOrDefault();
            UpdateDraft(data.HprNumber, first?.PositionPercentage, first?.EmploymentStartDate, first?.FundingThrough, now);
            ApplicationDataJson = System.Text.Json.JsonSerializer.Serialize(data);
        }

        public void ApproveCertificate(DateTimeOffset now)
        {
            if (Status is not (GrantCaseStatus.Draft or GrantCaseStatus.Submitted or GrantCaseStatus.UnderReview or GrantCaseStatus.ReturnedForCorrection))
                throw new InvalidOperationException("Attesten kan ikke godkjennes i denne statusen.");
            var data = ApplicationDataJson is null ? throw new InvalidOperationException("Saken mangler søknadsdata.")
                : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson)!;
            var certificate = data.Certificate;
            if (string.IsNullOrWhiteSpace(certificate.SupervisorName) || string.IsNullOrWhiteSpace(certificate.DoctorName)
                || certificate.Sessions.Count == 0 || certificate.Sessions.Any(s => !s.Date.HasValue || !s.Hours.HasValue || string.IsNullOrWhiteSpace(s.Topic)))
                throw new ArgumentException("Fyll inn navn, dato, timer og tema for veiledningsøktene før godkjenning.");
            data.ValidateDraft();
            data.SupervisorApprovedAtUtc = now;
            ApplicationDataJson = System.Text.Json.JsonSerializer.Serialize(data);
            LastModifiedAtUtc = now;
        }

        public void UpdateCentrality(bool grade6, decimal? amount, DateTimeOffset now)
        {
            if (Status is not (GrantCaseStatus.Draft or GrantCaseStatus.Submitted or GrantCaseStatus.UnderReview or GrantCaseStatus.ReturnedForCorrection))
                throw new InvalidOperationException("Saken kan ikke redigeres i denne statusen.");
            if (amount < 0 || (grade6 && !amount.HasValue))
                throw new ArgumentException("Oppgi et gyldig beløp for sentralitetstillegg.");
            var data = ApplicationDataJson is null ? throw new InvalidOperationException("Saken mangler søknadsdata.")
                : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson)!;
            data.IsCentralityGrade6 = grade6;
            data.CentralitySupplementRequestedAmount = grade6 ? amount : null;
            ApplicationDataJson = System.Text.Json.JsonSerializer.Serialize(data);
            LastModifiedAtUtc = now;
        }

        public void Submit(DateTimeOffset now)
        {
            if (ApplicationDataJson is not null)
                System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson)!.ValidateSubmission();
            if (Status != GrantCaseStatus.Draft && Status != GrantCaseStatus.ReturnedForCorrection)
                throw new InvalidOperationException("Only draft or returned-for-correction cases can be submitted.");

            if (string.IsNullOrWhiteSpace(HprNumber))
                throw new ArgumentException("Oppgi HPR-nummer for legen.");

            if (!EmploymentPercentage.HasValue)
                throw new ArgumentException("Oppgi stillingsprosent.");

            if (EmploymentPercentage.Value <= 0 || EmploymentPercentage.Value > 100)
                throw new ArgumentOutOfRangeException(nameof(EmploymentPercentage), "EmploymentPercentage must be > 0 and <= 100.");

            if (!EmploymentStartDate.HasValue)
                throw new ArgumentException("Oppgi startdato for ansettelsesperioden.");
            if (!EmploymentEndDate.HasValue)
                throw new ArgumentException("Oppgi sluttdato for ansettelsesperioden.");

            if (EmploymentEndDate!.Value < EmploymentStartDate!.Value)
                throw new ArgumentException("Sluttdato for ansettelsesperioden kan ikke være før startdato.");

            Status = GrantCaseStatus.Submitted;
            LastModifiedAtUtc = now;
        }

        public void StartReview(DateTimeOffset now)
        {
            if (Status != GrantCaseStatus.Submitted)
                throw new InvalidOperationException("Can only start review for submitted cases.");

            Status = GrantCaseStatus.UnderReview;
            LastModifiedAtUtc = now;
        }

        public void ReturnForCorrection(string reason, DateTimeOffset now)
        {
            if (Status != GrantCaseStatus.UnderReview)
                throw new InvalidOperationException("Can only return cases that are under review.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Return reason must be provided.", nameof(reason));

            var trimmed = reason.Trim();
            if (trimmed.Length > 1000)
                throw new ArgumentException("Return reason cannot exceed 1000 characters.", nameof(reason));

            ReturnReason = trimmed;
            ReturnedAtUtc = now;
            Status = GrantCaseStatus.ReturnedForCorrection;
            LastModifiedAtUtc = now;
        }
    }
}
