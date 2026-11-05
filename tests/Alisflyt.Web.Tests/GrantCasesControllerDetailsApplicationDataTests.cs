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
        public void Guidance_status_is_displayed_when_guidance_data_exists_even_if_required_flag_is_false()
        {
            var model = new GrantCaseDetailsViewModel
            {
                Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                GuidanceApprovalRequired = false,
                GuidanceApprovalStatus = Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel()]
                    }
                }
            };

            Assert.True(model.HasGuidanceForDisplay);
        }

        [Fact]
        public void Draft_with_guidance_sessions_does_not_show_guidance_status()
        {
            var model = new GrantCaseDetailsViewModel
            {
                Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft,
                GuidanceApprovalStatus = Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel()]
                    }
                }
            };

            Assert.False(model.HasGuidanceForDisplay);
        }

        [Fact]
        public void Submitted_with_guidance_sessions_but_no_current_approval_does_not_show_status()
        {
            var model = new GrantCaseDetailsViewModel
            {
                Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel()]
                    }
                }
            };

            Assert.False(model.HasGuidanceForDisplay);
        }

        [Fact]
        public void Superseded_approval_does_not_show_guidance_status()
        {
            var model = new GrantCaseDetailsViewModel
            {
                Status = Alisflyt.Domain.Enums.GrantCaseStatus.Submitted,
                GuidanceApprovalStatus = Alisflyt.Domain.Enums.GuidanceApprovalStatus.Superseded,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData
                {
                    Certificate = new Alisflyt.Domain.Forms.SupervisionCertificateViewModel
                    {
                        Sessions = [new Alisflyt.Domain.Forms.SupervisionSessionInputModel()]
                    }
                }
            };

            Assert.False(model.HasGuidanceForDisplay);
        }

        [Fact]
        public void Guidance_status_is_not_required_when_no_guidance_data_or_approval_exists()
        {
            var model = new GrantCaseDetailsViewModel();

            Assert.False(model.HasGuidanceForDisplay);
        }

        [Fact]
        public void Guidance_status_stays_hidden_when_approval_is_required_but_no_sessions_exist()
        {
            var model = new GrantCaseDetailsViewModel
            {
                GuidanceApprovalRequired = true,
                GuidanceApprovalStatus = Alisflyt.Domain.Enums.GuidanceApprovalStatus.Pending,
                ApplicationData = new Alisflyt.Domain.Forms.GrantApplicationData()
            };

            Assert.False(model.HasGuidanceForDisplay);
        }

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

            var controller = new GrantCasesController(svc)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = GrantCasesTestContext.CreateAlis("123")
                }
            };

            var result = await controller.Details(dto.Id, default);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GrantCaseDetailsViewModel>(view.Model);
            Assert.NotNull(model.ApplicationData);
            Assert.Equal("Dr Test", model.ApplicationData.DoctorName);
        }
    }
}
