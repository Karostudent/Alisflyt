using Alisflyt.Domain.Enums;

namespace Alisflyt.Domain.Entities
{
    public class GuidanceApproval
    {
        public Guid Id { get; private set; }
        public Guid GrantCaseId { get; private set; }
        public string DoctorHprNumber { get; private set; } = string.Empty;
        public string SupervisorHprNumber { get; private set; } = string.Empty;
        public string SupervisorUserKey { get; private set; } = string.Empty;
        public string? SupervisorName { get; private set; }
        public GuidanceApprovalStatus Status { get; private set; } = GuidanceApprovalStatus.Pending;
        public DateTimeOffset CreatedAtUtc { get; private set; }
        public DateTimeOffset UpdatedAtUtc { get; private set; }
        public DateTimeOffset? ApprovedAtUtc { get; private set; }
        public string? ApprovedBy { get; private set; }
        public string? RequestedRevisionReason { get; private set; }
        public int VersionNumber { get; private set; }
        public string AttestationSnapshotHash { get; private set; } = string.Empty;
        public string? AttestationSnapshotJson { get; private set; }
        public GrantCase? GrantCase { get; private set; }

        private GuidanceApproval(
            Guid id,
            Guid grantCaseId,
            string doctorHprNumber,
            string supervisorHprNumber,
            string supervisorUserKey,
            int versionNumber,
            string attestationSnapshotHash,
            DateTimeOffset createdAtUtc,
            string? supervisorName = null,
            string? attestationSnapshotJson = null)
        {
            Id = id;
            GrantCaseId = grantCaseId;
            DoctorHprNumber = doctorHprNumber.Trim();
            SupervisorHprNumber = supervisorHprNumber.Trim();
            SupervisorUserKey = supervisorUserKey.Trim();
            SupervisorName = supervisorName?.Trim();
            VersionNumber = versionNumber;
            AttestationSnapshotHash = attestationSnapshotHash.Trim();
            AttestationSnapshotJson = attestationSnapshotJson;
            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = createdAtUtc;
        }

        public static GuidanceApproval Create(
            Guid id,
            Guid grantCaseId,
            string doctorHprNumber,
            string supervisorHprNumber,
            string supervisorUserKey,
            int versionNumber,
            string attestationSnapshotHash,
            DateTimeOffset createdAtUtc,
            string? supervisorName = null,
            string? attestationSnapshotJson = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("Id must not be empty.", nameof(id));
            if (grantCaseId == Guid.Empty) throw new ArgumentException("GrantCaseId must not be empty.", nameof(grantCaseId));
            if (string.IsNullOrWhiteSpace(doctorHprNumber)) throw new ArgumentException("Doctor HPR number is required.", nameof(doctorHprNumber));
            if (string.IsNullOrWhiteSpace(supervisorHprNumber)) throw new ArgumentException("Supervisor HPR number is required.", nameof(supervisorHprNumber));
            if (string.IsNullOrWhiteSpace(supervisorUserKey)) throw new ArgumentException("Supervisor user identity is required.", nameof(supervisorUserKey));
            if (versionNumber <= 0) throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be greater than zero.");
            if (string.IsNullOrWhiteSpace(attestationSnapshotHash)) throw new ArgumentException("Snapshot hash is required.", nameof(attestationSnapshotHash));

            return new GuidanceApproval(
                id,
                grantCaseId,
                doctorHprNumber,
                supervisorHprNumber,
                supervisorUserKey,
                versionNumber,
                attestationSnapshotHash,
                createdAtUtc,
                supervisorName,
                attestationSnapshotJson);
        }

        public void Approve(DateTimeOffset approvedAtUtc, string approvedByUserKey)
        {
            if (string.IsNullOrWhiteSpace(approvedByUserKey))
                throw new ArgumentException("Approved by user identity is required.", nameof(approvedByUserKey));

            if (Status == GuidanceApprovalStatus.Superseded)
                throw new InvalidOperationException("A superseded approval cannot be approved.");

            Status = GuidanceApprovalStatus.Approved;
            ApprovedAtUtc = approvedAtUtc;
            ApprovedBy = approvedByUserKey.Trim();
            RequestedRevisionReason = null;
            UpdatedAtUtc = approvedAtUtc;
        }

        public void RequestCorrection(string reason, DateTimeOffset now)
        {
            var trimmed = reason?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                throw new ArgumentException("Correction reason must be provided.", nameof(reason));

            if (trimmed.Length > 1000)
                throw new ArgumentException("Correction reason cannot exceed 1000 characters.", nameof(reason));

            RequestedRevisionReason = trimmed;
            Status = GuidanceApprovalStatus.NeedsCorrection;
            UpdatedAtUtc = now;
        }

        public void MarkSuperseded(DateTimeOffset now)
        {
            if (Status == GuidanceApprovalStatus.Superseded)
                return;

            Status = GuidanceApprovalStatus.Superseded;
            UpdatedAtUtc = now;
        }

        public bool MatchesVersion(int versionNumber, string attestationSnapshotHash)
        {
            return VersionNumber == versionNumber
                && string.Equals(AttestationSnapshotHash, attestationSnapshotHash, StringComparison.Ordinal);
        }
    }
}
