namespace Alisflyt.Domain.Enums
{
    // We keep guidance approval separate from the case status so a case can still be in review while the attestation itself is pending, approved, or needs correction.
    public enum GuidanceApprovalStatus
    {
        Pending = 0,
        Approved = 1,
        NeedsCorrection = 2,
        Superseded = 3
    }
}
