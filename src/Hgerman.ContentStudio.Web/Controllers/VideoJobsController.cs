using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Application.Services;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.DTOs;
using Hgerman.ContentStudio.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Web.Controllers;

public class VideoJobsController : Controller
{
    private readonly ContentStudioDbContext _db;
    private readonly IJobProcessor _jobProcessor;
    private readonly IPublishService _publishService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAutomationService _automationService;
    private readonly IVideoJobService _videoJobService;

    public VideoJobsController(
        ContentStudioDbContext db,
        IJobProcessor jobProcessor,
        IPublishService publishService,
        IWebHostEnvironment environment,
        IAutomationService automationService,
        IVideoJobService videoJobService)
    {
        _db = db;
        _jobProcessor = jobProcessor;
        _publishService = publishService;
        _environment = environment;
        _automationService = automationService;
        _videoJobService = videoJobService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _videoJobService.GetJobListAsync(cancellationToken);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAutomationNow(CancellationToken cancellationToken)
    {
        var createdCount = await _automationService.RunAllActiveNowAsync(cancellationToken);
        TempData["SuccessMessage"] = $"{createdCount} automation job(s) created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create()
    {
        var model = new CreateVideoJobViewModel
        {
            ProjectId = 1,
            LanguageCode = "en",
            PlatformType = PlatformType.YouTubeShorts,
            ToneType = ToneType.Inspirational,
            DurationTargetSec = 45,
            AspectRatio = AspectRatioType.Vertical916,
            SubtitleEnabled = true,
            ThumbnailEnabled = true
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var job = await _db.VideoJobs
            .Include(x => x.Project)
            .Include(x => x.PrimarySourceAsset)
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .Include(x => x.PublishTasks)
            .Include(x => x.ErrorLogs)
            .FirstOrDefaultAsync(x => x.VideoJobId == id, cancellationToken);

        if (job == null)
            return NotFound();

        return View(job);
    }
}