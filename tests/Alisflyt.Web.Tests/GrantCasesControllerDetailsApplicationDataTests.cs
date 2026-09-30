using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.ViewModels.GrantCases;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesControllerDetailsApplicationDataTests
    {
        [Fact]
        public async Task Details_Includes_ApplicationData_In_ViewModel()
        {
            var svc = new FakeGrantCaseApplicationService();
            var dto = new GrantCaseDto
            {
                Id = Guid.NewGuid(),
                CaseNumber = "CASE-1",
                HprNumber = "123",
                Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastModifiedAtUtc = DateTimeOffset.UtcNow,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    DoctorName = "Dr Test"
                }
            };
            svc.GetByIdResponse = dto;

            var controller = new GrantCasesController(svc);

            var result = await controller.Details(dto.Id, default);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GrantCaseDetailsViewModel>(view.Model);
            Assert.NotNull(model.ApplicationData);
            Assert.Equal("Dr Test", model.ApplicationData.DoctorName);
        }
    }
}
