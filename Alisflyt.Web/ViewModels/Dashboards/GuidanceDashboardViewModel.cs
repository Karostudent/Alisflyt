using System;
using System.Collections.Generic;

namespace Alisflyt.Web.ViewModels.Dashboards
{
    public sealed class GuidanceDashboardViewModel
    {
        public string Message { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
        public IReadOnlyList<string> AssignedDoctors { get; set; } = [];
        public int PendingCount { get; set; }
        public int NeedsCorrectionCount { get; set; }
        public int ApprovedCount { get; set; }
        public IReadOnlyList<GuidanceDashboardCaseViewModel> Cases { get; set; } = [];
    }

    public sealed class GuidanceDashboardCaseViewModel
    {
        public Guid Id { get; set; }
        public string CaseNumber { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string HprNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public string PeriodText { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public DateTimeOffset LastModifiedAtUtc { get; set; }
    }
}
