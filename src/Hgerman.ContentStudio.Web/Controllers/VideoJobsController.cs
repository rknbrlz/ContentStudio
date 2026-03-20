using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Web.Controllers;

public class VideoJobsController : Controller
{
    private readonly ContentStudioDbContext _db;
    private readonly IJobProcessor _jobProcessor;
    private readonly IPublishService _publishService;

    public VideoJobsController(
        ContentStudioDbContext db,
        IJobProcessor jobProcessor,
        IPublishService publishService)
    {
        _db = db;
        _jobProcessor = jobProcessor;
        _publishService = publishService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var jobs = await _db.VideoJobs
            .OrderByDescending(x => x.VideoJobId)
            .ToListAsync(cancellationToken);

        return View(jobs);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var job = await _db.VideoJobs
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
    public async Task<IActionResult> Retry(int id, CancellationToken cancellationToken)
    {
        var job = await _db.VideoJobs.FirstOrDefaultAsync(x => x.VideoJobId == id, cancellationToken);
        if (job == null)
        {
            return NotFound();
        }

        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.Queued;
        job.ProgressPercent = 0;
        job.ErrorMessage = null;
        job.CompletedDate = null;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> PublishToYouTube(int id, CancellationToken cancellationToken)
    {
        try
        {
            var url = await _publishService.PublishToYouTubeAsync(id, cancellationToken);

            return Json(new
            {
                success = true,
                videoJobId = id,
                youtubeUrl = url
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                success = false,
                videoJobId = id,
                error = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus(int id, CancellationToken cancellationToken)
    {
        var job = await _db.VideoJobs
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .Include(x => x.ErrorLogs)
            .FirstOrDefaultAsync(x => x.VideoJobId == id, cancellationToken);

        if (job == null)
        {
            return NotFound();
        }

        var finalVideoAsset = job.Assets
            .Where(x => x.AssetType == AssetType.FinalVideo)
            .OrderByDescending(x => x.AssetId)
            .FirstOrDefault();

        return Json(new
        {
            jobId = job.VideoJobId,
            status = job.Status.ToString(),
            step = job.CurrentStep.ToString(),
            progress = job.ProgressPercent,
            updated = job.UpdatedDate,
            errorMessage = job.ErrorMessage,
            completedDate = job.CompletedDate,

            finalVideo = finalVideoAsset == null
                ? null
                : new
                {
                    assetType = finalVideoAsset.AssetType.ToString(),
                    fileName = finalVideoAsset.FileName,
                    publicUrl = finalVideoAsset.PublicUrl,
                    status = finalVideoAsset.Status.ToString(),
                    fileSize = finalVideoAsset.FileSize
                },

            scenes = job.Scenes
                .OrderBy(x => x.SceneNo)
                .Select(x => new
                {
                    sceneNo = x.SceneNo,
                    sceneText = x.SceneText,
                    scenePrompt = x.ScenePrompt,
                    durationSecond = x.DurationSecond
                })
                .ToList(),

            assets = job.Assets
                .OrderBy(x => x.AssetType)
                .ThenBy(x => x.AssetId)
                .Select(x => new
                {
                    assetType = x.AssetType.ToString(),
                    fileName = x.FileName,
                    blobPath = x.BlobPath,
                    publicUrl = x.PublicUrl,
                    status = x.Status.ToString(),
                    fileSize = x.FileSize
                })
                .ToList(),

            errors = job.ErrorLogs
                .OrderByDescending(x => x.ErrorLogId)
                .Select(x => new
                {
                    stepName = x.StepName,
                    errorType = x.ErrorType,
                    errorMessage = x.ErrorMessage,
                    createdDate = x.CreatedDate
                })
                .ToList()
        });
    }
}