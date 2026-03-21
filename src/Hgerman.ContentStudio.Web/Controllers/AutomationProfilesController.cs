using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Web.Controllers;

public class AutomationProfilesController : Controller
{
    private readonly ContentStudioDbContext _db;
    private readonly IAutomationService _automationService;

    public AutomationProfilesController(
        ContentStudioDbContext db,
        IAutomationService automationService)
    {
        _db = db;
        _automationService = automationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var profiles = await _db.AutomationProfiles
            .OrderBy(x => x.AutomationProfileId)
            .ToListAsync(cancellationToken);

        var items = new List<AutomationProfileDashboardItemViewModel>();

        foreach (var profile in profiles)
        {
            var prefix = $"AUTO-{profile.AutomationProfileId}-";

            var todayCreatedCount = await _db.VideoJobs.CountAsync(
                x => x.JobNo.StartsWith(prefix) &&
                     x.CreatedDate.Date == todayUtc,
                cancellationToken);

            var completedUnpublishedCount = await _db.VideoJobs.CountAsync(
                x => x.JobNo.StartsWith(prefix) &&
                     x.Status == VideoJobStatus.Completed &&
                     !x.IsPublished,
                cancellationToken);

            items.Add(new AutomationProfileDashboardItemViewModel
            {
                AutomationProfileId = profile.AutomationProfileId,
                Name = profile.Name,
                IsActive = profile.IsActive,
                ProjectId = profile.ProjectId,
                LanguageCode = profile.LanguageCode,
                DailyVideoLimit = profile.DailyVideoLimit,
                PreferredHoursCsv = profile.PreferredHoursCsv,
                AutoPublishYouTube = profile.AutoPublishYouTube,
                LastRunAtUtc = profile.LastRunAtUtc,
                TodayCreatedCount = todayCreatedCount,
                CompletedUnpublishedCount = completedUnpublishedCount
            });
        }

        return View(new AutomationProfilesIndexViewModel
        {
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken cancellationToken)
    {
        var profile = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (profile != null)
        {
            profile.IsActive = !profile.IsActive;
            profile.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = $"Profile {(profile.IsActive ? "activated" : "paused")}.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id, CancellationToken cancellationToken)
    {
        var createdCount = await _automationService.RunProfileNowAsync(id, cancellationToken);
        TempData["SuccessMessage"] = $"{createdCount} job(s) created for selected profile.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAllNow(CancellationToken cancellationToken)
    {
        var createdCount = await _automationService.RunAllActiveNowAsync(cancellationToken);
        TempData["SuccessMessage"] = $"{createdCount} automation job(s) created for all active profiles.";
        return RedirectToAction(nameof(Index));
    }
}