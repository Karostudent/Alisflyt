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

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class FakeGrantCalculationApplicationService : Alisflyt.Application.Services.IGrantCalculationApplicationService
        {
            public Task<Alisflyt.Application.Models.GrantCalculationDetailsDto> CalculateForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
                => Task.FromResult(new Alisflyt.Application.Models.GrantCalculationDetailsDto());
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
    }
}
