using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Alisflyt.Web.ViewModels.GrantCases
{
    public sealed class CreateGrantCaseViewModel : IValidatableObject
    {
        [Display(Name = "HPR-nummer")]
        public string? HprNumber { get; set; }

        [Display(Name = "Arbeidsprosent")]
        public decimal? EmploymentPercentage { get; set; }

        [Display(Name = "Ansettelsesstart")]
        public DateOnly? EmploymentStartDate { get; set; }

        [Display(Name = "Ansettelsesslutt")]
        public DateOnly? EmploymentEndDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // EmploymentPercentage: if provided must be > 0 and <= 100
            if (EmploymentPercentage.HasValue)
            {
                if (EmploymentPercentage.Value <= 0m || EmploymentPercentage.Value > 100m)
                {
                    yield return new ValidationResult("Stillingsprosent må være større enn 0 og høyst 100.", new[] { nameof(EmploymentPercentage) });
                }
            }

            // Date range validation
            if (EmploymentStartDate.HasValue && EmploymentEndDate.HasValue)
            {
                if (EmploymentEndDate.Value < EmploymentStartDate.Value)
                {
                    yield return new ValidationResult("Sluttdato kan ikke være før startdato.", new[] { nameof(EmploymentEndDate), nameof(EmploymentStartDate) });
                }
            }
        }
    }
}
