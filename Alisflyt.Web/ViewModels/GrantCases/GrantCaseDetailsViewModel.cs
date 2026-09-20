using System;
using System.ComponentModel.DataAnnotations;
using Alisflyt.Domain.Enums;

namespace Alisflyt.Web.ViewModels.GrantCases
{
    public sealed class GrantCaseDetailsViewModel
    {
        public Guid Id { get; init; }

        [Display(Name = "Saksnummer")]
        public string CaseNumber { get; init; } = string.Empty;

        [Display(Name = "HPR-nummer")]
        public string? HprNumber { get; init; }

        [Display(Name = "Arbeidsprosent")]
        public decimal? EmploymentPercentage { get; init; }

        [Display(Name = "Ansettelsesstart")]
        public DateOnly? EmploymentStartDate { get; init; }

        [Display(Name = "Ansettelsesslutt")]
        public DateOnly? EmploymentEndDate { get; init; }

        [Display(Name = "Status")]
        public GrantCaseStatus Status { get; init; }

        [Display(Name = "Opprettet")]
        public DateTimeOffset CreatedAtUtc { get; init; }

        [Display(Name = "Sist endret")]
        public DateTimeOffset LastModifiedAtUtc { get; init; }

        [Display(Name = "Returnert begrunnelse")]
        public string? ReturnReason { get; init; }

        [Display(Name = "Returnert dato")]
        public DateTimeOffset? ReturnedAtUtc { get; init; }
    }
}
