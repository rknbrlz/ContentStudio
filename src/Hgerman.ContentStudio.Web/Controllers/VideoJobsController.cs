using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.DTOs;
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
        var model = new CreateVideoJobRequest
        {
            ProjectId = 1,
            Title = "New Video Job",
            Topic = "Motivation",
            LanguageCode = "en",
            DurationTargetSec = 20,
            SubtitleEnabled = true,
            ThumbnailEnabled = true
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateVideoJobRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        var videoJobId = await _videoJobService.CreateJobAsync(request, cancellationToken);
        await _videoJobService.QueueJobAsync(videoJobId, cancellationToken);

        return RedirectToAction(nameof(Details), new { id = videoJobId });
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
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id, CancellationToken cancellationToken)
    {
        await _videoJobService.RetryJobAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        await _publishService.PublishToYouTubeAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }
}