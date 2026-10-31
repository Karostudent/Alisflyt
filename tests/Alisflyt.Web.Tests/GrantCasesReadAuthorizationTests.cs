using System;
using System.Threading.Tasks;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.Controllers;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests;

public sealed class GrantCasesReadAuthorizationTests
{
    [Fact]
    public async Task DownloadAttachment_allows_owner_to_download_attachment()
    {
        var attachmentId = Guid.NewGuid();
        var fake = new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto
            {
                HprNumber = "123456789",
                ApplicationData = new GrantApplicationData
                {
                    Attachments =
                    [
                        new ApplicationAttachment
                        {
                            Id = attachmentId,
                            FileName = "kvittering.pdf",
                            ContentBase64 = Convert.ToBase64String([1, 2, 3])
                        }
                    ]
                }
            }
        };
        var controller = new GrantCasesController(fake)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
        };

        var result = await controller.DownloadAttachment(Guid.NewGuid(), attachmentId, default);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
    }

    [Fact]
    public async Task DownloadAttachment_rejects_anonymous_before_case_lookup()
    {
        var fake = new FakeGrantCaseApplicationService();
        var controller = new GrantCasesController(fake)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.DownloadAttachment(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Empty(fake.GetByIdCalls);
    }

    [Fact]
    public async Task DownloadAttachment_allows_assigned_supervisor_to_download_attachment()
    {
        var attachmentId = Guid.NewGuid();
        var fake = new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto
            {
                HprNumber = "123456789",
                ApplicationData = new GrantApplicationData
                {
                    Attachments = [new ApplicationAttachment
                    {
                        Id = attachmentId,
                        FileName = "kvittering.pdf",
                        ContentBase64 = Convert.ToBase64String([4, 5, 6])
                    }]
                }
            }
        };
        var controller = new GrantCasesController(fake)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateSupervisor() }
        };

        var result = await controller.DownloadAttachment(Guid.NewGuid(), attachmentId, default);

        Assert.IsType<FileContentResult>(result);
    }

    [Fact]
    public async Task DownloadAttachment_rejects_unassigned_supervisor_without_returning_file_content()
    {
        var attachmentId = Guid.NewGuid();
        var fake = new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto
            {
                HprNumber = "999999999",
                ApplicationData = new GrantApplicationData
                {
                    Attachments = [new ApplicationAttachment
                    {
                        Id = attachmentId,
                        FileName = "secret.pdf",
                        ContentBase64 = Convert.ToBase64String([9, 8, 7])
                    }]
                }
            }
        };
        var controller = new GrantCasesController(fake)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateSupervisor() }
        };

        var result = await controller.DownloadAttachment(Guid.NewGuid(), attachmentId, default);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task DownloadAttachment_rejects_another_alis_without_returning_file_content()
    {
        var attachmentId = Guid.NewGuid();
        var fake = new FakeGrantCaseApplicationService
        {
            GetByIdResponse = new GrantCaseDto
            {
                HprNumber = "999999999",
                ApplicationData = new GrantApplicationData
                {
                    Attachments =
                    [new ApplicationAttachment
                    {
                        Id = attachmentId,
                        FileName = "secret.pdf",
                        ContentBase64 = Convert.ToBase64String([9, 8, 7])
                    }]
                }
            }
        };
        var controller = new GrantCasesController(fake)
        {
            ControllerContext = new ControllerContext { HttpContext = GrantCasesTestContext.CreateAlis() }
        };

        var result = await controller.DownloadAttachment(Guid.NewGuid(), attachmentId, default);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }
}
