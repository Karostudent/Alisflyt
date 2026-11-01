using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class AlisControllerTests
    {
        [Fact]
        public async Task AlisController_Index_filters_cases_to_current_alis()
        {
            var own = new GrantCaseListItemDto { Id = Guid.NewGuid(), CaseNumber = "OWN", HprNumber = "123456789" };
            var other = new GrantCaseListItemDto { Id = Guid.NewGuid(), CaseNumber = "OTHER", HprNumber = "999999999" };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { own, other } };
            var ctrl = new Alisflyt.Web.Controllers.AlisController(fake)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
            };

            var result = await ctrl.Index(default);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.AlisDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);

            var item = Assert.Single(model.Cases);
            Assert.Equal(own.Id, item.Id);
        }

        [Fact]
        public async Task AlisController_Index_rejects_anonymous_before_list_access()
        {
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.AlisController(fake);

            var result = await ctrl.Index(default);

            Assert.Equal(Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
            Assert.Empty(fake.ListCalls);
        }

        [Fact]
        public async Task AlisController_Index_MapsCasesAndCalculatesStatusCounts()
        {
            var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
            var dto1 = new GrantCaseListItemDto { Id = Guid.Parse("88888888-8888-8888-8888-888888888888"), CaseNumber = "AC1", HprNumber = "123456789", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, LastModifiedAtUtc = now };
            var dto2 = new GrantCaseListItemDto { Id = Guid.Parse("99999999-9999-9999-9999-999999999999"), CaseNumber = "AC2", HprNumber = "123456789", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, LastModifiedAtUtc = now };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { dto1, dto2 } };
            var ctrl = new Alisflyt.Web.Controllers.AlisController(fake)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
            };

            var result = await ctrl.Index(default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.AlisDashboardViewModel>(view.Model);
            Assert.Equal(2, ((System.Collections.ICollection)model.Cases).Count);
            Assert.Equal(1, model.DraftCount);
            Assert.Equal(1, model.SubmittedCount);
            Assert.Contains(model.Cases, c => c.Id == dto1.Id && c.CaseNumber == dto1.CaseNumber && c.HprNumber == dto1.HprNumber && c.Status == dto1.Status && c.LastModifiedAtUtc == dto1.LastModifiedAtUtc);
            Assert.Contains(model.Cases, c => c.Id == dto2.Id && c.CaseNumber == dto2.CaseNumber && c.HprNumber == dto2.HprNumber && c.Status == dto2.Status && c.LastModifiedAtUtc == dto2.LastModifiedAtUtc);
            Assert.Single(fake.ListCalls);
        }

        [Theory]
        [InlineData(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, Alisflyt.Domain.Enums.GuidanceApprovalStatus.NeedsCorrection, true)]
        [InlineData(Alisflyt.Domain.Enums.GrantCaseStatus.UnderReview, Alisflyt.Domain.Enums.GuidanceApprovalStatus.NeedsCorrection, true)]
        [InlineData(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending, false)]
        [InlineData(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, Alisflyt.Domain.Enums.GuidanceApprovalStatus.Approved, false)]
        [InlineData(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, Alisflyt.Domain.Enums.GuidanceApprovalStatus.Superseded, false)]
        [InlineData(Alisflyt.Domain.Enums.GrantCaseStatus.AwaitingAuditorApproval, Alisflyt.Domain.Enums.GuidanceApprovalStatus.NeedsCorrection, false)]
        public async Task AlisController_Index_exposes_correction_only_when_current_approval_requires_it(Alisflyt.Domain.Enums.GrantCaseStatus status, Alisflyt.Domain.Enums.GuidanceApprovalStatus approvalStatus, bool expected)
        {
            var dto = new GrantCaseListItemDto
            {
                Id = Guid.NewGuid(),
                CaseNumber = "CORR-1",
                HprNumber = "123456789",
                Status = status,
                GuidanceApprovalStatus = approvalStatus,
                GuidanceCorrectionReason = expected ? "Timer stemmer ikke." : null
            };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { dto } };
            var ctrl = new Alisflyt.Web.Controllers.AlisController(fake)
            {
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
            };

            var result = await ctrl.Index(default);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.AlisDashboardViewModel>(Assert.IsType<ViewResult>(result).Model);
            var item = Assert.Single(model.Cases);

            Assert.Equal(expected, item.NeedsGuidanceCorrection);
            Assert.Equal(expected ? "Timer stemmer ikke." : null, item.GuidanceCorrectionReason);
        }
    }
}
