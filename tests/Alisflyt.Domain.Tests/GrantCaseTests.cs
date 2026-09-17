using System;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Xunit;

namespace Alisflyt.Domain.Tests
{
    public class GrantCaseTests
    {
        [Fact]
        public void New_case_is_draft()
        {
            var id = Guid.NewGuid();
            var created = DateTimeOffset.UtcNow;
            var c = GrantCase.Create(id, "CASE-1", created);

            Assert.Equal(id, c.Id);
            Assert.Equal("CASE-1", c.CaseNumber);
            Assert.Equal(GrantCaseStatus.Draft, c.Status);
            Assert.Equal(created, c.CreatedAtUtc);
            Assert.Equal(created, c.LastModifiedAtUtc);
        }

        [Fact]
        public void Can_create_and_save_incomplete_draft()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-2", DateTimeOffset.UtcNow);
            var before = c.LastModifiedAtUtc;
            var now = before.AddMinutes(5);

            c.UpdateDraft(null, null, null, null, now);

            Assert.Null(c.HprNumber);
            Assert.Null(c.EmploymentPercentage);
            Assert.Null(c.EmploymentStartDate);
            Assert.Null(c.EmploymentEndDate);
            Assert.Equal(now, c.LastModifiedAtUtc);
        }

        [Fact]
        public void Valid_draft_can_be_submitted()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-3", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR123", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            var submitTime = DateTimeOffset.UtcNow.AddMinutes(2);

            c.Submit(submitTime);

            Assert.Equal(GrantCaseStatus.Submitted, c.Status);
            Assert.Equal(submitTime, c.LastModifiedAtUtc);
        }

        [Fact]
        public void Submit_requires_hpr()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-4", DateTimeOffset.UtcNow);
            c.UpdateDraft(null, 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(() => c.Submit(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Submit_requires_employment_percentage()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-5", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR123", null, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(() => c.Submit(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Employment_percentage_zero_is_invalid()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-6", DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentOutOfRangeException>(() => c.UpdateDraft("HPR123", 0m, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Employment_percentage_over_100_is_invalid()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-7", DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentOutOfRangeException>(() => c.UpdateDraft("HPR123", 150m, null, null, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void End_date_before_start_date_is_invalid()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-8", DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(() => c.UpdateDraft("HPR123", 50m, new DateOnly(2026,6,1), new DateOnly(2026,1,1), DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Submitted_case_cannot_be_edited_or_resubmitted()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-9", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR123", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow);
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() => c.UpdateDraft("X", 60m, null, null, DateTimeOffset.UtcNow));
            Assert.Throws<InvalidOperationException>(() => c.Submit(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void LastModified_updates_on_edit_and_submit()
        {
            var created = DateTimeOffset.UtcNow;
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-10", created);
            var editTime = created.AddHours(1);
            c.UpdateDraft("HPR123", 25m, null, null, editTime);

            Assert.Equal(editTime, c.LastModifiedAtUtc);

            var submitTime = editTime.AddHours(1);
            c.UpdateDraft("HPR123", 25m, new DateOnly(2026,1,1), new DateOnly(2026,12,31), submitTime);
            c.Submit(submitTime.AddMinutes(1));

            Assert.Equal(submitTime.AddMinutes(1), c.LastModifiedAtUtc);
        }
    }
}
