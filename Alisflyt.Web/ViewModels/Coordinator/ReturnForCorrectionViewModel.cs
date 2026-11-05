using System;
using System.ComponentModel.DataAnnotations;

namespace Alisflyt.Web.ViewModels.Coordinator
{
    public sealed class ReturnForCorrectionViewModel
    {
        public Guid Id { get; init; }

        [Display(Name = "Saksnummer")]
        public string CaseNumber { get; init; } = string.Empty;

        [Required(ErrorMessage = "Skriv en begrunnelse før saken returneres.")]
        [StringLength(1000, ErrorMessage = "Begrunnelsen kan være på maksimalt 1000 tegn.")]
        [Display(Name = "Begrunnelse")]
        public string Reason { get; set; } = string.Empty;
    }
}
