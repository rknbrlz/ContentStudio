using Hgerman.ContentStudio.Application.Interfaces;
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

    [HttpGet]
    public IActionResult Create()
    {
        var model = new CreateVideoJobViewModel
        {
            ProjectId = 1,
            Title = "New Video Job",
            Topic = "Motivation",
            SourcePrompt = null,
            LanguageCode = "en",
            DurationTargetSec = 20,
            SubtitleEnabled = true,
            ThumbnailEnabled = true
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateVideoJobViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new CreateVideoJobRequest
        {
            ProjectId = model.ProjectId,
            Title = model.Title,
            Topic = model.Topic,
            SourcePrompt = model.SourcePrompt,
            LanguageCode = model.LanguageCode,
            PlatformType = model.PlatformType,
            ToneType = model.ToneType,
            DurationTargetSec = model.DurationTargetSec,
            AspectRatio = model.AspectRatio,
            VoiceProvider = model.VoiceProvider,
            VoiceName = model.VoiceName,
            SubtitleEnabled = model.SubtitleEnabled,
            ThumbnailEnabled = model.ThumbnailEnabled,
            InputMode = model.InputMode
        };

        await _videoJobService.CreateJobAsync(request, cancellationToken);

        var latestJobId = await _db.VideoJobs
            .AsNoTracking()
            .OrderByDescending(x => x.VideoJobId)
            .Select(x => x.VideoJobId)
            .FirstAsync(cancellationToken);

        if (model.SourceImage != null && model.SourceImage.Length > 0)
        {
            await using var ms = new MemoryStream();
            await model.SourceImage.CopyToAsync(ms, cancellationToken);

            await _videoJobService.AttachUploadedSourceImageAsync(
                latestJobId,
                model.SourceImage.FileName,
                model.SourceImage.ContentType ?? "application/octet-stream",
                ms.ToArray(),
                cancellationToken);
        }

        await _videoJobService.QueueJobAsync(latestJobId, cancellationToken);

        TempData["Success"] = $"Video job #{latestJobId} created and queued.";
        return RedirectToAction(nameof(Details), new { id = latestJobId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var job = await _db.VideoJobs
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.AutomationProfile)
            .Include(x => x.PrimarySourceAsset)
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .Include(x => x.ErrorLogs)
            .FirstOrDefaultAsync(x => x.VideoJobId == id, cancellationToken);

        if (job == null)
        {
            return NotFound();
        }

        return View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken)
    {
        await _jobProcessor.ProcessNextPendingJobAsync(cancellationToken);
        TempData["Success"] = "Next pending video job started.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAutomationNow(CancellationToken cancellationToken)
    {
        var count = await _automationService.RunAllActiveNowAsync(cancellationToken);
        TempData["Success"] = $"{count} automation profile(s) triggered.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id, CancellationToken cancellationToken)
    {
        await _videoJobService.RetryJobAsync(id, cancellationToken);
        TempData["Success"] = $"Video job #{id} re-queued.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        await _publishService.PublishToYouTubeAsync(id, cancellationToken);
        TempData["Success"] = $"Publish started for video job #{id}.";
        return RedirectToAction(nameof(Details), new { id });
    }
}