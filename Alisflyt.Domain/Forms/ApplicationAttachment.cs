namespace Alisflyt.Domain.Forms;
public class ApplicationAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "Receipt";
    public string FileName { get; set; } = "";
    public string ContentBase64 { get; set; } = "";
    public static void ValidateAll(List<ApplicationAttachment> attachments)
    {
        if (attachments.Count > 30 || attachments.Select(a => a?.Id).Distinct().Count() != attachments.Count)
            throw new ArgumentException("Maksimalt 30 vedlegg med unike referanser.");
        long total = 0;
        foreach (var a in attachments)
        {
            if (a is null || a.Id == Guid.Empty || a.Kind is not ("Agreement" or "CalculationBasis" or "Receipt")
                || string.IsNullOrWhiteSpace(a.FileName) || a.FileName.Length > 200
                || !new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(a.FileName).ToLowerInvariant()))
                throw new ArgumentException("Vedlegg må være PDF, PNG eller JPEG.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(a.ContentBase64); }
            catch (FormatException) { throw new ArgumentException("Ugyldig vedlegg."); }
            if (bytes.Length == 0 || bytes.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Vedlegg må være mellom 1 byte og 5 MB.");
            total += bytes.Length;
        }
        if (total > 15 * 1024 * 1024) throw new ArgumentException("Vedlegg kan være maksimalt 15 MB totalt.");
    }
}
