using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.DTOs;
using Hgerman.ContentStudio.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Web.Controllers;

public class AutomationProfilesController : Controller
{
    private readonly ContentStudioDbContext _db;

    public AutomationProfilesController(ContentStudioDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var profiles = await _db.AutomationProfiles
            .AsNoTracking()
            .OrderByDescending(x => x.AutomationProfileId)
            .Select(x => new AutomationProfileListItemDto
            {
                AutomationProfileId = x.AutomationProfileId,
                Name = x.Name,
                IsActive = x.IsActive,
                LanguageCode = x.LanguageCode,
                GrowthMode = x.GrowthMode,
                DailyVideoLimit = x.DailyVideoLimit,
                PreferredHoursCsv = x.PreferredHoursCsv,
                LastRunAtUtc = x.LastRunAtUtc
            })
            .ToListAsync(cancellationToken);

        return View(profiles);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var profile = await _db.AutomationProfiles
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.VideoJobs)
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (profile == null)
        {
            return NotFound();
        }

        profile.VideoJobs = profile.VideoJobs
            .OrderByDescending(x => x.VideoJobId)
            .Take(20)
            .ToList();

        return View(profile);
    }

    [HttpGet]
    public IActionResult Create()
    {
        PopulateSelections();

        var model = new AutomationProfileEditViewModel
        {
            ProjectId = 1,
            Name = "New Automation Profile",
            IsActive = true,
            LanguageCode = "en",
            PlatformType = 1,
            ToneType = 1,
            DurationTargetSec = 20,
            AspectRatio = 1,
            SubtitleEnabled = true,
            ThumbnailEnabled = true,
            DailyVideoLimit = 1,
            PreferredHoursCsv = "09",
            TopicPrompt = "Create short motivational videos about discipline and success mindset.",
            HookTemplate = "Start with a strong hook.",
            ViralPatternTemplate = "Hook → Tension → Payoff → CTA",
            AutoPublishYouTube = false,
            TrendKeywordsCsv = "",
            SeedTopicsCsv = "",
            GrowthMode = "balanced",
            TitleTestVariants = 3,
            MinSuccessScore = 55m
        };

        return View("Edit", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AutomationProfileEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PopulateSelections();
            return View("Edit", model);
        }

        var entity = new AutomationProfile
        {
            ProjectId = model.ProjectId,
            Name = model.Name,
            IsActive = model.IsActive,
            LanguageCode = model.LanguageCode,
            PlatformType = model.PlatformType,
            ToneType = model.ToneType,
            DurationTargetSec = model.DurationTargetSec,
            AspectRatio = model.AspectRatio,
            SubtitleEnabled = model.SubtitleEnabled,
            ThumbnailEnabled = model.ThumbnailEnabled,
            DailyVideoLimit = model.DailyVideoLimit,
            PreferredHoursCsv = model.PreferredHoursCsv,
            TopicPrompt = model.TopicPrompt,
            HookTemplate = model.HookTemplate,
            ViralPatternTemplate = model.ViralPatternTemplate,
            AutoPublishYouTube = model.AutoPublishYouTube,
            TrendKeywordsCsv = model.TrendKeywordsCsv,
            SeedTopicsCsv = model.SeedTopicsCsv,
            GrowthMode = model.GrowthMode,
            TitleTestVariants = model.TitleTestVariants,
            MinSuccessScore = model.MinSuccessScore,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.AutomationProfiles.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var profile = await _db.AutomationProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (profile == null)
        {
            return NotFound();
        }

        PopulateSelections();

        var model = new AutomationProfileEditViewModel
        {
            AutomationProfileId = profile.AutomationProfileId,
            ProjectId = profile.ProjectId,
            Name = profile.Name,
            IsActive = profile.IsActive,
            LanguageCode = profile.LanguageCode,
            PlatformType = profile.PlatformType,
            ToneType = profile.ToneType,
            DurationTargetSec = profile.DurationTargetSec,
            AspectRatio = profile.AspectRatio,
            SubtitleEnabled = profile.SubtitleEnabled,
            ThumbnailEnabled = profile.ThumbnailEnabled,
            DailyVideoLimit = profile.DailyVideoLimit,
            PreferredHoursCsv = profile.PreferredHoursCsv,
            TopicPrompt = profile.TopicPrompt,
            HookTemplate = profile.HookTemplate,
            ViralPatternTemplate = profile.ViralPatternTemplate,
            AutoPublishYouTube = profile.AutoPublishYouTube,
            TrendKeywordsCsv = profile.TrendKeywordsCsv,
            SeedTopicsCsv = profile.SeedTopicsCsv,
            GrowthMode = profile.GrowthMode,
            TitleTestVariants = profile.TitleTestVariants,
            MinSuccessScore = profile.MinSuccessScore
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AutomationProfileEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.AutomationProfileId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            PopulateSelections();
            return View(model);
        }

        var existing = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (existing == null)
        {
            return NotFound();
        }

        existing.ProjectId = model.ProjectId;
        existing.Name = model.Name;
        existing.IsActive = model.IsActive;
        existing.LanguageCode = model.LanguageCode;
        existing.PlatformType = model.PlatformType;
        existing.ToneType = model.ToneType;
        existing.DurationTargetSec = model.DurationTargetSec;
        existing.AspectRatio = model.AspectRatio;
        existing.SubtitleEnabled = model.SubtitleEnabled;
        existing.ThumbnailEnabled = model.ThumbnailEnabled;
        existing.DailyVideoLimit = model.DailyVideoLimit;
        existing.PreferredHoursCsv = model.PreferredHoursCsv;
        existing.TopicPrompt = model.TopicPrompt;
        existing.HookTemplate = model.HookTemplate;
        existing.ViralPatternTemplate = model.ViralPatternTemplate;
        existing.AutoPublishYouTube = model.AutoPublishYouTube;
        existing.TrendKeywordsCsv = model.TrendKeywordsCsv;
        existing.SeedTopicsCsv = model.SeedTopicsCsv;
        existing.GrowthMode = model.GrowthMode;
        existing.TitleTestVariants = model.TitleTestVariants;
        existing.MinSuccessScore = model.MinSuccessScore;
        existing.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(int id, CancellationToken cancellationToken)
    {
        var profile = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (profile == null)
        {
            return NotFound();
        }

        profile.IsActive = true;
        profile.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id, CancellationToken cancellationToken)
    {
        var profile = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (profile == null)
        {
            return NotFound();
        }

        profile.IsActive = false;
        profile.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private void PopulateSelections()
    {
        ViewBag.PlatformTypes = new List<SelectListItem>
        {
            new("YouTube Shorts", "1"),
            new("TikTok", "2"),
            new("Instagram Reels", "3")
        };

        ViewBag.ToneTypes = new List<SelectListItem>
        {
            new("Motivational", "1"),
            new("Educational", "2"),
            new("Storytelling", "3"),
            new("Emotional", "4")
        };

        ViewBag.AspectRatios = new List<SelectListItem>
        {
            new("9:16 Vertical", "1"),
            new("16:9 Horizontal", "2"),
            new("1:1 Square", "3")
        };

        ViewBag.GrowthModes = new List<SelectListItem>
        {
            new("Balanced", "balanced"),
            new("Aggressive", "aggressive"),
            new("Safe", "safe")
        };
    }
}