using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GuidanceControllerTests
    {
        [Theory]
        [InlineData("All", 2)]
        [InlineData("Draft", 1)]
        [InlineData("Unknown", 2)]
        public async Task Applications_filters_cases(string status, int count)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "veileder-per",
                Role = "Veileder",
                Name = "Per Olsen",
                HprNumber = "987654321"
            });

            var fake = new FakeGrantCaseApplicationService
            {
                ListResponse = new[]
                {
                    new Alisflyt.Application.Models.GrantCaseListItemDto { HprNumber = "123456789", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft },
                    new Alisflyt.Application.Models.GrantCaseListItemDto { HprNumber = "123456789", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted }
                }
            };
            var controller = new Alisflyt.Web.Controllers.GuidanceController(fake, new FakeGrantCaseRepository())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = Assert.IsType<ViewResult>(await controller.Applications(fake, status));
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.GuidanceApplicationsViewModel>(result.Model);
            Assert.Equal(count, model.Cases.Count);
            Assert.Equal(status == "Unknown" ? "All" : status, model.Filter);
        }

        [Fact]
        public async Task GuidanceController_Index_ReturnsGuidanceDashboardViewModel()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "veileder-per",
                Role = "Veileder",
                Name = "Per Olsen",
                HprNumber = "987654321"
            });

            var repo = new GuidanceRepositoryStub();
            var ctrl = new Alisflyt.Web.Controllers.GuidanceController(new FakeGrantCaseApplicationService(), repo)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await ctrl.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.GuidanceDashboardViewModel>(view.Model);
            Assert.Contains("Veiledningsattester", model.Message);
            Assert.NotNull(model.Cases);
        }

        [Fact]
        public async Task Applications_forbids_non_supervisor_role()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "administrator-ole",
                Role = "Administrator",
                Name = "Ole Hovland",
                HprNumber = "999999999"
            });

            var controller = new Alisflyt.Web.Controllers.GuidanceController(new FakeGrantCaseApplicationService(), new FakeGrantCaseRepository())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Applications(new FakeGrantCaseApplicationService(), "All");

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public void DemoAuthentication_maps_doctor_user_key_to_supervisor_user_key()
        {
            Assert.Equal("veileder-per", Alisflyt.Web.Models.DemoAuthentication.GetSupervisorUserKeyForDoctorUserKey("alis-anna"));
            Assert.Contains("alis-anna", Alisflyt.Web.Models.DemoAuthentication.GetAssignedDoctorUserKeys("veileder-per"));
            Assert.Equal("veileder-per", Alisflyt.Web.Models.DemoAuthentication.GetSupervisorUserKeyForDoctor("123456789"));
        }

        [Fact]
        public async Task SupervisorController_Details_forbids_unassigned_supervisor()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "veileder-per",
                Role = "Veileder",
                Name = "Per Olsen",
                HprNumber = "987654321"
            });

            var id = Guid.NewGuid();
            var service = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new Alisflyt.Application.Models.GrantCaseDto
                {
                    Id = id,
                    CaseNumber = "CASE-99",
                    HprNumber = "555555555",
                    Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    LastModifiedAtUtc = DateTimeOffset.UtcNow
                }
            };

            var controller = new Alisflyt.Web.Controllers.SupervisorController(
                service,
                new FakeGrantCaseRepository(),
                TimeProvider.System,
                new FakeGrantCalculationApplicationService(),
                new FakeGuidanceApprovalService())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Details(id, default);

            Assert.IsType<ForbidResult>(result);
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

        private sealed class FakeGrantCaseRepository : Alisflyt.Application.Abstractions.IGrantCaseRepository
        {
            public Task<Alisflyt.Domain.Entities.GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<Alisflyt.Domain.Entities.GrantCase?>(null);

            public Task<IReadOnlyList<Alisflyt.Domain.Entities.GrantCase>> ListAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<Alisflyt.Domain.Entities.GrantCase>>(Array.Empty<Alisflyt.Domain.Entities.GrantCase>());

            public Task AddAsync(Alisflyt.Domain.Entities.GrantCase grantCase, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task AddGuidanceApprovalAsync(Alisflyt.Domain.Entities.GuidanceApproval approval, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class GuidanceRepositoryStub : Alisflyt.Application.Abstractions.IGrantCaseRepository
        {
            public Task<Alisflyt.Domain.Entities.GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<Alisflyt.Domain.Entities.GrantCase?>(null);

            public Task<IReadOnlyList<Alisflyt.Domain.Entities.GrantCase>> ListAsync(CancellationToken cancellationToken = default)
            {
                var now = DateTimeOffset.UtcNow;
                var case1 = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CASE-01", now);
                var app = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    HprNumber = "123456789",
                    DoctorName = "Anna Hansen",
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        DoctorName = "Anna Hansen",
                        SupervisorHprNumber = "987654321",
                        SupervisorName = "Per Olsen",
                        DoctorSigningPlace = "Oslo",
                        SupervisorSigningPlace = "Oslo",
                        Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel { Date = new DateOnly(2026, 1, 10), Hours = 2m, Topic = "Veiledning" }]
                    }
                };
                case1.UpdateApplication(app, now);
                case1.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now, "Per Olsen");
                return Task.FromResult<IReadOnlyList<Alisflyt.Domain.Entities.GrantCase>>([case1]);
            }

            public Task AddAsync(Alisflyt.Domain.Entities.GrantCase grantCase, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task AddGuidanceApprovalAsync(Alisflyt.Domain.Entities.GuidanceApproval approval, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class FakeGrantCalculationApplicationService : Alisflyt.Application.Services.IGrantCalculationApplicationService
        {
            public Task<Alisflyt.Application.Models.GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
                => Task.FromResult(new Alisflyt.Application.Models.GrantCalculationDetailsDto());
        }

        [Fact]
        public async Task GrantCasesController_Submit_creates_pending_guidance_request_for_new_attestation()
        {
            var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
            var caseId = Guid.NewGuid();
            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(caseId, "CASE-PENDING", now);
            var app = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = "123456789",
                DoctorName = "Anna Hansen",
                DoctorProfessions = ["Lege"],
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                ConfirmsSpecialization = true,
                SpecializationStartDate = new DateOnly(2025, 1, 1),
                ExpectedCompletionDate = new DateOnly(2030, 1, 1),
                ConfirmsAlisAgreement = true,
                ConfirmsOfficialAgreementTemplate = true,
                ConfirmsNoCommercialAgencyAffiliation = true,
                AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
                FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
                SelectedPositionTypes = [Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum],
                EmploymentPeriods =
                [
                    new Alisflyt.Domain.Forms.EmploymentPeriodInputModel
                    {
                        PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum,
                        PositionPercentage = 80m,
                        EmploymentStartDate = new DateOnly(2025, 1, 1),
                        FundingFrom = new DateOnly(2026, 1, 1),
                        FundingThrough = new DateOnly(2026, 6, 30)
                    }
                ],
                Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                {
                    DoctorName = "Anna Hansen",
                    SupervisorHprNumber = "987654321",
                    SupervisorName = "Per Olsen",
                    DoctorSigningPlace = "Oslo",
                    SupervisorSigningPlace = "Oslo",
                    DoctorSigningDate = new DateOnly(2026, 6, 30),
                    SupervisorSigningDate = new DateOnly(2026, 6, 30),
                    Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel { Date = new DateOnly(2026, 10, 2), Hours = 2m, Topic = "Veiledning" }]
                }
            };
            grantCase.UpdateApplication(app, now.AddMinutes(1));
            grantCase.Submit(now.AddMinutes(2));

            var repo = new InMemoryGrantCaseRepository(grantCase);
            var service = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new Alisflyt.Application.Models.GrantCaseDto
                {
                    Id = caseId,
                    CaseNumber = "CASE-PENDING",
                    HprNumber = "123456789",
                    Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                    CreatedAtUtc = now,
                    LastModifiedAtUtc = now.AddMinutes(2),
                    ApplicationData = app
                }
            };
            var guidanceService = new Alisflyt.Application.Services.GuidanceApprovalService(repo, TimeProvider.System);
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "alis-anna",
                Role = "ALIS",
                Name = "Anna Hansen",
                HprNumber = "123456789"
            });

            var controller = new Alisflyt.Web.Controllers.GrantCasesController(service, guidanceService, repo)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Submit(caseId, default);

            Assert.IsType<RedirectToActionResult>(result);
            var approval = repo.GetByIdAsync(caseId).GetAwaiter().GetResult()!.GetCurrentGuidanceApproval();
            Assert.NotNull(approval);
            Assert.Equal(Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, approval!.Status);
            Assert.Equal(1, approval.VersionNumber);

            var supervisorContext = new DefaultHttpContext();
            supervisorContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(supervisorContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "veileder-per",
                Role = "Veileder",
                Name = "Per Olsen",
                HprNumber = "987654321"
            });

            var guidanceController = new Alisflyt.Web.Controllers.GuidanceController(new FakeGrantCaseApplicationService(), repo)
            {
                ControllerContext = new ControllerContext { HttpContext = supervisorContext }
            };
            var guidanceIndex = await guidanceController.Index(default);
            var guidanceView = Assert.IsType<ViewResult>(guidanceIndex);
            var guidanceModel = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.GuidanceDashboardViewModel>(guidanceView.Model);
            Assert.True(guidanceModel.PendingCount >= 1);

            var supervisorController = new Alisflyt.Web.Controllers.SupervisorController(
                service,
                repo,
                TimeProvider.System,
                new FakeGrantCalculationApplicationService(),
                guidanceService)
            {
                ControllerContext = new ControllerContext { HttpContext = supervisorContext }
            };

            var detailsResult = await supervisorController.Details(caseId, default);
            var detailsView = Assert.IsType<ViewResult>(detailsResult);
            Assert.NotNull(supervisorController.ViewData["GuidanceApproval"]);
            Assert.Equal("Avventer godkjenning", supervisorController.ViewData["GuidanceStatusText"] as string);
            Assert.NotNull(detailsView.Model);
        }

        [Fact]
        public async Task SupervisorController_Approve_pending_guidance_sets_success_message()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "veileder-per",
                Role = "Veileder",
                Name = "Per Olsen",
                HprNumber = "987654321"
            });

            var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
            var caseId = Guid.NewGuid();
            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(caseId, "CASE-APPROVE", now);
            var app = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = "123456789",
                DoctorName = "Anna Hansen",
                Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                {
                    DoctorName = "Anna Hansen",
                    SupervisorHprNumber = "987654321",
                    SupervisorName = "Per Olsen",
                    DoctorSigningPlace = "Oslo",
                    SupervisorSigningPlace = "Oslo",
                    Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel { Date = new DateOnly(2026, 10, 2), Hours = 2m, Topic = "Veiledning" }]
                }
            };
            grantCase.UpdateApplication(app, now.AddMinutes(1));
            var approval = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");
            var repo = new InMemoryGrantCaseRepository(grantCase);
            var guidanceService = new TrackingGuidanceApprovalService();
            var controller = new Alisflyt.Web.Controllers.SupervisorController(
                new FakeGrantCaseApplicationService(),
                repo,
                TimeProvider.System,
                new FakeGrantCalculationApplicationService(),
                guidanceService)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Approve(caseId, true, default);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(1, guidanceService.ApproveCalls.Count);
            Assert.Equal("Veiledningstimene er godkjent.", controller.TempData["Success"] as string);
            Assert.Equal(approval.Id, guidanceService.ApproveCalls[0].GuidanceApprovalId);
        }

        [Fact]
        public async Task SupervisorController_RequestCorrection_rejects_blank_reason()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "veileder-per",
                Role = "Veileder",
                Name = "Per Olsen",
                HprNumber = "987654321"
            });

            var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
            var caseId = Guid.NewGuid();
            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(caseId, "CASE-CORR", now);
            var app = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = "123456789",
                DoctorName = "Anna Hansen",
                Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                {
                    DoctorName = "Anna Hansen",
                    SupervisorHprNumber = "987654321",
                    SupervisorName = "Per Olsen",
                    DoctorSigningPlace = "Oslo",
                    SupervisorSigningPlace = "Oslo",
                    Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel { Date = new DateOnly(2026, 10, 2), Hours = 2m, Topic = "Veiledning" }]
                }
            };
            grantCase.UpdateApplication(app, now.AddMinutes(1));
            grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");
            var repo = new InMemoryGrantCaseRepository(grantCase);
            var controller = new Alisflyt.Web.Controllers.SupervisorController(
                new FakeGrantCaseApplicationService(),
                repo,
                TimeProvider.System,
                new FakeGrantCalculationApplicationService(),
                new TrackingGuidanceApprovalService())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.RequestCorrection(caseId, "   ", default);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Begrunnelse for korrigering er påkrevd.", controller.TempData["Error"] as string);
        }

        [Fact]
        public async Task SupervisorController_Approve_rejects_unauthed_users()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "alis-anna",
                Role = "ALIS",
                Name = "Anna Hansen",
                HprNumber = "123456789"
            });

            var caseId = Guid.NewGuid();
            var controller = new Alisflyt.Web.Controllers.SupervisorController(
                new FakeGrantCaseApplicationService(),
                new InMemoryGrantCaseRepository(Alisflyt.Domain.Entities.GrantCase.Create(caseId, "CASE-NOT-ALLOWED", DateTimeOffset.UtcNow)),
                TimeProvider.System,
                new FakeGrantCalculationApplicationService(),
                new TrackingGuidanceApprovalService())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Approve(caseId, true, default);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task SupervisorController_RequestCorrection_rejects_unauthed_users()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "koord-ole",
                Role = "Koordinator",
                Name = "Ole Koordinator",
                HprNumber = "111111111"
            });

            var caseId = Guid.NewGuid();
            var controller = new Alisflyt.Web.Controllers.SupervisorController(
                new FakeGrantCaseApplicationService(),
                new InMemoryGrantCaseRepository(Alisflyt.Domain.Entities.GrantCase.Create(caseId, "CASE-NOT-ALLOWED", DateTimeOffset.UtcNow)),
                TimeProvider.System,
                new FakeGrantCalculationApplicationService(),
                new TrackingGuidanceApprovalService())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.RequestCorrection(caseId, "Mangler data", default);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GrantCasesController_Details_shows_correction_notice_for_needs_correction()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Session = new TestSession();
            Alisflyt.Web.Models.DemoAuthentication.SignIn(httpContext, new Alisflyt.Web.Models.DemoUser
            {
                Key = "alis-anna",
                Role = "ALIS",
                Name = "Anna Hansen",
                HprNumber = "123456789"
            });

            var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
            var caseId = Guid.NewGuid();
            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(caseId, "CASE-ALIS-KORR", now);
            var app = new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = "123456789",
                DoctorName = "Anna Hansen",
                Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                {
                    DoctorName = "Anna Hansen",
                    SupervisorHprNumber = "987654321",
                    SupervisorName = "Per Olsen",
                    DoctorSigningPlace = "Oslo",
                    SupervisorSigningPlace = "Oslo",
                    Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel { Date = new DateOnly(2026, 10, 2), Hours = 2m, Topic = "Veiledning" }]
                }
            };
            grantCase.UpdateApplication(app, now.AddMinutes(1));
            var approval = grantCase.RequestGuidanceApproval("123456789", "987654321", "veileder-per", now.AddMinutes(2), "Per Olsen");
            approval.RequestCorrection("Timer stemmer ikke.", now.AddMinutes(3));

            var service = new FakeGrantCaseApplicationService
            {
                GetByIdResponse = new Alisflyt.Application.Models.GrantCaseDto
                {
                    Id = caseId,
                    CaseNumber = "CASE-ALIS-KORR",
                    HprNumber = "123456789",
                    Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                    CreatedAtUtc = now,
                    LastModifiedAtUtc = now
                }
            };
            var controller = new Alisflyt.Web.Controllers.GrantCasesController(service, repository: new InMemoryGrantCaseRepository(grantCase))
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };

            var result = await controller.Details(caseId, default);

            Assert.IsType<ViewResult>(result);
            Assert.Equal("Korrigering nødvendig", controller.ViewData["GuidanceCorrectionStatus"] as string);
            Assert.Equal("Timer stemmer ikke.", controller.ViewData["GuidanceCorrectionReason"] as string);
        }

        private sealed class FakeGuidanceApprovalService : Alisflyt.Application.Services.IGuidanceApprovalService
        {
            public Task<Alisflyt.Domain.Entities.GuidanceApproval> RequestApprovalAsync(Guid grantCaseId, string doctorHprNumber, string supervisorHprNumber, string supervisorUserKey, string? supervisorName, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Alisflyt.Domain.Entities.GuidanceApproval> ApproveAsync(Guid grantCaseId, Guid guidanceApprovalId, string approvedByUserKey, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Alisflyt.Domain.Entities.GuidanceApproval> RequestCorrectionAsync(Guid grantCaseId, Guid guidanceApprovalId, string reason, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Alisflyt.Domain.Entities.GuidanceApproval?> GetLatestApprovalAsync(Guid grantCaseId, CancellationToken cancellationToken = default)
                => Task.FromResult<Alisflyt.Domain.Entities.GuidanceApproval?>(null);
        }

        private sealed class TrackingGuidanceApprovalService : Alisflyt.Application.Services.IGuidanceApprovalService
        {
            public List<(Guid GrantCaseId, Guid GuidanceApprovalId, string ApprovedByUserKey)> ApproveCalls { get; } = [];
            public List<(Guid GrantCaseId, Guid GuidanceApprovalId, string Reason)> CorrectionCalls { get; } = [];

            public Task<Alisflyt.Domain.Entities.GuidanceApproval> RequestApprovalAsync(Guid grantCaseId, string doctorHprNumber, string supervisorHprNumber, string supervisorUserKey, string? supervisorName, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<Alisflyt.Domain.Entities.GuidanceApproval> ApproveAsync(Guid grantCaseId, Guid guidanceApprovalId, string approvedByUserKey, CancellationToken cancellationToken = default)
            {
                ApproveCalls.Add((grantCaseId, guidanceApprovalId, approvedByUserKey));
                return Task.FromResult<Alisflyt.Domain.Entities.GuidanceApproval>(null!);
            }

            public Task<Alisflyt.Domain.Entities.GuidanceApproval> RequestCorrectionAsync(Guid grantCaseId, Guid guidanceApprovalId, string reason, CancellationToken cancellationToken = default)
            {
                CorrectionCalls.Add((grantCaseId, guidanceApprovalId, reason));
                return Task.FromResult<Alisflyt.Domain.Entities.GuidanceApproval>(null!);
            }

            public Task<Alisflyt.Domain.Entities.GuidanceApproval?> GetLatestApprovalAsync(Guid grantCaseId, CancellationToken cancellationToken = default)
                => Task.FromResult<Alisflyt.Domain.Entities.GuidanceApproval?>(null);
        }

        private sealed class InMemoryGrantCaseRepository : Alisflyt.Application.Abstractions.IGrantCaseRepository
        {
            private readonly Alisflyt.Domain.Entities.GrantCase _grantCase;

            public InMemoryGrantCaseRepository(Alisflyt.Domain.Entities.GrantCase grantCase)
            {
                _grantCase = grantCase;
            }

            public Task<Alisflyt.Domain.Entities.GrantCase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<Alisflyt.Domain.Entities.GrantCase?>(_grantCase.Id == id ? _grantCase : null);

            public Task<IReadOnlyList<Alisflyt.Domain.Entities.GrantCase>> ListAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<Alisflyt.Domain.Entities.GrantCase>>([_grantCase]);

            public Task AddAsync(Alisflyt.Domain.Entities.GrantCase grantCase, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task AddGuidanceApprovalAsync(Alisflyt.Domain.Entities.GuidanceApproval approval, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
