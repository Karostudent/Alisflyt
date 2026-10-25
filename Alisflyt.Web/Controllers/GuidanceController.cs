using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Alisflyt.Application.Abstractions;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Enums;
using Alisflyt.Domain.Forms;
using Alisflyt.Web.Models;
using Alisflyt.Web.ViewModels.Dashboards;
using Alisflyt.Web.ViewModels.GrantCases;

namespace Alisflyt.Web.Controllers
{
    public class GuidanceController : Controller
    {
        private readonly IGrantCaseApplicationService _svc;
        private readonly IGrantCaseRepository _repository;

        public GuidanceController(IGrantCaseApplicationService svc, IGrantCaseRepository repository)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        private static bool HasRelevantGuidanceSessions(GrantCase grantCase)
        {
            if (grantCase.ApplicationDataJson is null)
                return false;

            var app = JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson);
            var sessions = app?.Certificate?.Sessions ?? [];
            return sessions.Any(session => session.Hours is > 0m);
        }

        private static string GetGuidanceStatusLabel(GrantCase grantCase, GuidanceApproval? approval)
        {
            if (approval is not null)
            {
                return approval.Status switch
                {
                    GuidanceApprovalStatus.Pending => "Avventer godkjenning",
                    GuidanceApprovalStatus.NeedsCorrection => "Korrigering nødvendig",
                    GuidanceApprovalStatus.Approved => "Godkjent",
                    GuidanceApprovalStatus.Superseded => "Erstattet",
                    _ => approval.Status.ToString()
                };
            }

            var hasSessions = HasRelevantGuidanceSessions(grantCase);
            if (!hasSessions)
                return "Godkjenning ikke påkrevd";

            if (grantCase.Status is GrantCaseStatus.Draft or GrantCaseStatus.ReturnedForCorrection)
                return "Ikke sendt til godkjenning";

            return "Mangler aktiv godkjenningsforespørsel";
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
        {
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase)))
                return Forbid();

            var assignedDoctors = DemoAuthentication.GetAssignedDoctorUserKeys(currentUser?.Key)
                .Select(key => DemoAuthentication.DemoUsers.FirstOrDefault(u => string.Equals(u.Key, key, StringComparison.OrdinalIgnoreCase)))
                .Where(u => u is not null)
                .Select(u => u!.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cases = (await _repository.ListAsync(cancellationToken)).Where(c =>
                    !string.IsNullOrWhiteSpace(c.HprNumber)
                    && DemoAuthentication.GetAssignedDoctorHprNumbers(currentUser?.Key)
                        .Contains(c.HprNumber, StringComparer.OrdinalIgnoreCase))
                    .ToList();

            var pending = 0;
            var needsCorrection = 0;
            var approved = 0;
            var caseSummaries = new List<GuidanceDashboardCaseViewModel>();

            foreach (var grantCase in cases.OrderByDescending(c => c.LastModifiedAtUtc))
            {
                var approval = grantCase.GetCurrentGuidanceApproval();
                var hasSessions = HasRelevantGuidanceSessions(grantCase);
                var shouldBeIncluded = hasSessions && grantCase.Status is not GrantCaseStatus.Draft;

                if (!shouldBeIncluded && approval is null)
                    continue;

                if (approval is not null)
                {
                    switch (approval.Status)
                    {
                        case GuidanceApprovalStatus.Pending:
                            pending++;
                            break;
                        case GuidanceApprovalStatus.NeedsCorrection:
                            needsCorrection++;
                            break;
                        case GuidanceApprovalStatus.Approved:
                            approved++;
                            break;
                    }
                }

                var app = grantCase.ApplicationDataJson is null
                    ? null
                    : JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson);
                var certificate = app?.Certificate;
                var sessions = certificate?.Sessions ?? [];
                var totalHours = sessions.Sum(session => session.Hours ?? 0m);
                var dates = sessions.Where(s => s.Date.HasValue).Select(s => s.Date!.Value).OrderBy(d => d).ToList();
                var periodText = dates.Count == 0 ? "-" : $"{dates.First():dd.MM.yyyy} - {dates.Last():dd.MM.yyyy}";

                caseSummaries.Add(new GuidanceDashboardCaseViewModel
                {
                    Id = grantCase.Id,
                    CaseNumber = grantCase.CaseNumber,
                    DoctorName = certificate?.DoctorName ?? string.Empty,
                    HprNumber = grantCase.HprNumber ?? string.Empty,
                    Status = approval?.Status.ToString() ?? "Missing",
                    StatusText = GetGuidanceStatusLabel(grantCase, approval),
                    PeriodText = periodText,
                    TotalHours = totalHours,
                    LastModifiedAtUtc = grantCase.LastModifiedAtUtc
                });
            }

            var vm = new GuidanceDashboardViewModel
            {
                Message = $"Veiledningsattester for {currentUser?.Name ?? "dine ALIS-leger"}.",
                CurrentUserName = currentUser?.Name ?? string.Empty,
                AssignedDoctors = assignedDoctors,
                PendingCount = pending,
                NeedsCorrectionCount = needsCorrection,
                ApprovedCount = approved,
                Cases = caseSummaries
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Applications([FromServices] IGrantCaseApplicationService service, string status = "All", CancellationToken cancellationToken = default)
        {
            var currentUser = DemoAuthentication.GetCurrentUser(HttpContext);
            if (HttpContext is not null && (currentUser is null || !string.Equals(currentUser.Role, "Veileder", StringComparison.OrdinalIgnoreCase)))
                return Forbid();

            var allowedDoctors = new HashSet<string>(DemoAuthentication.GetAssignedDoctorHprNumbers(currentUser?.Key), StringComparer.OrdinalIgnoreCase);
            var cases = (await service.ListAsync(cancellationToken))
                .Where(c => allowedDoctors.Contains(c.HprNumber ?? string.Empty))
                .ToList();

            if (!new[] { "All", "Draft", "Submitted", "UnderReview", "ReturnedForCorrection" }.Contains(status)) status = "All";
            return View(new GuidanceApplicationsViewModel
            {
                Filter = status,
                Cases = cases.Where(c => status == "All" || c.Status.ToString() == status)
                    .OrderByDescending(c => c.LastModifiedAtUtc)
                    .Select(c => new GrantCaseListItemViewModel
                    {
                        Id = c.Id,
                        CaseNumber = c.CaseNumber,
                        HprNumber = c.HprNumber,
                        Status = c.Status,
                        LastModifiedAtUtc = c.LastModifiedAtUtc
                    }).ToList()
            });
        }
    }
}
