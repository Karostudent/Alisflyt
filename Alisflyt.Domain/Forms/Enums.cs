using System.ComponentModel.DataAnnotations;

namespace Alisflyt.Domain.Forms;

public enum GrantType
{
    [Display(Name = "Tilskudd til ALIS-avtale (inkl. veiledning)")]
    AlisAgreementIncludingSupervision = 1,
    [Display(Name = "Tilskudd til veiledning av ALIS")]
    SupervisionOnly = 2
}

public enum PositionType
{
    [Display(Name = "Fastlege/fastlegevikar")]
    RegularGpOrLocum = 1,
    [Display(Name = "Introduksjonslege")]
    IntroductoryDoctor = 2,
    [Display(Name = "Allmennlege utenfor FLO")]
    GeneralPractitionerOutsideRegularGpScheme = 3
}

