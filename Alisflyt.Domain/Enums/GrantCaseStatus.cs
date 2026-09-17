namespace Alisflyt.Domain.Enums
{
    public enum GrantCaseStatus
    {
        Draft,
        AwaitingGuidanceConfirmation,
        ReadyForSubmission,
        Submitted,
        UnderReview,
        ReturnedForCorrection,
        ReadyForCalculation,
        AwaitingAuditorApproval,
        AuditorApprovalDocumented,
        ReadyForDirectorateSubmission,
        SubmittedToDirectorate,
        DecisionReceived,
        Completed,
        Closed
    }
}
