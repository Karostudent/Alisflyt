namespace Alisflyt.Domain.Forms;

public class SupervisionCertificateViewModel
{
    public string SupervisorName { get; set; } = "";
    public string DoctorName { get; set; } = "";
    public List<SupervisionSessionInputModel> Sessions { get; set; } = [];
    public string DoctorSigningPlace { get; set; } = "";
    public DateOnly? DoctorSigningDate { get; set; }
    public string SupervisorSigningPlace { get; set; } = "";
    public DateOnly? SupervisorSigningDate { get; set; }
}

public class SupervisionSessionInputModel
{
    public DateOnly? Date { get; set; }
    public decimal? Hours { get; set; }
    public string Topic { get; set; } = "";
    // Typed names are not authenticated electronic signatures.
    public string DoctorSignatureName { get; set; } = "";
    public string SupervisorSignatureName { get; set; } = "";
}
