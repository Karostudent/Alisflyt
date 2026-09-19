using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Application.Services;
using Alisflyt.Web.ViewModels.Dashboards;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.Controllers
{
    public class AlisController : Controller
    {
        private readonly IGrantCaseApplicationService _svc;

        public AlisController(IGrantCaseApplicationService svc)
        {
            _svc = svc;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var list = await _svc.ListAsync(cancellationToken).ConfigureAwait(false);
            var vm = new AlisDashboardViewModel
            {
                Cases = list.Select(c => new GrantCaseListItemViewModel
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                }).ToList()
            };

            return View(vm);
        }

        // ALIS uses the existing GrantCases Details view via GrantCasesController; keep AlisController focused on index only.
    }
}
