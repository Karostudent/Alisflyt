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
        public async Task AlisController_Index_MapsCasesAndCalculatesStatusCounts()
        {
            var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
            var dto1 = new GrantCaseListItemDto { Id = Guid.Parse("88888888-8888-8888-8888-888888888888"), CaseNumber = "AC1", HprNumber = "AH1", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, LastModifiedAtUtc = now };
            var dto2 = new GrantCaseListItemDto { Id = Guid.Parse("99999999-9999-9999-9999-999999999999"), CaseNumber = "AC2", HprNumber = "AH2", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, LastModifiedAtUtc = now };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { dto1, dto2 } };
            var ctrl = new Alisflyt.Web.Controllers.AlisController(fake);

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
    }
}
