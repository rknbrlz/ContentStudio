using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Shared.DTOs;
using Hgerman.ContentStudio.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hgerman.ContentStudio.Web.Controllers;

public class VideoJobsController : Controller
{
    private readonly IVideoJobService _videoJobService;

    public VideoJobsController(IVideoJobService videoJobService)
    {
        _videoJobService = videoJobService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var jobs = await _videoJobService.GetJobListAsync(cancellationToken);
        return View(jobs);
    }

    [HttpGet]
    public IActionResult Create()
    {
        PopulateLookups();

        return View(new CreateVideoJobViewModel
        {
            ProjectId = 1,
            DurationTargetSec = 45,
            SubtitleEnabled = true,
            ThumbnailEnabled = true,
            LanguageCode = "en",
            InputMode = InputModeType.AiOnly
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 15 * 1024 * 1024)]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreateVideoJobViewModel model, CancellationToken cancellationToken)
    {
        if (model.InputMode == InputModeType.UploadedSingleImage && model.SourceImage == null)
        {
            ModelState.AddModelError(nameof(model.SourceImage), "Please upload a source image.");
        }

        if (model.SourceImage != null)
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(model.SourceImage.FileName).ToLowerInvariant();

            if (!allowed.Contains(ext))
            {
                ModelState.AddModelError(nameof(model.SourceImage), "Only .jpg, .jpeg, .png, .webp files are allowed.");
            }

            if (model.SourceImage.Length <= 0)
            {
                ModelState.AddModelError(nameof(model.SourceImage), "Uploaded file is empty.");
            }

            if (model.SourceImage.Length > 15 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(model.SourceImage), "Max file size is 15 MB.");
            }
        }

        if (!ModelState.IsValid)
        {
            PopulateLookups();
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

        var jobId = await _videoJobService.CreateJobAsync(request, cancellationToken);

        if (model.SourceImage != null)
        {
            await using var ms = new MemoryStream();
            await model.SourceImage.CopyToAsync(ms, cancellationToken);

            await _videoJobService.AttachUploadedSourceImageAsync(
                jobId,
                model.SourceImage.FileName,
                model.SourceImage.ContentType ?? "image/jpeg",
                ms.ToArray(),
                cancellationToken);
        }

        await _videoJobService.QueueJobAsync(jobId, cancellationToken);

        return RedirectToAction(nameof(Details), new { id = jobId });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var job = await _videoJobService.GetJobAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(int id, CancellationToken cancellationToken)
    {
        await _videoJobService.RetryJobAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private void PopulateLookups()
    {
        ViewBag.PlatformTypes = Enum.GetValues<PlatformType>()
            .Select(x => new SelectListItem(x.ToString(), x.ToString()))
            .ToList();

        ViewBag.ToneTypes = Enum.GetValues<ToneType>()
            .Select(x => new SelectListItem(x.ToString(), x.ToString()))
            .ToList();

        ViewBag.AspectRatios = Enum.GetValues<AspectRatioType>()
            .Select(x => new SelectListItem(x.ToString(), x.ToString()))
            .ToList();

        ViewBag.InputModes = Enum.GetValues<InputModeType>()
            .Select(x => new SelectListItem(x.ToString(), ((int)x).ToString()))
            .ToList();
    }
}