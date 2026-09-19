using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class CoordinatorDashboardTests
    {
        [Fact]
        public async Task CoordinatorController_Index_ReturnsAllCases_WhenFilterIsAll()
        {
            var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
            var a = new GrantCaseListItemDto { Id = Guid.NewGuid(), CaseNumber = "C1", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, LastModifiedAtUtc = now };
            var b = new GrantCaseListItemDto { Id = Guid.NewGuid(), CaseNumber = "C2", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, LastModifiedAtUtc = now };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { a, b } };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.Index("All", default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.CoordinatorDashboardViewModel>(view.Model);
            Assert.Equal("All", model.Filter);
            Assert.Equal(2, ((System.Collections.ICollection)model.Cases).Count);
            Assert.Single(fake.ListCalls);
        }

        [Fact]
        public async Task CoordinatorController_Index_ReturnsOnlySubmittedCases_WhenFilterIsSubmitted()
        {
            var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
            var a = new GrantCaseListItemDto { Id = Guid.NewGuid(), CaseNumber = "C3", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, LastModifiedAtUtc = now };
            var b = new GrantCaseListItemDto { Id = Guid.NewGuid(), CaseNumber = "C4", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, LastModifiedAtUtc = now };
            var fake = new FakeGrantCaseApplicationService { ListResponse = new List<GrantCaseListItemDto> { a, b } };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.Index("Submitted", default);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Dashboards.CoordinatorDashboardViewModel>(view.Model);
            Assert.Equal("Submitted", model.Filter);
            Assert.Equal(1, ((System.Collections.ICollection)model.Cases).Count);
            Assert.All(model.Cases, c => Assert.Equal(Alisflyt.Domain.Enums.GrantCaseStatus.Submitted, c.Status));
            Assert.Single(fake.ListCalls);
        }

        [Fact]
        public async Task CoordinatorController_Details_MapsDtoToReadOnlyDetailsViewModel()
        {
            var id = Guid.Parse("aaaaaaaa-aaaa-4444-aaaa-aaaaaaaaaaaa");
            var dto = new GrantCaseDto { Id = id, CaseNumber = "CD1", HprNumber = "HCD", EmploymentPercentage = 10m, EmploymentStartDate = new DateOnly(2026,1,1), EmploymentEndDate = new DateOnly(2026,12,31), Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.CoordinatorController(fake);

            var result = await ctrl.Details(id, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("~/Views/Coordinator/Details.cshtml", view.ViewName);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.Coordinator.CoordinatorCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
            Assert.Equal(dto.CaseNumber, model.CaseNumber);
            Assert.Equal(dto.HprNumber, model.HprNumber);
            Assert.Equal(dto.EmploymentPercentage, model.EmploymentPercentage);
            Assert.Equal(dto.EmploymentStartDate, model.EmploymentStartDate);
            Assert.Equal(dto.EmploymentEndDate, model.EmploymentEndDate);
            Assert.Equal(dto.Status, model.Status);
            Assert.Single(fake.GetByIdCalls);
            Assert.Equal(id, fake.GetByIdCalls[0].id);
        }
    }
}
