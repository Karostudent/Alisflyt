using System.Text.Json;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Domain.Tests;

public class ApplicationFormChangesTests
{
    private static GrantApplicationData Read(GrantCase c) => JsonSerializer.Deserialize<GrantApplicationData>(c.ApplicationDataJson!)!;
    private static GrantCase Create(GrantApplicationData data)
    {
        var c = GrantCase.Create(Guid.NewGuid(), "FORM", DateTimeOffset.UtcNow);
        c.UpdateApplication(data, DateTimeOffset.UtcNow);
        return c;
    }

    [Fact]
    public void Form_updates_save_explicit_cost_values_and_preserve_coordinator_values()
    {
        var c = Create(new() { SupervisionExpenses = 123, HasAdditionalSupervisionCosts = true, AdditionalSupervisionCosts = 45 });
        c.UpdateCentrality(true, 1000, DateTimeOffset.UtcNow);
        c.UpdateApplication(new()
        {
            ProductivityRequestedAmount = 250,
            SupervisionExpenses = 999,
            HasAdditionalSupervisionCosts = false,
            AdditionalSupervisionCosts = 0,
            IsCentralityGrade6 = false,
            CentralitySupplementRequestedAmount = 0
        }, DateTimeOffset.UtcNow);
        var saved = Read(c);
        Assert.Equal(250, saved.ProductivityRequestedAmount);
        Assert.Equal(999, saved.SupervisionExpenses);
        Assert.False(saved.HasAdditionalSupervisionCosts);
        Assert.Equal(0, saved.AdditionalSupervisionCosts);
        Assert.True(saved.IsCentralityGrade6);
        Assert.Equal(1000, saved.CentralitySupplementRequestedAmount);
        Assert.Throws<ArgumentException>(() => c.UpdateCentrality(true, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Form_updates_can_remove_additional_supervision_costs()
    {
        var c = Create(new() { HasAdditionalSupervisionCosts = true, AdditionalSupervisionCosts = 45 });

        c.UpdateApplication(new()
        {
            HasAdditionalSupervisionCosts = null,
            AdditionalSupervisionCosts = null
        }, DateTimeOffset.UtcNow);

        var saved = Read(c);
        Assert.Null(saved.HasAdditionalSupervisionCosts);
        Assert.Null(saved.AdditionalSupervisionCosts);
    }

    [Fact]
    public void Legacy_partial_update_does_not_clear_application_data()
    {
        var c = Create(new()
        {
            HasAdditionalSupervisionCosts = true,
            AdditionalSupervisionCosts = 45,
            SupervisionExpenses = 123
        });
        var applicationData = c.ApplicationDataJson;

        c.UpdateDraft("1234567", 80, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), DateTimeOffset.UtcNow);

        Assert.Equal(applicationData, c.ApplicationDataJson);
    }

    [Fact]
    public void Coordinator_can_change_and_clear_centrality_values()
    {
        var c = Create(new());
        c.UpdateCentrality(true, 0, DateTimeOffset.UtcNow);

        var saved = Read(c);
        Assert.True(saved.IsCentralityGrade6);
        Assert.Equal(0, saved.CentralitySupplementRequestedAmount);

        c.UpdateCentrality(false, null, DateTimeOffset.UtcNow);
        saved = Read(c);
        Assert.False(saved.IsCentralityGrade6);
        Assert.Null(saved.CentralitySupplementRequestedAmount);
    }

    [Fact]
    public void Certificate_changes_invalidate_approval_but_other_form_changes_preserve_it()
    {
        var c = Create(new() { Certificate = new() { DoctorName = "Lege", SupervisorHprNumber = "1234567",
            Sessions = [new() { Date = new(2026, 10, 1), Hours = 1, Topic = "Tema" }] } });
        c.ApproveCertificate(DateTimeOffset.UtcNow);
        var form = Read(c);
        Assert.NotNull(form.SupervisorApprovedAtUtc);
        form.ProductivityRequestedAmount = 50;
        c.UpdateApplication(form, DateTimeOffset.UtcNow);
        Assert.NotNull(Read(c).SupervisorApprovedAtUtc);
        form.Certificate.Sessions[0].Hours = 2;
        c.UpdateApplication(form, DateTimeOffset.UtcNow);
        Assert.Null(Read(c).SupervisorApprovedAtUtc);
    }

    [Fact]
    public void Client_cannot_supply_approval_and_incomplete_certificate_cannot_be_approved()
    {
        var c = Create(new() { SupervisorApprovedAtUtc = DateTimeOffset.UtcNow });
        Assert.Null(Read(c).SupervisorApprovedAtUtc);
        Assert.Throws<ArgumentException>(() => c.ApproveCertificate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Multiple_attachments_survive_saving_and_invalid_files_do_not_overwrite()
    {
        var a = new ApplicationAttachment { Kind = "Agreement", FileName = "avtale.pdf", ContentBase64 = Convert.ToBase64String("%PDF-test"u8.ToArray()) };
        var b = new ApplicationAttachment { Kind = "Receipt", FileName = "bilag.pdf", ContentBase64 = a.ContentBase64 };
        var c = Create(new() { Attachments = [a, b] });
        Assert.Equal(2, Read(c).Attachments.Count);
        Assert.Equal(a.ContentBase64, Read(c).Attachments[0].ContentBase64);
        var form = Read(c); form.Attachments[0].ContentBase64 = "invalid";
        Assert.Throws<ArgumentException>(() => c.UpdateApplication(form, DateTimeOffset.UtcNow));
        Assert.Equal(a.ContentBase64, Read(c).Attachments[0].ContentBase64);
        form = Read(c); form.Attachments[0].FileName = "script.html";
        Assert.Throws<ArgumentException>(() => form.ValidateDraft());
    }
}
