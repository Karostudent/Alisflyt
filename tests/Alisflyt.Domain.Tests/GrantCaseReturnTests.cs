using System;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Xunit;

namespace Alisflyt.Domain.Tests
{
    public class GrantCaseReturnTests
    {
        [Fact]
        public void Submitted_to_UnderReview_success()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R1", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            var submitTime = DateTimeOffset.UtcNow.AddMinutes(2);
            c.Submit(submitTime);

            var reviewTime = submitTime.AddMinutes(5);
            c.StartReview(reviewTime);

            Assert.Equal(GrantCaseStatus.UnderReview, c.Status);
            Assert.Equal(reviewTime, c.LastModifiedAtUtc);
        }

        [Fact]
        public void StartReview_rejected_from_other_status()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R2", DateTimeOffset.UtcNow);
            Assert.Throws<InvalidOperationException>(() => c.StartReview(DateTimeOffset.UtcNow));
        }

        [Fact]
        public void UnderReview_to_ReturnedForCorrection_success()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R3", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            var reviewTime = DateTimeOffset.UtcNow.AddMinutes(3);
            c.StartReview(reviewTime);

            var returnTime = reviewTime.AddMinutes(2);
            c.ReturnForCorrection("Feil i ansettelsesdato.", returnTime);

            Assert.Equal(GrantCaseStatus.ReturnedForCorrection, c.Status);
            Assert.Equal(returnTime, c.ReturnedAtUtc);
            Assert.Equal(returnTime, c.LastModifiedAtUtc);
            Assert.Equal("Feil i ansettelsesdato.", c.ReturnReason);
        }

        [Fact]
        public void ReturnForCorrection_rejects_empty_reason()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R4", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            c.StartReview(DateTimeOffset.UtcNow.AddMinutes(3));

            Assert.Throws<ArgumentException>(() => c.ReturnForCorrection("   ", DateTimeOffset.UtcNow));
        }

        [Fact]
        public void ReturnForCorrection_rejects_too_long_reason()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R5", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            c.StartReview(DateTimeOffset.UtcNow.AddMinutes(3));

            var longReason = new string('x', 1001);
            Assert.Throws<ArgumentException>(() => c.ReturnForCorrection(longReason, DateTimeOffset.UtcNow));
        }

        [Fact]
        public void Return_reason_is_trimmed()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R6", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            c.StartReview(DateTimeOffset.UtcNow.AddMinutes(3));

            var reason = "  Needs correction  ";
            c.ReturnForCorrection(reason, DateTimeOffset.UtcNow);

            Assert.Equal("Needs correction", c.ReturnReason);
        }

        [Fact]
        public void Return_rejected_from_wrong_status()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R7", DateTimeOffset.UtcNow);
            Assert.Throws<InvalidOperationException>(() => c.ReturnForCorrection("Reason", DateTimeOffset.UtcNow));
        }

        [Fact]
        public void UpdateDraft_allowed_after_return()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R8", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            c.StartReview(DateTimeOffset.UtcNow.AddMinutes(3));
            c.ReturnForCorrection("Fix", DateTimeOffset.UtcNow.AddMinutes(4));

            var editTime = DateTimeOffset.UtcNow.AddMinutes(5);
            c.UpdateDraft("HPR2", 60m, null, null, editTime);

            Assert.Equal("HPR2", c.HprNumber);
            Assert.Equal(60m, c.EmploymentPercentage);
            Assert.Equal(editTime, c.LastModifiedAtUtc);
        }

        [Fact]
        public void Submit_allowed_after_return_with_validation()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-R9", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            c.StartReview(DateTimeOffset.UtcNow.AddMinutes(3));
            c.ReturnForCorrection("Fix", DateTimeOffset.UtcNow.AddMinutes(4));

            // still requires valid fields
            Assert.Throws<ArgumentException>(() => {
                // remove HPR
                c.UpdateDraft(null, 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(5));
                c.Submit(DateTimeOffset.UtcNow.AddMinutes(6));
            });

            // valid resubmit
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(7));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(8));

            Assert.Equal(GrantCaseStatus.Submitted, c.Status);
        }

        [Fact]
        public void ReturnForCorrection_trims_reason_and_sets_timestamps()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-RX1", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            var submitTime = DateTimeOffset.UtcNow.AddMinutes(2);
            c.Submit(submitTime);
            var reviewTime = submitTime.AddMinutes(3);
            c.StartReview(reviewTime);

            var reason = "  Needs trimming  ";
            var returnTime = reviewTime.AddMinutes(5);
            c.ReturnForCorrection(reason, returnTime);

            Assert.Equal(GrantCaseStatus.ReturnedForCorrection, c.Status);
            Assert.Equal("Needs trimming", c.ReturnReason);
            Assert.Equal(returnTime, c.ReturnedAtUtc);
            Assert.Equal(returnTime, c.LastModifiedAtUtc);
        }

        [Fact]
        public void ReturnForCorrection_allows_exactly_1000_chars()
        {
            var c = GrantCase.Create(Guid.NewGuid(), "CASE-RX2", DateTimeOffset.UtcNow);
            c.UpdateDraft("HPR1", 50m, new DateOnly(2026,1,1), new DateOnly(2026,6,1), DateTimeOffset.UtcNow.AddMinutes(1));
            c.Submit(DateTimeOffset.UtcNow.AddMinutes(2));
            c.StartReview(DateTimeOffset.UtcNow.AddMinutes(3));

            var longReason = new string('a', 1000);
            var returnTime = DateTimeOffset.UtcNow.AddMinutes(4);

            c.ReturnForCorrection(longReason, returnTime);

            Assert.Equal(GrantCaseStatus.ReturnedForCorrection, c.Status);
            Assert.Equal(longReason, c.ReturnReason);
            Assert.Equal(returnTime, c.ReturnedAtUtc);
            Assert.Equal(returnTime, c.LastModifiedAtUtc);
        }
    }
}