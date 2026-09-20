using System;
using System.Threading;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Application.Abstractions;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Xunit;
using Alisflyt.Application.Tests.Fakes;

namespace Alisflyt.Application.Tests
{
    public class GrantCaseApplicationServiceReturnTests
    {
        [Fact]
        public async Task StartReviewAsync_changes_status_and_saves()
        {
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var grantCase = GrantCase.Create(id, "CASE-A1", now);
            grantCase.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), now.AddMinutes(1));
            grantCase.Submit(now.AddMinutes(2));

            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            await repo.SaveChangesAsync();

            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-X"), new FakeTimeProvider(now));

            await svc.StartReviewAsync(id, CancellationToken.None);

            var stored = await repo.GetByIdAsync(id);
            Assert.Equal(GrantCaseStatus.UnderReview, stored!.Status);
        }

        [Fact]
        public async Task StartReviewAsync_throws_when_not_found()
        {
            var repo = new FakeGrantCaseRepository();
            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-X"), new FakeTimeProvider(DateTimeOffset.UtcNow));

            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.StartReviewAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task ReturnForCorrectionAsync_changes_status_and_saves()
        {
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var grantCase = GrantCase.Create(id, "CASE-A2", now);
            grantCase.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), now.AddMinutes(1));
            grantCase.Submit(now.AddMinutes(2));
            grantCase.StartReview(now.AddMinutes(3));

            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            await repo.SaveChangesAsync();

            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-X"), new FakeTimeProvider(now));

            var req = new ReturnForCorrectionRequest { Reason = "Please fix" };
            await svc.ReturnForCorrectionAsync(id, req);

            var stored = await repo.GetByIdAsync(id);
            Assert.Equal(GrantCaseStatus.ReturnedForCorrection, stored!.Status);
            Assert.Equal("Please fix", stored.ReturnReason);
            Assert.NotNull(stored.ReturnedAtUtc);
        }

        [Fact]
        public async Task ReturnForCorrectionAsync_throws_when_not_found()
        {
            var repo = new FakeGrantCaseRepository();
            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-X"), new FakeTimeProvider(DateTimeOffset.UtcNow));

            var req = new ReturnForCorrectionRequest { Reason = "x" };
            await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.ReturnForCorrectionAsync(Guid.NewGuid(), req));
        }

        [Fact]
        public async Task Domain_exceptions_propagate()
        {
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var grantCase = GrantCase.Create(id, "CASE-A3", now);
            // not submitted

            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            await repo.SaveChangesAsync();

            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-X"), new FakeTimeProvider(now));

            var req = new ReturnForCorrectionRequest { Reason = "x" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ReturnForCorrectionAsync(id, req));
        }

        [Fact]
        public async Task GetById_includes_return_fields()
        {
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var grantCase = GrantCase.Create(id, "CASE-A4", now);
            grantCase.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), now.AddMinutes(1));
            grantCase.Submit(now.AddMinutes(2));
            grantCase.StartReview(now.AddMinutes(3));
            grantCase.ReturnForCorrection("Reason", now.AddMinutes(4));

            var repo = new FakeGrantCaseRepository();
            await repo.AddAsync(grantCase);
            await repo.SaveChangesAsync();

            var svc = new GrantCaseApplicationService(repo, new FakeCaseNumberGenerator("CN-X"), new FakeTimeProvider(now));

            var dto = await svc.GetByIdAsync(id);

            Assert.Equal("Reason", dto.ReturnReason);
            Assert.NotNull(dto.ReturnedAtUtc);
        }
    }
}
