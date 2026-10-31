using System;
using System.Linq;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Application.Tests.Fakes;
using Alisflyt.Domain.Enums;
using Xunit;

namespace Alisflyt.Application.Tests
{
    public class GrantCaseApplicationServiceFocusedTests
    {
        [Fact]
        public async Task CreateDraftAsync_creates_draft()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-1");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var dto = await svc.CreateDraftAsync(new CreateGrantCaseRequest { HprNumber = "H1" });

            Assert.Equal(GrantCaseStatus.Draft, dto.Status);
            Assert.Equal("H1", dto.HprNumber);
        }

        [Fact]
        public async Task CreateDraftAsync_uses_generated_case_number()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("GENERATED-123");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var dto = await svc.CreateDraftAsync(new CreateGrantCaseRequest());

            Assert.Equal("GENERATED-123", dto.CaseNumber);
        }

        [Fact]
        public async Task CreateDraftAsync_returns_correct_dto()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-2");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var req = new CreateGrantCaseRequest { HprNumber = "HPRX", EmploymentPercentage = 33.3m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,6,1) };
            var dto = await svc.CreateDraftAsync(req);

            Assert.Equal(req.HprNumber, dto.HprNumber);
            Assert.Equal(req.EmploymentPercentage, dto.EmploymentPercentage);
            Assert.Equal(req.EmploymentStartDate, dto.EmploymentStartDate);
            Assert.Equal(req.EmploymentEndDate, dto.EmploymentEndDate);
        }

        [Fact]
        public async Task CreateDraftAsync_calls_add_and_save()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-3");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            await svc.CreateDraftAsync(new CreateGrantCaseRequest());

            Assert.Equal(1, repo.AddAsyncCallCount);
            Assert.Equal(1, repo.SaveChangesCallCount);
        }

        [Fact]
        public async Task GetByIdAsync_returns_correct_dto()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-4");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var created = await svc.CreateDraftAsync(new CreateGrantCaseRequest { HprNumber = "HPRY" });
            var fetched = await svc.GetByIdAsync(created.Id);

            Assert.Equal(created.Id, fetched.Id);
            Assert.Equal(created.CaseNumber, fetched.CaseNumber);
        }

        [Fact]
        public async Task GetByIdAsync_throws_when_case_is_missing()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-5");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task ListAsync_returns_stored_cases()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-6");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var a = await svc.CreateDraftAsync(new CreateGrantCaseRequest());
            var b = await svc.CreateDraftAsync(new CreateGrantCaseRequest());

            var list = await svc.ListAsync();

            Assert.Contains(list, x => x.Id == a.Id);
            Assert.Contains(list, x => x.Id == b.Id);
        }

        [Fact]
        public async Task UpdateDraftAsync_updates_draft()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-7");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var created = await svc.CreateDraftAsync(new CreateGrantCaseRequest { HprNumber = "OLD" });

            var updated = await svc.UpdateDraftAsync(created.Id, new UpdateGrantCaseDraftRequest { HprNumber = "NEW", EmploymentPercentage = 77m });

            Assert.Equal("NEW", updated.HprNumber);
            Assert.Equal(77m, updated.EmploymentPercentage);
        }

        [Fact]
        public async Task UpdateDraftAsync_rejects_submitted_case()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-8");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var created = await svc.CreateDraftAsync(new CreateGrantCaseRequest { HprNumber = "HPRZ" });
            // provide required employment period before submit
            await svc.UpdateDraftAsync(created.Id, new UpdateGrantCaseDraftRequest { HprNumber = "HPRZ", EmploymentPercentage = 10m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,12,31) });
            await svc.SubmitAsync(created.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateDraftAsync(created.Id, new UpdateGrantCaseDraftRequest { HprNumber = "X" }));
        }

        [Fact]
        public async Task SubmitAsync_changes_status_to_submitted()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-9");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var created = await svc.CreateDraftAsync(new CreateGrantCaseRequest { HprNumber = "HPRB", EmploymentPercentage = 40m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,6,1) });
            await svc.SubmitAsync(created.Id);

            var fetched = await svc.GetByIdAsync(created.Id);
            Assert.Equal(GrantCaseStatus.Submitted, fetched.Status);
        }

        [Fact]
        public async Task SubmitAsync_calls_save()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-10");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            var created = await svc.CreateDraftAsync(new CreateGrantCaseRequest { HprNumber = "HPRC", EmploymentPercentage = 45m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,12,31) });

            var before = repo.SaveChangesCallCount;
            await svc.SubmitAsync(created.Id);
            Assert.Equal(before + 1, repo.SaveChangesCallCount);
        }

        [Fact]
        public async Task SubmitAsync_throws_when_case_is_missing()
        {
            var repo = new FakeGrantCaseRepository();
            var gen = new FakeCaseNumberGenerator("CN-11");
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new GrantCaseApplicationService(repo, gen, tp);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.SubmitAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task SubmitAsync_with_guidance_creates_pending_approval_and_saves_once()
        {
            var now = DateTimeOffset.UtcNow;
            var grantCase = CreateCaseWithGuidance(now);
            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-12"), new FakeTimeProvider(now));

            await svc.SubmitAsync(grantCase.Id, new SubmitGrantCaseRequest
            {
                DoctorHprNumber = "123456789",
                SupervisorHprNumber = "987654321",
                SupervisorUserKey = "veileder-per",
                SupervisorName = "Per Olsen"
            });

            Assert.Equal(GrantCaseStatus.Submitted, grantCase.Status);
            var approval = Assert.Single(grantCase.GuidanceApprovals);
            Assert.Equal(GuidanceApprovalStatus.Pending, approval.Status);
            Assert.Equal(1, approval.VersionNumber);
            Assert.Equal(1, repo.SaveChangesCallCount);
        }

        [Fact]
        public async Task SubmitAsync_without_guidance_does_not_create_approval()
        {
            var now = DateTimeOffset.UtcNow;
            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CN-13", now);
            grantCase.UpdateDraft("123456789", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-13"), new FakeTimeProvider(now));

            await svc.SubmitAsync(grantCase.Id, guidanceRequest: null);

            Assert.Equal(GrantCaseStatus.Submitted, grantCase.Status);
            Assert.Empty(grantCase.GuidanceApprovals);
            Assert.Equal(1, repo.SaveChangesCallCount);
        }

        [Fact]
        public async Task SubmitAsync_when_guidance_creation_fails_does_not_save()
        {
            var now = DateTimeOffset.UtcNow;
            var grantCase = CreateCaseWithGuidance(now);
            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-14"), new FakeTimeProvider(now));

            await Assert.ThrowsAsync<ArgumentException>(() => svc.SubmitAsync(grantCase.Id, new SubmitGrantCaseRequest
            {
                DoctorHprNumber = "123456789",
                SupervisorHprNumber = "987654321",
                SupervisorUserKey = ""
            }));

            Assert.Equal(0, repo.SaveChangesCallCount);
            Assert.Equal(GrantCaseStatus.Draft, grantCase.Status);
        }

        [Fact]
        public async Task SubmitAsync_propagates_save_failure_after_single_save_attempt()
        {
            var now = DateTimeOffset.UtcNow;
            var grantCase = CreateCaseWithGuidance(now);
            var repo = new FakeGrantCaseRepository { SaveChangesException = new InvalidOperationException("save failed") };
            await repo.AddAsync(grantCase);
            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-15"), new FakeTimeProvider(now));

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitAsync(grantCase.Id, new SubmitGrantCaseRequest
            {
                DoctorHprNumber = "123456789",
                SupervisorHprNumber = "987654321",
                SupervisorUserKey = "veileder-per"
            }));

            Assert.Equal(1, repo.SaveChangesCallCount);
        }

        private static Alisflyt.Domain.Entities.GrantCase CreateCaseWithGuidance(DateTimeOffset now)
        {
            var grantCase = Alisflyt.Domain.Entities.GrantCase.Create(Guid.NewGuid(), "CN-GUIDANCE", now);
            grantCase.UpdateDraft("123456789", 50m, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), now);
            grantCase.UpdateApplication(new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = "123456789",
                GrantType = Alisflyt.Domain.Forms.GrantType.AlisAgreementIncludingSupervision,
                ConfirmsSpecialization = true,
                SpecializationStartDate = new DateOnly(2025, 1, 1),
                ExpectedCompletionDate = new DateOnly(2028, 1, 1),
                ConfirmsAlisAgreement = true,
                ConfirmsOfficialAgreementTemplate = true,
                ConfirmsNoCommercialAgencyAffiliation = true,
                AgreementEffectiveFrom = new DateOnly(2025, 1, 1),
                FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
                DoctorName = "Anna Hansen",
                DoctorProfessions = ["Allmennlege"],
                SelectedPositionTypes = [Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum],
                EmploymentPeriods = [new Alisflyt.Domain.Forms.EmploymentPeriodInputModel
                {
                    PositionType = Alisflyt.Domain.Forms.PositionType.RegularGpOrLocum,
                    PositionPercentage = 50m,
                    EmploymentStartDate = new DateOnly(2026, 1, 1),
                    FundingFrom = new DateOnly(2026, 1, 1),
                    FundingThrough = new DateOnly(2026, 12, 31)
                }],
                Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                {
                    DoctorName = "Anna Hansen",
                    SupervisorHprNumber = "987654321",
                    SupervisorName = "Per Olsen",
                    Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel
                    {
                        Date = new DateOnly(2026, 1, 10),
                        Hours = 2m,
                        Topic = "Veiledning"
                    }]
                }
            }, now);
            return grantCase;
        }
    }
}
