using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
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

    public VideoJobsController(
        ContentStudioDbContext db,
        IJobProcessor jobProcessor,
        IPublishService publishService,
        IWebHostEnvironment environment)
    {
        _db = db;
        _jobProcessor = jobProcessor;
        _publishService = publishService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _db.VideoJobs
            .OrderByDescending(x => x.VideoJobId)
            .Select(x => new VideoJobListItemDto
            {
                VideoJobId = x.VideoJobId,
                JobNo = x.JobNo,
                Title = x.Title,
                LanguageCode = x.LanguageCode,
                PlatformType = x.PlatformType,
                Status = x.Status,
                CurrentStep = x.CurrentStep.ToString(),
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        return View(items);
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
            ThumbnailEnabled = true,
            InputMode = InputModeType.AiOnly
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 25_000_000)]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Create(CreateVideoJobViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.InputMode == InputModeType.UploadedSingleImage && model.SourceImage == null)
        {
            ModelState.AddModelError(nameof(model.SourceImage), "Please upload one source image.");
            return View(model);
        }

        var now = DateTime.UtcNow;
        var jobNo = $"VJ-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var job = new VideoJob
        {
            JobNo = jobNo,
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
            InputMode = model.InputMode,
            Status = VideoJobStatus.Queued,
            CurrentStep = VideoPipelineStep.Queued,
            ProgressPercent = 0,
            RetryCount = 0,
            CreatedDate = now,
            UpdatedDate = now
        };

        _db.VideoJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        if (model.InputMode == InputModeType.UploadedSingleImage && model.SourceImage != null)
        {
            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            }

            var uploadsRoot = Path.Combine(webRoot, "uploads", "video-jobs", job.VideoJobId.ToString());
            Directory.CreateDirectory(uploadsRoot);

            var extension = Path.GetExtension(model.SourceImage.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            var fileName = $"source{extension}";
            var physicalPath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await model.SourceImage.CopyToAsync(stream, cancellationToken);
            }

            var asset = new Asset
            {
                VideoJobId = job.VideoJobId,
                AssetType = AssetType.SourceImage,
                FileName = fileName,
                MimeType = string.IsNullOrWhiteSpace(model.SourceImage.ContentType)
                    ? "image/jpeg"
                    : model.SourceImage.ContentType,
                BlobPath = $"/uploads/video-jobs/{job.VideoJobId}/{fileName}",
                PublicUrl = $"/uploads/video-jobs/{job.VideoJobId}/{fileName}",
                FileSize = model.SourceImage.Length,
                Status = VideoJobStatus.Completed,
                CreatedDate = now,
                UpdatedDate = now
            };

            _db.Assets.Add(asset);
            await _db.SaveChangesAsync(cancellationToken);

            job.PrimarySourceAssetId = asset.AssetId;
            job.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id = job.VideoJobId });
    }

    [HttpGet]
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
            finalVideo = finalVideoAsset == null ? null : new
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