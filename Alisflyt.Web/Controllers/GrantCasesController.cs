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
            var d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (d is null)
                return NotFound();

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

        public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
        {
            var d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (d is null)
                return NotFound();

            if (d.Status != Domain.Enums.GrantCaseStatus.Draft)
                return BadRequest();

            var vm = new EditGrantCaseViewModel
            {
                Id = d.Id,
                HprNumber = d.HprNumber,
                EmploymentPercentage = d.EmploymentPercentage,
                EmploymentStartDate = d.EmploymentStartDate,
                EmploymentEndDate = d.EmploymentEndDate
            };

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

            var updated = await _svc.UpdateDraftAsync(model.Id, req, cancellationToken).ConfigureAwait(false);

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
            catch (ArgumentOutOfRangeException)
            {
                // Domain reported out of range values
            }
            catch (ArgumentException)
            {
                // Domain validation failed for submission (incomplete or invalid draft)
            }
            catch (InvalidOperationException)
            {
                // Domain reported operation not allowed (e.g. not a draft)
            }

            // On validation-related failure, show a friendly Norwegian message and render Details with ModelState errors.
            ModelState.AddModelError(string.Empty, "Søknaden kan ikke sendes inn. Kontroller at HPR-nummer, stillingsprosent og ansettelsesperiode er fylt ut riktig.");

            var d = await _svc.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (d is null)
                return NotFound();

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

            return View(vm);
        }
    }
}
