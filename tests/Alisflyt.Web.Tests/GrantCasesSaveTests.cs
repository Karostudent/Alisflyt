using System.Text.Json;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Models;
using Alisflyt.Web.Tests.Fakes;
using Alisflyt.Web.ViewModels.GrantCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Alisflyt.Web.Tests;

public class GrantCasesSaveTests
{
    [Fact]
    public void Create_uses_shared_view_with_empty_form()
    {
        var controller = new GrantCasesController(new FakeGrantCaseApplicationService());
        var view = Assert.IsType<ViewResult>(controller.Create());
        Assert.Equal("Application", view.ViewName);
        var model = Assert.IsType<GrantApplicationFormViewModel>(view.Model);
        Assert.Equal(Guid.Empty, model.Id);
        Assert.Null(model.HprNumber);
        Assert.Equal(Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision, model.GrantType);
        Assert.Empty(model.EmploymentPeriods);
        Assert.Empty(model.Certificate.Sessions);
        Assert.False(model.ReadOnly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_preserves_both_steps_and_returns_navigation_links(bool existing)
    {
        var savedId = Guid.NewGuid();
        var dto = new GrantCaseDto { Id = savedId };
        var fake = new FakeGrantCaseApplicationService { CreateDraftResponse = dto, UpdateDraftResponse = dto };
        var controller = new GrantCasesController(fake) { Url = new GrantCaseUrlHelper() };
        var model = new GrantApplicationFormViewModel { Id = existing ? savedId : Guid.Empty, HprNumber = "1234567",
            EmploymentPeriods = [new() { PositionPercentage = 50 }, new() { PositionPercentage = 25 }],
            Certificate = new() { Sessions = [new() { Hours = 1.5m, Topic = "Tema" }] } };
        var result = Assert.IsType<OkObjectResult>(await controller.SaveApplication(model, default));
        if (existing)
        {
            var call = Assert.Single(fake.UpdateDraftCalls);
            Assert.Equal(savedId, call.id);
            Assert.Same(model, call.request.ApplicationData);
            Assert.Empty(fake.CreateDraftCalls);
        }
        else
        {
            Assert.Same(model, Assert.Single(fake.CreateDraftCalls).request.ApplicationData);
            Assert.Empty(fake.UpdateDraftCalls);
        }
        Assert.Equal(Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision, model.GrantType);
        var response = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(savedId, response.GetProperty("id").GetGuid());
        Assert.Equal($"/GrantCases/Edit/{savedId}", response.GetProperty("editUrl").GetString());
        Assert.Equal($"/GrantCases/Details/{savedId}", response.GetProperty("detailsUrl").GetString());
    }

    [Fact]
    public async Task SaveApplication_persists_real_form_data_across_updates()
    {
        var repository = new InMemoryGrantCaseRepository();
        var service = new GrantCaseApplicationService(repository, new TestCaseNumberGenerator("CASE-101"), TimeProvider.System);
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        DemoAuthentication.SignIn(httpContext, new DemoUser { Key = "alis-anna", Role = "ALIS", Name = "Anna Hansen", HprNumber = "123456789" });
        var controller = new GrantCasesController(service, repository: repository)
        {
            Url = new GrantCaseUrlHelper(),
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var model = new GrantApplicationFormViewModel
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Lege", "Spesialist"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2024, 1, 1),
            ExpectedCompletionDate = new DateOnly(2030, 1, 1),
            FirstRegularGpOrLocumDate = new DateOnly(2022, 1, 1),
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 50m,
                    EmploymentStartDate = new DateOnly(2024, 1, 1),
                    FundingFrom = new DateOnly(2024, 1, 1),
                    FundingThrough = new DateOnly(2024, 12, 31)
                }
            ],
            AbsenceCompensation = 100m,
            LearningActivityExpenses = 250m,
            SupervisionExpenses = 500m,
            HasAdditionalSupervisionCosts = true,
            AdditionalSupervisionCosts = 600m,
            Certificate = new SupervisionCertificateViewModel
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                DoctorSigningPlace = "Oslo",
                SupervisorSigningPlace = "Oslo",
                DoctorSigningDate = new DateOnly(2024, 6, 1),
                SupervisorSigningDate = new DateOnly(2024, 6, 2),
                Sessions =
                [
                    new SupervisionSessionInputModel { Date = new DateOnly(2024, 2, 1), Hours = 1.5m, Topic = "Kommunikasjon" }
                ]
            }
        };

        var created = Assert.IsType<OkObjectResult>(await controller.SaveApplication(model, default));
        var id = JsonSerializer.SerializeToElement(created.Value).GetProperty("id").GetGuid();

