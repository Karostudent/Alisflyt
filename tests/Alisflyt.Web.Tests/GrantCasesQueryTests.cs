using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesQueryTests
    {
        [Fact]
        public async Task GrantCasesController_Index_MapsDtosToListViewModels()
        {
            var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
            var dto1 = new GrantCaseListItemDto { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), CaseNumber = "CN1", HprNumber = "H1", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, LastModifiedAtUtc = now };
            var dto2 = new GrantCaseListItemDto { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), CaseNumber = "CN2", HprNumber = "H2", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, LastModifiedAtUtc = now };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { dto1, dto2 } };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Index(default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<List<Alisflyt.Web.ViewModels.GrantCases.GrantCaseListItemViewModel>>(view.Model);
            Assert.Equal(2, model.Count);
            Assert.Contains(model, m => m.Id == dto1.Id && m.CaseNumber == dto1.CaseNumber && m.HprNumber == dto1.HprNumber && m.Status == dto1.Status && m.LastModifiedAtUtc == dto1.LastModifiedAtUtc);
            Assert.Contains(model, m => m.Id == dto2.Id && m.CaseNumber == dto2.CaseNumber && m.HprNumber == dto2.HprNumber && m.Status == dto2.Status && m.LastModifiedAtUtc == dto2.LastModifiedAtUtc);
            Assert.Single(fake.ListCalls);
        }

        [Fact]
        public async Task GrantCasesController_Details_MapsDtoToDetailsViewModel()
        {
            var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var dto = new GrantCaseDto { Id = id, CaseNumber = "CN3", HprNumber = "H3", EmploymentPercentage = 50m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,12,31), Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"), LastModifiedAtUtc = DateTimeOffset.Parse("2026-01-02T00:00:00Z") };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Details(id, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Details", view.ViewName);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.GrantCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
            Assert.Equal(dto.CaseNumber, model.CaseNumber);
            Assert.Equal(dto.HprNumber, model.HprNumber);
            Assert.Equal(dto.EmploymentPercentage, model.EmploymentPercentage);
            Assert.Equal(dto.EmploymentStartDate, model.EmploymentStartDate);
            Assert.Equal(dto.EmploymentEndDate, model.EmploymentEndDate);
            Assert.Equal(dto.Status, model.Status);
            Assert.Equal(dto.CreatedAtUtc, model.CreatedAtUtc);
            Assert.Equal(dto.LastModifiedAtUtc, model.LastModifiedAtUtc);
            Assert.Single(fake.GetByIdCalls);
            Assert.Equal(id, fake.GetByIdCalls[0].id);
        }
    }
}
