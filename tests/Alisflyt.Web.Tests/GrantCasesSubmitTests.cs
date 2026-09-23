using System;
using System.Threading.Tasks;
using Alisflyt.Web.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Alisflyt.Web.Tests
{
    public class GrantCasesSubmitTests
    {
        [Fact]
        public async Task Submission_lists_each_missing_field_without_generic_duplicate()
        {
            var errors = new[] { "Oppgi HPR-nummer for legen.", "Stilling i kommunen, rad 2: Oppgi stillingsprosent." };
            var fake = new FakeGrantCaseApplicationService
            {
                SubmitException = new Alisflyt.Domain.Forms.SubmissionValidationException(errors),
                GetByIdResponse = new Alisflyt.Application.Models.GrantCaseDto { Id = Guid.NewGuid() }
            };
            var controller = new Alisflyt.Web.Controllers.GrantCasesController(fake);
            var result = Assert.IsType<ViewResult>(await controller.Submit(fake.GetByIdResponse.Id, default));
            Assert.Equal("Details", result.ViewName);
            Assert.Equal(errors, controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        }

        [Fact]
        public async Task GrantCasesController_Submit_RedirectsToDetails_When_SubmissionSucceeds()
        {
            var id = Guid.NewGuid();
            var fake = new FakeGrantCaseApplicationService();
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Submit(id, default);

            Assert.IsType<RedirectToActionResult>(result);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(redirect.RouteValues);
            Assert.Equal(id, redirect.RouteValues!["id"]);
            Assert.Single(fake.SubmitCalls);
            Assert.Equal(id, fake.SubmitCalls[0].id);
        }

        [Fact]
        public async Task GrantCasesController_Submit_ReturnsDetailsWithNorwegianError_When_ArgumentExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, CaseNumber = "CN-1", HprNumber = "HPR1", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { SubmitException = new ArgumentException(), GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Submit(id, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Details", view.ViewName);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.GrantCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
            Assert.True(ctrl.ModelState.ErrorCount > 0);
            Assert.Contains(ctrl.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("Søknaden kan ikke sendes inn"));
            Assert.Single(fake.GetByIdCalls);
            Assert.Equal(id, fake.GetByIdCalls[0].id);
        }

        [Fact]
        public async Task GrantCasesController_Submit_ReturnsDetailsWithNorwegianError_When_ArgumentOutOfRangeExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, CaseNumber = "CN-2", HprNumber = "HPR2", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { SubmitException = new ArgumentOutOfRangeException(), GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Submit(id, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Details", view.ViewName);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.GrantCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
            Assert.True(ctrl.ModelState.ErrorCount > 0);
            Assert.Contains(ctrl.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("Søknaden kan ikke sendes inn"));
            Assert.Single(fake.GetByIdCalls);
            Assert.Equal(id, fake.GetByIdCalls[0].id);
        }

        [Fact]
        public async Task GrantCasesController_Submit_ReturnsDetailsWithNorwegianError_When_InvalidOperationExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var dto = new Alisflyt.Application.Models.GrantCaseDto { Id = id, CaseNumber = "CN-3", HprNumber = "HPR3", Status = Alisflyt.Domain.Enums.GrantCaseStatus.Draft, CreatedAtUtc = DateTimeOffset.UtcNow, LastModifiedAtUtc = DateTimeOffset.UtcNow };
            var fake = new FakeGrantCaseApplicationService { SubmitException = new InvalidOperationException(), GetByIdResponse = dto };
            var ctrl = new Alisflyt.Web.Controllers.GrantCasesController(fake);

            var result = await ctrl.Submit(id, default);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Details", view.ViewName);
            var model = Assert.IsType<Alisflyt.Web.ViewModels.GrantCases.GrantCaseDetailsViewModel>(view.Model);
            Assert.Equal(dto.Id, model.Id);
            Assert.True(ctrl.ModelState.ErrorCount > 0);
            Assert.Contains(ctrl.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("Søknaden kan ikke sendes inn"));
            Assert.Single(fake.GetByIdCalls);
            Assert.Equal(id, fake.GetByIdCalls[0].id);
        }
    }
}
