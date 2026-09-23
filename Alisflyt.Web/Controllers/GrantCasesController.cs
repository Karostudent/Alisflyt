using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Application.Services;
using Alisflyt.Application.Models;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.Controllers
{
    public class GrantCasesController : Controller
    {
        private readonly IGrantCaseApplicationService _svc;

        public GrantCasesController(IGrantCaseApplicationService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var list = await _svc.ListAsync(cancellationToken).ConfigureAwait(false);
            var vm = list.Select(d => new GrantCaseListItemViewModel
            {
                Id = d.Id,
                CaseNumber = d.CaseNumber,
                HprNumber = d.HprNumber,
                Status = d.Status,
                LastModifiedAtUtc = d.LastModifiedAtUtc
            }).ToList();
            return View(vm);
        }

        public IActionResult Create()
        {
            return View(new CreateGrantCaseViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1_048_576)]
        public async Task<IActionResult> SaveApplication([FromBody] GrantApplicationFormViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Kontroller tall og datoer før du lagrer." });
            try
            {
                var saved = model.Id == Guid.Empty
                    ? await _svc.CreateDraftAsync(new CreateGrantCaseRequest { ApplicationData = model }, cancellationToken)
                    : await _svc.UpdateDraftAsync(model.Id, new UpdateGrantCaseDraftRequest { ApplicationData = model }, cancellationToken);
                return Ok(new { id = saved.Id, message = "Utkastet er lagret.",
                    editUrl = Url.Action(nameof(Edit), new { id = saved.Id }),
                    detailsUrl = Url.Action(nameof(Details), new { id = saved.Id }) });
            }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Søknaden finnes ikke lenger." }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException) { return Conflict(new { message = "Søknaden kan ikke redigeres i nåværende status." }); }
        }

        private static T FormModel<T>(GrantCaseDto data) where T : GrantApplicationFormViewModel, new()
        {
            var form = data.ApplicationData ?? new Alisflyt.Domain.Forms.GrantApplicationData
            {
                HprNumber = data.HprNumber,
                EmploymentPeriods = data.EmploymentPercentage.HasValue || data.EmploymentStartDate.HasValue || data.EmploymentEndDate.HasValue
                    ? [new() { PositionPercentage = data.EmploymentPercentage, EmploymentStartDate = data.EmploymentStartDate,
                        FundingFrom = data.EmploymentStartDate, FundingThrough = data.EmploymentEndDate }]
                    : []
            };
            var model = System.Text.Json.JsonSerializer.Deserialize<T>(System.Text.Json.JsonSerializer.Serialize(form))!;
            model.Id = data.Id;
            return model;
        }

        [HttpGet]
        public async Task<IActionResult> Application(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var data = await _svc.GetByIdAsync(id, cancellationToken);
                var model = FormModel<GrantApplicationFormViewModel>(data);
                model.ReadOnly = true;
                return View(model);
            }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGrantCaseViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(model);

            var req = new CreateGrantCaseRequest
            {
                HprNumber = model.HprNumber,
                EmploymentPercentage = model.EmploymentPercentage,
                EmploymentStartDate = model.EmploymentStartDate,
                EmploymentEndDate = model.EmploymentEndDate
            };

            var created = await _svc.CreateDraftAsync(req, cancellationToken).ConfigureAwait(false);

            return RedirectToAction(nameof(Details), new { id = created.Id });
        }

        public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        {
            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }

            var vm = new GrantCaseDetailsViewModel
            {
                Id = d.Id,
                CaseNumber = d.CaseNumber,
                HprNumber = d.HprNumber,
                EmploymentPercentage = d.EmploymentPercentage,
                EmploymentStartDate = d.EmploymentStartDate,
                EmploymentEndDate = d.EmploymentEndDate,
                Status = d.Status,
                CreatedAtUtc = d.CreatedAtUtc,
                LastModifiedAtUtc = d.LastModifiedAtUtc,
                ReturnReason = d.ReturnReason,
                ReturnedAtUtc = d.ReturnedAtUtc
            };

            // Return explicit Details view so MVC does not search for a Submit.cshtml view.
            return View("Details", vm);
        }

        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }

            if (d.Status != Domain.Enums.GrantCaseStatus.Draft && d.Status != Domain.Enums.GrantCaseStatus.ReturnedForCorrection)
                return BadRequest();

            var vm = FormModel<EditGrantCaseViewModel>(d);
            vm.EmploymentPercentage = d.EmploymentPercentage;
            vm.EmploymentStartDate = d.EmploymentStartDate;
            vm.EmploymentEndDate = d.EmploymentEndDate;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditGrantCaseViewModel model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return View(model);

            var req = new UpdateGrantCaseDraftRequest
            {
                HprNumber = model.HprNumber,
                EmploymentPercentage = model.EmploymentPercentage,
                EmploymentStartDate = model.EmploymentStartDate,
                EmploymentEndDate = model.EmploymentEndDate
            };

            Alisflyt.Application.Models.GrantCaseDto updated;
            try
            {
                updated = await _svc.UpdateDraftAsync(model.Id, req, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Details), new { id = updated.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _svc.SubmitAsync(id, cancellationToken).ConfigureAwait(false);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Alisflyt.Domain.Forms.SubmissionValidationException ex)
            {
                foreach (var error in ex.Errors)
                    ModelState.AddModelError(string.Empty, error);
            }
            catch (ArgumentOutOfRangeException)
            {
                ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn: Stillingsprosent må være større enn 0 og høyst 100.");
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn: " + ex.Message);
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn i nåværende status. Bare utkast og søknader returnert for korrigering kan sendes inn.");
            }

            Alisflyt.Application.Models.GrantCaseDto d;
            try
            {
                d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }

            var vm = new GrantCaseDetailsViewModel
            {
                Id = d.Id,
                CaseNumber = d.CaseNumber,
                HprNumber = d.HprNumber,
                EmploymentPercentage = d.EmploymentPercentage,
                EmploymentStartDate = d.EmploymentStartDate,
                EmploymentEndDate = d.EmploymentEndDate,
                Status = d.Status,
                CreatedAtUtc = d.CreatedAtUtc,
                LastModifiedAtUtc = d.LastModifiedAtUtc
            };

            // Return explicit Details view so MVC does not search for a Submit.cshtml view.
            return View("Details", vm);
        }
    }
}
