using System;
using System.Linq;
using System.Text.Json;
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
        public DateTimeOffset? ReadyForAuditorApprovalAtUtc { get; private set; }
        public ICollection<GuidanceApproval> GuidanceApprovals { get; private set; } = [];

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
            ArgumentNullException.ThrowIfNull(data);
            data.Attachments ??= [];
            data.DoctorProfessions ??= [];
            data.EmploymentPeriods ??= [];
            data.SelectedPositionTypes ??= [];
            data.Certificate ??= new();
            data.Certificate.Sessions ??= [];

            var existing = ApplicationDataJson is null ? null : JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson);
            if (existing is not null)
            {
                var previousSnapshot = GuidanceApproval.CreateAttestationSnapshotHash(existing);
                var nextSnapshot = GuidanceApproval.CreateAttestationSnapshotHash(data);
                if (!string.Equals(previousSnapshot, nextSnapshot, StringComparison.Ordinal))
                {
                    foreach (var approval in GuidanceApprovals.Where(a => a.Status != GuidanceApprovalStatus.Superseded).ToList())
                    {
                        approval.MarkSuperseded(now);
                    }
                }

                // Centrality is maintained separately by the coordinator, not by the ALIS form.
                data.IsCentralityGrade6 = existing.IsCentralityGrade6;
                data.CentralitySupplementRequestedAmount = existing.CentralitySupplementRequestedAmount;
            }
            data.SupervisorApprovedAtUtc = existing is not null
                && JsonSerializer.Serialize(existing.Certificate) == JsonSerializer.Serialize(data.Certificate)
                ? existing.SupervisorApprovedAtUtc : null;
            data.ValidateDraft();
            var first = data.EmploymentPeriods.FirstOrDefault();
            UpdateDraft(data.HprNumber, first?.PositionPercentage, first?.EmploymentStartDate, first?.FundingThrough, now);
            ApplicationDataJson = JsonSerializer.Serialize(data);
        }

        public void ApproveCertificate(DateTimeOffset now)
        {
            if (Status is not (GrantCaseStatus.Draft or GrantCaseStatus.Submitted or GrantCaseStatus.UnderReview or GrantCaseStatus.ReturnedForCorrection))
                throw new InvalidOperationException("Attesten kan ikke godkjennes i denne statusen.");
            var data = ApplicationDataJson is null ? throw new InvalidOperationException("Saken mangler søknadsdata.")
                : System.Text.Json.JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson)!;
            var certificate = data.Certificate;
            if (string.IsNullOrWhiteSpace(certificate.SupervisorHprNumber) || string.IsNullOrWhiteSpace(certificate.DoctorName)
                || certificate.Sessions.Count == 0 || certificate.Sessions.Any(s => !s.Date.HasValue || !s.Hours.HasValue || string.IsNullOrWhiteSpace(s.Topic)))
                throw new ArgumentException("Fyll inn veileders HPR-nummer, ALIS-legens navn, dato, timer og tema for veiledningsøktene før godkjenning.");
            data.ValidateDraft();
            data.SupervisorApprovedAtUtc = now;
            ApplicationDataJson = System.Text.Json.JsonSerializer.Serialize(data);
            LastModifiedAtUtc = now;
        }

        public GuidanceApproval RequestGuidanceApproval(
            string doctorHprNumber,
            string supervisorHprNumber,
            string supervisorUserKey,
            DateTimeOffset now,
            string? supervisorName = null)
        {
            if (string.IsNullOrWhiteSpace(doctorHprNumber))
                throw new ArgumentException("Doctor HPR must be provided.", nameof(doctorHprNumber));
            if (string.IsNullOrWhiteSpace(supervisorHprNumber))
                throw new ArgumentException("Supervisor HPR must be provided.", nameof(supervisorHprNumber));
            if (string.IsNullOrWhiteSpace(supervisorUserKey))
                throw new ArgumentException("Supervisor user key must be provided.", nameof(supervisorUserKey));

            var data = ApplicationDataJson is null
                ? throw new InvalidOperationException("Saken mangler søknadsdata.")
                : JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson)!;
            if (data.Certificate is null || data.Certificate.Sessions is null || data.Certificate.Sessions.Count == 0)
                throw new InvalidOperationException("Det finnes ingen veiledningstimer som kan sendes til godkjenning.");

            var snapshotHash = GuidanceApproval.CreateAttestationSnapshotHash(data);
            var existingMatchingApproval = GuidanceApprovals
                .Where(a => a.Status != GuidanceApprovalStatus.Superseded)
                .OrderByDescending(a => a.VersionNumber)
                .FirstOrDefault(a => string.Equals(a.AttestationSnapshotHash, snapshotHash, StringComparison.Ordinal));
            if (existingMatchingApproval is not null)
            {
                return existingMatchingApproval;
            }

            var nextVersion = GuidanceApprovals.Count == 0 ? 1 : GuidanceApprovals.Max(a => a.VersionNumber) + 1;
            var approval = GuidanceApproval.Create(
                Guid.NewGuid(),
                Id,
                doctorHprNumber,
                supervisorHprNumber,
                supervisorUserKey,
                nextVersion,
                snapshotHash,
                now,
                supervisorName,
                JsonSerializer.Serialize(data.Certificate));

            foreach (var currentApproval in GuidanceApprovals.Where(a => a.Status != GuidanceApprovalStatus.Superseded).ToList())
            {
                if (!string.Equals(currentApproval.AttestationSnapshotHash, snapshotHash, StringComparison.Ordinal))
                    currentApproval.MarkSuperseded(now);
            }

            GuidanceApprovals.Add(approval);
            data.SupervisorApprovedAtUtc = null;
            ApplicationDataJson = JsonSerializer.Serialize(data);
            LastModifiedAtUtc = now;
            return approval;
        }

        public GuidanceApproval? GetCurrentGuidanceApproval()
        {
            return GuidanceApprovals
                .Where(a => a.Status != GuidanceApprovalStatus.Superseded)
                .OrderByDescending(a => a.VersionNumber)
                .ThenByDescending(a => a.CreatedAtUtc)
                .FirstOrDefault();
        }

        public void SynchronizeLegacySupervisorApproval(DateTimeOffset? approvedAtUtc, DateTimeOffset now)
        {
            var data = ApplicationDataJson is null
                ? throw new InvalidOperationException("Saken mangler søknadsdata.")
                : JsonSerializer.Deserialize<Alisflyt.Domain.Forms.GrantApplicationData>(ApplicationDataJson)
                    ?? throw new InvalidOperationException("Saken mangler søknadsdata.");

            data.SupervisorApprovedAtUtc = approvedAtUtc;
            ApplicationDataJson = JsonSerializer.Serialize(data);
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

        public void MarkReadyForAuditorApproval(DateTimeOffset now)
        {
            if (Status != GrantCaseStatus.UnderReview)
                throw new InvalidOperationException("Only cases under review can be marked ready for auditor approval.");

            Status = GrantCaseStatus.AwaitingAuditorApproval;
            ReadyForAuditorApprovalAtUtc = now;
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
