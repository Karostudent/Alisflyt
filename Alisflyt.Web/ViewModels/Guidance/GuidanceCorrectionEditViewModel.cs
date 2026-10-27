using System;
using System.Collections.Generic;
using Alisflyt.Domain.Forms;

namespace Alisflyt.Web.ViewModels.Guidance;

public sealed class GuidanceCorrectionEditViewModel
{
    public Guid CaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string? HprNumber { get; set; }
    public string? DoctorName { get; set; }
    public string? CorrectionReason { get; set; }
    public List<SupervisionSessionInputModel> Sessions { get; set; } = [];
}
