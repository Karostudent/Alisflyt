using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Forms;

namespace Alisflyt.Web.Exports;

public static class GrantCaseExcelExport
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static byte[] Create(GrantCaseDto application, GrantCalculationDetailsDto calculation, DateTimeOffset exportedAt)
    {
        var data = application.ApplicationData;
        var rows = new List<XElement>();
        var merges = new List<XElement>();
        void Row(params object?[] values)
        {
            var rowNumber = rows.Count + 1;
            rows.Add(new XElement(Ns + "row", new XAttribute("r", rowNumber), values.Select((value, column) =>
            {
                var cell = new XElement(Ns + "c", new XAttribute("r", $"{(char)('A' + column)}{rowNumber}"));
                switch (value)
                {
                    case decimal number:
                        cell.Add(new XAttribute("s", "1"), new XElement(Ns + "v", number.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case DateOnly date:
                        cell.Add(new XAttribute("s", "2"), new XElement(Ns + "v", date.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture)));
                        break;
                    default:
                        cell.Add(new XAttribute("s", "3"), new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", value?.ToString() ?? "")));
                        break;
                }
                return cell;
            })));
            // Reserve merged cells for long explanations, keeping dates and
            // ordinary field values immediately beside their labels.
            var wideNote = values.Length == 2 && values[1] is string text && text.Length > 52;
            if (wideNote)
            {
                merges.Add(new XElement(Ns + "mergeCell", new XAttribute("ref", $"B{rowNumber}:E{rowNumber}")));
                for (var column = 'C'; column <= 'E'; column++)
                    rows[^1].Add(new XElement(Ns + "c", new XAttribute("r", $"{column}{rowNumber}"), new XAttribute("s", "3")));
            }
            if (values.Length == 2 && values[1] is DateOnly)
                rows[^1].Elements(Ns + "c").ElementAt(1).SetAttributeValue("s", "7");
            else if (values.Length == 2 && values[1] is decimal)
                rows[^1].Elements(Ns + "c").ElementAt(1).SetAttributeValue("s", "8");
            if (values.Length > 0)
            {
                // Explicit height is needed because Excel does not autofit merged cells.
                var lines = values.Select((v, column) => (int)Math.Ceiling((v?.ToString()?.Length ?? 0) / (wideNote && column == 1 ? 130d : column == 0 ? 40d : column == 1 ? 38d : 29d))).DefaultIfEmpty(1).Max();
                rows[^1].SetAttributeValue("ht", Math.Max(16, lines * 12 + 3));
                rows[^1].SetAttributeValue("customHeight", 1);
            }
            else
            {
                rows[^1].SetAttributeValue("ht", 7);
                rows[^1].SetAttributeValue("customHeight", 1);
            }
        }

        void Section(string title)
        {
            if (rows.Count > 0 && rows[^1].Elements(Ns + "c").Any()) Row();
            Row(title, "", "", "", "");
            foreach (var cell in rows[^1].Elements(Ns + "c")) cell.SetAttributeValue("s", "4");
            merges.Add(new XElement(Ns + "mergeCell", new XAttribute("ref", $"A{rows.Count}:E{rows.Count}")));
            rows[^1].SetAttributeValue("ht", 20);
        }

        void Header(params object?[] values)
        {
            Row(values);
            foreach (var cell in rows[^1].Elements(Ns + "c")) cell.SetAttributeValue("s", "5");
        }

        Section("1. Søknadsinformasjon – revisjonsgrunnlag");
        Row("ALIS-lege", data?.DoctorName);
        Row("Saksnummer", application.CaseNumber);
        Row("Søknads-ID", application.Id);
        Row("HPR-nummer", data?.HprNumber ?? application.HprNumber);
        Row("Status", "Under behandling");
        Row("Tilskuddstype", data?.GrantType == GrantType.AlisAgreementIncludingSupervision ? "ALIS-avtale inkludert veiledning" : data?.GrantType.HasValue == true ? "Veiledning av ALIS" : "Ikke oppgitt");
        Row("Opprettet (UTC)", application.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        Row("Sist endret (UTC)", application.LastModifiedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        Row("Spesialisering fra", data?.SpecializationStartDate);
        Row("Forventet avslutning", data?.ExpectedCompletionDate);
        Row("ALIS-avtale fra", data?.AgreementEffectiveFrom);
        Row("Første start som fastlege/fastlegevikar", data?.FirstRegularGpOrLocumDate);
        Row("Sentralitetsgrad 6", data?.IsCentralityGrade6 is bool grade6 ? grade6 ? "Ja" : "Nei" : "Ikke oppgitt");
        Row();
        Section("2. Stillingsperioder");
        Header("Stillingstype", "Stillingsprosent", "Tiltredelse", "Tilskudd fra", "Tilskudd til og med");
        foreach (var period in data?.EmploymentPeriods ?? [])
            Row(period.PositionType switch { PositionType.RegularGpOrLocum => "Fastlege/fastlegevikar", PositionType.IntroductoryDoctor => "Introduksjonslege", null => "Ikke oppgitt", _ => "Allmennlege utenfor FLO" }, period.PositionPercentage, period.EmploymentStartDate, period.FundingFrom, period.FundingThrough);
        Row();
        Section("3. Beregnede tilskudd og summer");
        Header("Beregningsposter", "Søkt beløp (kr)", "Beregnet beløp (kr)");
        void Amount(string label, decimal? requested, decimal calculated) => Row(label, requested, calculation.IsAvailable ? calculated : null);
        Amount("Praksiskompensasjon", data?.AbsenceCompensation, calculation.PracticeCompensationAmount);
        Amount("Læringsaktiviteter", data?.LearningActivityExpenses, calculation.LearningActivitiesAmount);
        Amount("Produktivitetselement", data?.ProductivityRequestedAmount, calculation.ProductivityAmount);
        Amount("Tilskudd til veiledning", data?.SupervisionExpenses, calculation.GuidanceAmount);
        Amount("Tilrettelegging", null, calculation.FacilitationAmount);
        Amount("Sentralitetstillegg", data?.CentralitySupplementRequestedAmount, calculation.CentralitySupplementAmount);
        var requestedAmounts = new[] { data?.AbsenceCompensation, data?.LearningActivityExpenses, data?.ProductivityRequestedAmount, data?.SupervisionExpenses, data?.AdditionalSupervisionCosts, data?.CentralitySupplementRequestedAmount };
        Amount("Totalt", requestedAmounts.Any(x => x.HasValue) ? requestedAmounts.Sum(x => x ?? 0) : null, calculation.TotalAmount);
        foreach (var cell in rows[^1].Elements(Ns + "c")) cell.SetAttributeValue("s", cell.Element(Ns + "v") is null ? "5" : "6");
        Row();
        Row("Oppgitte ekstra veiledningskostnader (kr)", data?.AdditionalSupervisionCosts);
        Row("Søkte beløp", "Summen omfatter oppgitte beløp, inkludert ekstra veiledningskostnader. Produktivitet og tilrettelegging beregnes etter egne regler i detaljgrunnlaget.");
        Section("4. Beregningsmerknader og satssett");
        Row("Beregningsstatus", calculation.IsAvailable ? "Tilgjengelig" : calculation.UnavailableReason ?? "Beregning ikke tilgjengelig");
        if (calculation.IsAvailable)
        {
            Row("Maksgrense praksiskompensasjon (kr)", calculation.PracticeCompensationMaximumAmount);
            Row("Praksiskompensasjon begrenset av maksgrense", calculation.IsPracticeCompensationCapped ? "Ja" : "Nei");
            Row("Sentralitet – forklaring", calculation.CentralityExplanation);
            Row("Satssett", calculation.RateSetName);
            Row("Satssett-ID", calculation.RateSetId);
            Row("Satser gyldige fra", calculation.RateSetValidFrom);
            Row("Satser gyldige til", calculation.RateSetValidTo);
        }
        if (calculation.IsAvailable)
        {
            Row();
            Section("5. Detaljert beregningsgrunnlag for revisor");
            var rates = calculation.AuditRows.Where(step => step.Rule == "Sats fra valgt satssett").ToList();
            if (rates.Count > 0)
            {
                Header("Sats", "Verdi", "Enhet");
                foreach (var rate in rates)
                {
                    Row(rate.Item, rate.Amount, rate.Substitution);
                    rows[^1].Elements(Ns + "c").ElementAt(1).SetAttributeValue("s", "8");
                }
                Row();
            }
            Header("Post / periode / sats", "Beregningsregel", "Tall i beregningen", "", "Resultat / satsverdi");
            merges.Add(new XElement(Ns + "mergeCell", new XAttribute("ref", $"C{rows.Count}:D{rows.Count}")));
            foreach (var step in calculation.AuditRows.Where(step => step.Rule != "Sats fra valgt satssett"))
            {
                Row(step.Item, step.Rule, step.Substitution, "", step.Amount);
                merges.Add(new XElement(Ns + "mergeCell", new XAttribute("ref", $"C{rows.Count}:D{rows.Count}")));
                var lines = Math.Max(Math.Ceiling(step.Item.Length / 40d), Math.Max(Math.Ceiling(step.Rule.Length / 38d), Math.Ceiling(step.Substitution.Length / 58d)));
                rows[^1].SetAttributeValue("ht", Math.Max(16, lines * 12 + 3));
            }
            if (calculation.AuditRows.Count == 0)
                Row("Detaljgrunnlag er ikke tilgjengelig for denne beregningen.");
            Row("Presisjon", "Beregningen bruker desimaltall uten særskilt avrunding av delbeløp. Excel viser beløp med to desimaler; underliggende verdier beholdes.");
            Row("Valg av satssett", "Aktivt satssett som dekker tidligste tilskudd-fra-dato; ved flere treff velges nyeste gyldig-fra-dato. Samme satssett brukes på alle perioder.");
        }
        Section("6. Om eksporten");
        Row("Beregningsgrunnlag", "Beregnet med gjeldende satssett ved eksport, på samme måte som i koordinatorvisningen.");
        Row("Eksportert (UTC)", exportedAt.ToString("yyyy-MM-dd HH:mm 'UTC'"));

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Add(string path, string xml)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open());
                writer.Write(xml);
            }
            Add("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            Add("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Add("xl/workbook.xml", new XElement(Ns + "workbook", new XAttribute(XNamespace.Xmlns + "r", rel), new XElement(Ns + "sheets", new XElement(Ns + "sheet", new XAttribute("name", SheetName(data?.DoctorName)), new XAttribute("sheetId", 1), new XAttribute(rel + "id", "rId1")))).ToString());
            Add("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            Add("xl/styles.xml", Styles().ToString());
            Add("xl/worksheets/sheet1.xml", new XElement(Ns + "worksheet", new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", 0), new XAttribute("showGridLines", 0))), new XElement(Ns + "cols", new XElement(Ns + "col", new XAttribute("min", 1), new XAttribute("max", 1), new XAttribute("width", 36), new XAttribute("customWidth", 1)), new XElement(Ns + "col", new XAttribute("min", 2), new XAttribute("max", 2), new XAttribute("width", 34), new XAttribute("customWidth", 1)), new XElement(Ns + "col", new XAttribute("min", 3), new XAttribute("max", 4), new XAttribute("width", 26), new XAttribute("customWidth", 1)), new XElement(Ns + "col", new XAttribute("min", 5), new XAttribute("max", 5), new XAttribute("width", 22), new XAttribute("customWidth", 1))), new XElement(Ns + "sheetData", rows), new XElement(Ns + "mergeCells", new XAttribute("count", merges.Count), merges), new XElement(Ns + "pageSetup", new XAttribute("orientation", "landscape"), new XAttribute("paperSize", 9))).ToString());
        }
        return stream.ToArray();
    }

    private static XElement Styles()
    {
        XElement Font(bool bold, string color) => new(Ns + "font",
            new XElement(Ns + "sz", new XAttribute("val", 10)),
            new XElement(Ns + "name", new XAttribute("val", "Calibri")),
            new XElement(Ns + "color", new XAttribute("rgb", color)),
            bold ? new XElement(Ns + "b") : null);
        XElement Fill(string color) => new(Ns + "fill", new XElement(Ns + "patternFill",
            new XAttribute("patternType", "solid"), new XElement(Ns + "fgColor", new XAttribute("rgb", color)),
            new XElement(Ns + "bgColor", new XAttribute("indexed", 64))));
        XElement Format(int number, int font = 0, int fill = 0, bool left = false) => new(Ns + "xf",
            new XAttribute("numFmtId", number), new XAttribute("fontId", font), new XAttribute("fillId", fill),
            new XAttribute("borderId", 1), new XAttribute("xfId", 0),
            new XAttribute("applyNumberFormat", 1), new XAttribute("applyFont", 1),
            new XAttribute("applyFill", 1), new XAttribute("applyBorder", 1), new XAttribute("applyAlignment", 1),
            new XElement(Ns + "alignment", new XAttribute("wrapText", 1), new XAttribute("vertical", "top"), left ? new XAttribute("horizontal", "left") : null));
        return new XElement(Ns + "styleSheet",
            new XElement(Ns + "fonts", new XAttribute("count", 3), Font(false, "FF374151"), Font(true, "FF374151"), Font(true, "FF374151")),
            new XElement(Ns + "fills", new XAttribute("count", 4),
                new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "none"))),
                new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "gray125"))),
                Fill("FFE8EDEF"), Fill("FFF3F5F6")),
            new XElement(Ns + "borders", new XAttribute("count", 2), new XElement(Ns + "border"),
                new XElement(Ns + "border", new[] { "left", "right", "top", "bottom" }.Select(side =>
                    new XElement(Ns + side, new XAttribute("style", "thin"), new XElement(Ns + "color", new XAttribute("rgb", "FFDCE1E4")))))),
            new XElement(Ns + "cellStyleXfs", new XAttribute("count", 1), new XElement(Ns + "xf")),
            new XElement(Ns + "cellXfs", new XAttribute("count", 9),
                Format(0), Format(4), Format(14), Format(0), Format(0, 1, 2), Format(0, 2, 3), Format(4, 2, 3), Format(14, left: true), Format(4, left: true)));
    }

    public static string SheetName(string? name)
    {
        var cleaned = new string((name ?? "").Where(c => !"[]:*?/\\".Contains(c) && !char.IsControl(c)).ToArray()).Trim().Trim('\'');
        if (cleaned.Length > 31) cleaned = cleaned[..31].TrimEnd('\'');
        return string.IsNullOrWhiteSpace(cleaned) ? "ALIS-lege" : cleaned;
    }
}


