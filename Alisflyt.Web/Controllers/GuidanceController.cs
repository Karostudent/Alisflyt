using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Alisflyt.Infrastructure.Identity;
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
    [Authorize(Roles = "Veileder")]
    public class GuidanceController : Controller
    {
        private readonly IGrantCaseRepository _repository;
        private readonly UserManager<AlisflytUser> _userManager;

        public GuidanceController(
            IGrantCaseRepository repository,
            UserManager<AlisflytUser> userManager)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
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
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null || string.IsNullOrWhiteSpace(currentUser.HprNumber))
                return StatusCode(StatusCodes.Status403Forbidden);

            var cases = (await _repository.ListAsync(cancellationToken)).Where(c =>
                    !string.IsNullOrWhiteSpace(c.HprNumber)
                    && HasSupervisorHpr(c, currentUser.HprNumber))
                    .ToList();
            var assignedDoctors = cases
                .Select(GetDoctorName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
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
                Message = $"Veiledningsattester for {currentUser.UserName ?? "dine ALIS-leger"}.",
                CurrentUserName = currentUser.UserName ?? string.Empty,
                AssignedDoctors = assignedDoctors,
                PendingCount = pending,
                NeedsCorrectionCount = needsCorrection,
                ApprovedCount = approved,
                Cases = caseSummaries
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Applications(string status = "All", CancellationToken cancellationToken = default)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser is null || string.IsNullOrWhiteSpace(currentUser.HprNumber))
                return StatusCode(StatusCodes.Status403Forbidden);

            var cases = (await _repository.ListAsync(cancellationToken))
                .Where(c => c.Status != GrantCaseStatus.Draft && HasSupervisorHpr(c, currentUser.HprNumber))
                .Select(c => new Alisflyt.Application.Models.GrantCaseListItemDto
                {
                    Id = c.Id,
                    CaseNumber = c.CaseNumber,
                    HprNumber = c.HprNumber,
                    Status = c.Status,
                    LastModifiedAtUtc = c.LastModifiedAtUtc
                })
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

        private static bool HasSupervisorHpr(GrantCase grantCase, string supervisorHpr)
        {
            var application = grantCase.ApplicationDataJson is null
                ? null
                : JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson);
            return string.Equals(
                application?.Certificate?.SupervisorHprNumber,
                supervisorHpr,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDoctorName(GrantCase grantCase)
        {
            if (grantCase.ApplicationDataJson is null)
                return string.Empty;

            return JsonSerializer.Deserialize<GrantApplicationData>(grantCase.ApplicationDataJson)
                ?.DoctorName ?? string.Empty;
        }
    }
}