        var loaded = await service.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal("Anna Hansen", loaded!.ApplicationData!.DoctorName);
        Assert.Equal("123456789", loaded.ApplicationData.HprNumber);
        Assert.Equal(2, loaded.ApplicationData.DoctorProfessions.Count);
        Assert.Equal("Kommunikasjon", loaded.ApplicationData.Certificate.Sessions[0].Topic);
        Assert.Equal(50m, loaded.ApplicationData.EmploymentPeriods[0].PositionPercentage);

        loaded.ApplicationData.DoctorName = "Anna Oppdatert";
        loaded.ApplicationData.Certificate.Sessions[0].Topic = "Oppdatert tema";
        await controller.SaveApplication(new GrantApplicationFormViewModel
        {
            Id = id,
            HprNumber = loaded.ApplicationData.HprNumber,
            DoctorName = loaded.ApplicationData.DoctorName,
            DoctorProfessions = loaded.ApplicationData.DoctorProfessions,
            GrantType = loaded.ApplicationData.GrantType,
            SelectedPositionTypes = loaded.ApplicationData.SelectedPositionTypes,
            EmploymentPeriods = loaded.ApplicationData.EmploymentPeriods,
            Certificate = loaded.ApplicationData.Certificate,
            AbsenceCompensation = loaded.ApplicationData.AbsenceCompensation,
            LearningActivityExpenses = loaded.ApplicationData.LearningActivityExpenses,
            SupervisionExpenses = loaded.ApplicationData.SupervisionExpenses,
            HasAdditionalSupervisionCosts = loaded.ApplicationData.HasAdditionalSupervisionCosts,
            AdditionalSupervisionCosts = loaded.ApplicationData.AdditionalSupervisionCosts,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2024, 1, 1),
            ExpectedCompletionDate = new DateOnly(2030, 1, 1),
            FirstRegularGpOrLocumDate = new DateOnly(2022, 1, 1)
        }, default);

        var reloaded = await service.GetByIdAsync(id);
        Assert.NotNull(reloaded);
        Assert.Equal("Anna Oppdatert", reloaded!.ApplicationData!.DoctorName);
        Assert.Equal("Oppdatert tema", reloaded.ApplicationData.Certificate.Sessions[0].Topic);
    }

    [Fact]
    public async Task SaveApplication_allows_valid_step_one_draft_before_certificate_is_filled()
    {
        var repository = new InMemoryGrantCaseRepository();
        var service = new GrantCaseApplicationService(repository, new TestCaseNumberGenerator("CASE-102"), TimeProvider.System);
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        DemoAuthentication.SignIn(httpContext, new DemoUser { Key = "alis-anna", Role = "ALIS", Name = "Anna Hansen", HprNumber = "123456789" });
        var controller = new GrantCasesController(service, repository: repository)
        {
            Url = new GrantCaseUrlHelper(),
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var model = new GrantApplicationFormViewModel
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Lege"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2024, 1, 1),
            ExpectedCompletionDate = new DateOnly(2030, 1, 1),
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 50m,
                    EmploymentStartDate = new DateOnly(2024, 1, 1),
                    FundingFrom = new DateOnly(2024, 1, 1),
                    FundingThrough = new DateOnly(2024, 12, 31)
                }
            ],
            Certificate = new SupervisionCertificateViewModel()
        };

        var result = await controller.SaveApplication(model, default);
        var ok = Assert.IsType<OkObjectResult>(result);
        var element = JsonSerializer.SerializeToElement(ok.Value);
        var id = element.GetProperty("id").GetGuid();
        var saved = await service.GetByIdAsync(id);
        Assert.NotNull(saved);
        Assert.Equal("Anna Hansen", saved!.ApplicationData!.DoctorName);
        Assert.Empty(saved.ApplicationData.Certificate.Sessions);
    }

    [Fact]
    public async Task SaveApplication_does_not_erase_step_one_data_when_updating_certificate_sessions()
    {
        var repository = new InMemoryGrantCaseRepository();
        var service = new GrantCaseApplicationService(repository, new TestCaseNumberGenerator("CASE-103"), TimeProvider.System);
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        DemoAuthentication.SignIn(httpContext, new DemoUser { Key = "alis-anna", Role = "ALIS", Name = "Anna Hansen", HprNumber = "123456789" });
        var controller = new GrantCasesController(service, repository: repository)
        {
            Url = new GrantCaseUrlHelper(),
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var initial = new GrantApplicationFormViewModel
        {
            HprNumber = "123456789",
            DoctorName = "Anna Hansen",
            DoctorProfessions = ["Lege"],
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            ConfirmsSpecialization = true,
            SpecializationStartDate = new DateOnly(2024, 1, 1),
            ExpectedCompletionDate = new DateOnly(2030, 1, 1),
            SelectedPositionTypes = [PositionType.RegularGpOrLocum],
            EmploymentPeriods =
            [
                new EmploymentPeriodInputModel
                {
                    PositionType = PositionType.RegularGpOrLocum,
                    PositionPercentage = 50m,
                    EmploymentStartDate = new DateOnly(2024, 1, 1),
                    FundingFrom = new DateOnly(2024, 1, 1),
                    FundingThrough = new DateOnly(2024, 12, 31)
                }
            ],
            AbsenceCompensation = 100m,
            LearningActivityExpenses = 75m,
            SupervisionExpenses = 250m,
            Certificate = new SupervisionCertificateViewModel
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                Sessions = [new SupervisionSessionInputModel { Date = new DateOnly(2024, 2, 1), Hours = 1.5m, Topic = "Opprinnelig tema" }]
            }
        };

        var created = Assert.IsType<OkObjectResult>(await controller.SaveApplication(initial, default));
        var id = JsonSerializer.SerializeToElement(created.Value).GetProperty("id").GetGuid();

        var updated = new GrantApplicationFormViewModel
        {
            Id = id,
            HprNumber = initial.HprNumber,
            DoctorName = initial.DoctorName,
            DoctorProfessions = initial.DoctorProfessions,
            GrantType = initial.GrantType,
            ConfirmsSpecialization = initial.ConfirmsSpecialization,
            SpecializationStartDate = initial.SpecializationStartDate,
            ExpectedCompletionDate = initial.ExpectedCompletionDate,
            SelectedPositionTypes = initial.SelectedPositionTypes,
            EmploymentPeriods = initial.EmploymentPeriods,
            AbsenceCompensation = initial.AbsenceCompensation,
            LearningActivityExpenses = initial.LearningActivityExpenses,
            SupervisionExpenses = initial.SupervisionExpenses,
            Certificate = new SupervisionCertificateViewModel
            {
                DoctorName = "Anna Hansen",
                SupervisorHprNumber = "987654321",
                SupervisorName = "Per Olsen",
                Sessions = [
                    new SupervisionSessionInputModel { Date = new DateOnly(2024, 2, 1), Hours = 2.0m, Topic = "Oppdatert tema" },
                    new SupervisionSessionInputModel { Date = new DateOnly(2024, 2, 5), Hours = 1.0m, Topic = "Andre tema" }
                ]
            }
        };

        var result = Assert.IsType<OkObjectResult>(await controller.SaveApplication(updated, default));
        var saved = await service.GetByIdAsync(id);

        Assert.NotNull(saved);
        Assert.Equal("123456789", saved!.ApplicationData!.HprNumber);
        Assert.Equal("Anna Hansen", saved.ApplicationData.DoctorName);
        Assert.Equal(50m, saved.ApplicationData.EmploymentPeriods[0].PositionPercentage);
        Assert.Equal(2, saved.ApplicationData.Certificate.Sessions.Count);
        Assert.Equal("Oppdatert tema", saved.ApplicationData.Certificate.Sessions[0].Topic);
        Assert.Equal("Andre tema", saved.ApplicationData.Certificate.Sessions[1].Topic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_model_is_not_saved(bool existing)
    {
        var fake = new FakeGrantCaseApplicationService();
        var controller = new GrantCasesController(fake);
        controller.ModelState.AddModelError("Certificate.Sessions[0].Hours", "Ugyldig antall timer");
        Assert.IsType<BadRequestObjectResult>(await controller.SaveApplication(new() { Id = existing ? Guid.NewGuid() : Guid.Empty }, default));
        Assert.Empty(fake.CreateDraftCalls);
        Assert.Empty(fake.UpdateDraftCalls);
    }

    [Fact]
    public async Task Submitted_case_cannot_be_edited_or_saved()
    {
        var fake = new FakeGrantCaseApplicationService { GetByIdResponse = new() { Status = GrantCaseStatus.Submitted }, UpdateDraftException = new InvalidOperationException() };
        var controller = new GrantCasesController(fake);
        var id = Guid.NewGuid();
        Assert.IsType<BadRequestResult>(await controller.Edit(id, default));
        Assert.IsType<ConflictObjectResult>(await controller.SaveApplication(new() { Id = id }, default));
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id { get; } = Guid.NewGuid().ToString();
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }

    private sealed class TestCaseNumberGenerator : ICaseNumberGenerator
    {
        private readonly string _value;

        public TestCaseNumberGenerator(string value) => _value = value;

        public ValueTask<string> GenerateAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_value);
    }

    private sealed class InMemoryGrantCaseRepository : IGrantCaseRepository
    {
        private readonly Dictionary<Guid, GrantCase> _store = new();

        public Task<GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(id, out var grantCase) ? grantCase : null);

        public Task<IReadOnlyList<GrantCase>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GrantCase>>(_store.Values.ToList());

        public Task AddAsync(GrantCase grantCase, CancellationToken cancellationToken = default)
        {
            _store[grantCase.Id] = grantCase;
            return Task.CompletedTask;
        }

        public Task AddGuidanceApprovalAsync(GuidanceApproval approval, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
