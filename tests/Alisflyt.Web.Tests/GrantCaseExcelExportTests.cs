using System.IO.Compression;
using System.Xml.Linq;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.Exports;
using Xunit;

namespace Alisflyt.Web.Tests;

public class GrantCaseExcelExportTests
{
    [Theory]
    [InlineData(GrantCaseStatus.UnderReview, "Under behandling")]
    [InlineData(GrantCaseStatus.AwaitingAuditorApproval, "Avventer revisorkontroll")]
    public void Export_includes_actual_case_status(GrantCaseStatus status, string expectedStatus)
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto { Status = status }, new GrantCalculationDetailsDto(), DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        Assert.Contains(expectedStatus, xml.ToString());
    }

    [Fact]
    public void Export_includes_auditor_rules_substitutions_and_numeric_results()
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto(), new GrantCalculationDetailsDto
        {
            IsAvailable = true,
            AuditRows = [new("Praksismaks", "Årsgrunnlag × stillingsandel × måneder / 12", "132000 × 50 / 100 × 6 / 12", 33000m)]
        }, DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var row = Assert.Single(xml.Descendants(ns + "row").Where(r => r.Elements(ns + "c").FirstOrDefault()?.Value == "Praksismaks"));
        Assert.Contains("Årsgrunnlag", row.Elements(ns + "c").ElementAt(1).Value);
        Assert.Equal("132000 × 50 / 100 × 6 / 12", row.Elements(ns + "c").ElementAt(2).Value);
        Assert.Equal("33000", row.Elements(ns + "c").ElementAt(4).Element(ns + "v")?.Value);
    }

    [Theory]
    [InlineData("Ola Nordmann", "Ola Nordmann")]
    [InlineData("'Ola:/Nordmann'", "OlaNordmann")]
    [InlineData("[]:*?/\\", "ALIS-lege")]
    [InlineData("12345678901234567890123456789012345", "1234567890123456789012345678901")]
    public void Sheet_names_follow_excel_restrictions(string input, string expected)
        => Assert.Equal(expected, GrantCaseExcelExport.SheetName(input));

    [Fact]
    public void Export_contains_one_named_sheet_with_numeric_amounts_and_literal_user_text()
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto
        {
            ApplicationData = new GrantApplicationData { DoctorName = "=1+1", AbsenceCompensation = 1500.25m }
        }, new GrantCalculationDetailsDto { IsAvailable = true, PracticeCompensationAmount = 1200.5m }, DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        XDocument Read(string path)
        {
            using var stream = zip.GetEntry(path)!.Open();
            return XDocument.Load(stream);
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = Assert.Single(Read("xl/workbook.xml").Descendants(ns + "sheet"));
        Assert.Equal("=1+1", (string?)sheet.Attribute("name"));
        var cells = Read("xl/worksheets/sheet1.xml");
        Assert.Empty(cells.Descendants(ns + "f"));
        Assert.Contains(cells.Descendants(ns + "c"), c => (string?)c.Attribute("t") == "inlineStr" && c.Value == "=1+1");
        Assert.Contains(cells.Descendants(ns + "c"), c => c.Attribute("t") == null && c.Element(ns + "v")?.Value == "1500.25");
        Assert.Contains(cells.Descendants(ns + "v"), v => v.Value == "1200.5");
    }

    [Fact]
    public void Export_labels_productivity_as_calculated_and_keeps_legacy_requested_value_separate()
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto
        {
            ApplicationData = new GrantApplicationData { ProductivityRequestedAmount = 75_000m }
        }, new GrantCalculationDetailsDto { IsAvailable = true, ProductivityAmount = 75_000m }, DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        Assert.Contains("Lavere produktivitet (beregnet)", xml.ToString());
        Assert.Contains("Tidligere oppgitt produktivitetsbeløp (ikke brukt i beregning)", xml.ToString());
        Assert.DoesNotContain("Produktivitetselement", xml.ToString());
    }

    [Fact]
    public void Export_keeps_requested_and_calculated_totals_distinct_and_numeric()
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto
        {
            ApplicationData = new GrantApplicationData
            {
                AdditionalSupervisionCosts = 25_000m,
                AbsenceCompensation = 12_500m,
                SupervisionExpenses = 10_000m
            }
        }, new GrantCalculationDetailsDto
        {
            IsAvailable = true,
            PracticeCompensationAmount = 9_000m,
            GuidanceAmount = 10_000m
        }, DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        Assert.Contains("Søkt beløp (kr, ikke nødvendigvis innvilget)", xml.ToString());
        Assert.Contains("Automatisk beregnet tilskudd (kr)", xml.ToString());
        Assert.Contains("Totalt søkt beløp (ikke nødvendigvis innvilget)", xml.ToString());
        Assert.Contains("Merk: Beregnet veilederhonorar vises kun til informasjon og er ikke inkludert i «Beregnet samlet tilskuddsgrunnlag» nederst i tabellen. Tilskudd til veiledning beregnes separat ut fra oppgitte veiledningskostnader, i påvente av faglig avklaring.", xml.ToString());
        var totalRow = Assert.Single(xml.Descendants("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}row")
            .Where(row => row.Value.Contains("Totalt søkt beløp", StringComparison.Ordinal)));
        var cells = totalRow.Elements("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}c").ToList();
        Assert.Equal("22500", cells[1].Element("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}v")?.Value);
        Assert.Equal("19000", cells[2].Element("{http://schemas.openxmlformats.org/spreadsheetml/2006/main}v")?.Value);
        Assert.Contains("Separat søkt beløp", xml.ToString());
        Assert.Contains("ikke inkludert i automatisk beregning", xml.ToString());
    }

    [Fact]
    public void Export_keeps_additional_supervision_costs_separate_from_calculated_total()
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto
        {
            ApplicationData = new GrantApplicationData
            {
                AdditionalSupervisionCosts = 25_000m,
                SupervisionExpenses = 10_000m
            }
        }, new GrantCalculationDetailsDto
        {
            IsAvailable = true,
            GuidanceAmount = 10_000m
        }, DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        Assert.Contains("Separat søkt beløp", xml.ToString());
        Assert.Contains("ikke inkludert i automatisk beregning", xml.ToString());
        Assert.DoesNotContain("Summen omfatter oppgitte kostnader, inkludert ekstra veiledningskostnader", xml.ToString());
    }

    [Fact]
    public void Unavailable_calculation_does_not_export_zero_as_calculated_amount()
    {
        var bytes = GrantCaseExcelExport.Create(new GrantCaseDto(), new GrantCalculationDetailsDto { UnavailableReason = "Ingen gyldig sats" }, DateTimeOffset.UtcNow);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Contains("Ingen gyldig sats", xml.ToString());
        Assert.Empty(xml.Descendants(ns + "v"));
    }
}
