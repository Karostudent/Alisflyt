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
    }
}
