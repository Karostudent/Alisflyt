using System.Text.Json;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Application.Tests.Fakes;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Application.Tests;

public class GrantApplicationFormTests
{
    private static GrantCaseApplicationService Service() => new(new FakeGrantCaseRepository(), new FakeCaseNumberGenerator("FORM-1"), TimeProvider.System);

    private static GrantApplicationData CompleteForm() => new()
    {
        HprNumber = "1234567", DoctorName = "Test Lege", DoctorProfessions = ["Lege"],
        GrantType = GrantType.AlisAgreementIncludingSupervision, ConfirmsSpecialization = true,
        SpecializationStartDate = new(2025, 1, 1), ExpectedCompletionDate = new(2030, 1, 1),
        ConfirmsAlisAgreement = true, ConfirmsOfficialAgreementTemplate = true,
        ConfirmsNoCommercialAgencyAffiliation = true, AgreementEffectiveFrom = new(2025, 1, 1),
        SelectedPositionTypes = [PositionType.RegularGpOrLocum, PositionType.IntroductoryDoctor],
        EmploymentPeriods = [
            new() { PositionType = PositionType.RegularGpOrLocum, PositionPercentage = 80, EmploymentStartDate = new(2025, 1, 1), FundingFrom = new(2026, 1, 1), FundingThrough = new(2026, 6, 30) },
            new() { PositionType = PositionType.IntroductoryDoctor, PositionPercentage = 20, EmploymentStartDate = new(2025, 1, 1), FundingFrom = new(2026, 1, 1), FundingThrough = new(2026, 6, 30) }
        ],
        AbsenceCompensation = 1200.50m, LearningActivityExpenses = 2000, SupervisionExpenses = 3000,
        HasAdditionalSupervisionCosts = true, AdditionalSupervisionCosts = 400,
        Certificate = new() { DoctorName = "Test Lege", SupervisorName = "Test Veileder",
            DoctorSigningPlace = "Oslo", DoctorSigningDate = new(2026, 6, 30),
            SupervisorSigningPlace = "Oslo", SupervisorSigningDate = new(2026, 6, 30),
            Sessions = [new() { Date = new(2026, 2, 1), Hours = 1.5m, Topic = "Kommunikasjon" }, new() { Date = new(2026, 3, 1), Hours = 2, Topic = "Diagnostikk" }] }
    };

    [Fact]
    public async Task Both_steps_survive_create_reopen_edit_and_review()
    {
        var service = Service();
        var data = CompleteForm();
        var created = await service.CreateDraftAsync(new() { ApplicationData = data });
        var reopened = await service.GetByIdAsync(created.Id);
        Assert.Equal(JsonSerializer.Serialize(data), JsonSerializer.Serialize(reopened.ApplicationData));
        // Editing the detached DTO must not silently change the stored draft.
        reopened.ApplicationData!.Certificate.Sessions.RemoveAt(0);
        reopened.ApplicationData.EmploymentPeriods.RemoveAt(1);
        reopened.ApplicationData.SelectedPositionTypes.RemoveAt(1);
        Assert.Equal(2, (await service.GetByIdAsync(created.Id)).ApplicationData!.Certificate.Sessions.Count);
        await service.UpdateDraftAsync(created.Id, new() { ApplicationData = reopened.ApplicationData });
        await service.SubmitAsync(created.Id);
        await service.StartReviewAsync(created.Id);
        var reviewed = await service.GetByIdAsync(created.Id);
        Assert.Equal(GrantCaseStatus.UnderReview, reviewed.Status);
        Assert.Single(reviewed.ApplicationData!.EmploymentPeriods);
        Assert.Equal("Diagnostikk", Assert.Single(reviewed.ApplicationData.Certificate.Sessions).Topic);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateDraftAsync(created.Id, new() { ApplicationData = data }));
        await service.ReturnForCorrectionAsync(created.Id, new() { Reason = "Oppdater veiledningen" });
        await service.UpdateDraftAsync(created.Id, new() { ApplicationData = data });
        await service.SubmitAsync(created.Id);
        Assert.Equal(2, (await service.GetByIdAsync(created.Id)).ApplicationData!.Certificate.Sessions.Count);
    }

    [Fact]
    public async Task Incomplete_application_can_be_saved_but_not_submitted()
    {
        var service = Service();
        var created = await service.CreateDraftAsync(new() { ApplicationData = new() });
        Assert.NotNull((await service.GetByIdAsync(created.Id)).ApplicationData);
        var error = await Assert.ThrowsAsync<SubmissionValidationException>(() => service.SubmitAsync(created.Id));
        Assert.Equal(8, error.Errors.Count);
        Assert.Contains("Oppgi HPR-nummer for legen.", error.Errors);
        Assert.Contains("Oppgi når legen startet spesialiseringsløpet.", error.Errors);
    }

    [Fact]
    public async Task Invalid_second_period_or_session_does_not_overwrite_draft()
    {
        var service = Service();
        var data = CompleteForm();
        var created = await service.CreateDraftAsync(new() { ApplicationData = data });
        data.EmploymentPeriods[1].PositionPercentage = 101;
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(created.Id, new() { ApplicationData = data }));
        Assert.Equal(20, (await service.GetByIdAsync(created.Id)).ApplicationData!.EmploymentPeriods[1].PositionPercentage);
        data.EmploymentPeriods[1].PositionPercentage = 20;
        data.Certificate.Sessions[1].Hours = 25;
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(created.Id, new() { ApplicationData = data }));
    }

    [Fact]
    public async Task Submission_checks_every_employment_row()
    {
        var service = Service();
        var data = CompleteForm();
        data.EmploymentPeriods[1].FundingFrom = null;
        var created = await service.CreateDraftAsync(new() { ApplicationData = data });
        var error = await Assert.ThrowsAsync<SubmissionValidationException>(() => service.SubmitAsync(created.Id));
        Assert.Equal("Stilling i kommunen, rad 2: Oppgi «Tilskudd fra».", Assert.Single(error.Errors));
    }

    [Fact]
    public void Agreement_requirements_only_apply_when_agreement_grant_is_selected()
    {
        var data = CompleteForm();
        data.ConfirmsAlisAgreement = false;
        data.ConfirmsOfficialAgreementTemplate = false;
        data.AgreementEffectiveFrom = null;
        var error = Assert.Throws<SubmissionValidationException>(() => data.ValidateSubmission());
        Assert.Equal(3, error.Errors.Count);
        Assert.Contains("Oppgi når ALIS-avtalen gjelder fra.", error.Errors);
        data.GrantType = GrantType.SupervisionOnly;
        data.ValidateSubmission();
    }
}
