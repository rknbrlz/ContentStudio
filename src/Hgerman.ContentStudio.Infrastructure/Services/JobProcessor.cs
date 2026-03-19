using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Drawing;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class JobProcessor : IJobProcessor
{
    private readonly ContentStudioDbContext _db;
    private readonly IScriptGenerationService _scriptService;
    private readonly IScenePlannerService _scenePlanner;
    private readonly IImagePromptService _imagePromptService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVoiceGenerationService _voiceGenerationService;
    private readonly ISubtitleService _subtitleService;
    private readonly IVideoRenderService _videoRenderService;
    private readonly IPublishService _publishService;
    private readonly IStorageService _storageService;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<JobProcessor> _logger;

    public JobProcessor(
        ContentStudioDbContext db,
        IScriptGenerationService scriptService,
        IScenePlannerService scenePlanner,
        IImagePromptService imagePromptService,
        IImageGenerationService imageGenerationService,
        IVoiceGenerationService voiceGenerationService,
        ISubtitleService subtitleService,
        IVideoRenderService videoRenderService,
        IPublishService publishService,
        IStorageService storageService,
        IOptions<StorageOptions> storageOptions,
        ILogger<JobProcessor> logger)
    {
        _db = db;
        _scriptService = scriptService;
        _scenePlanner = scenePlanner;
        _imagePromptService = imagePromptService;
        _imageGenerationService = imageGenerationService;
        _voiceGenerationService = voiceGenerationService;
        _subtitleService = subtitleService;
        _videoRenderService = videoRenderService;
        _publishService = publishService;
        _storageService = storageService;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .OrderBy(x => x.VideoJobId)
            .FirstOrDefaultAsync(x => x.Status == VideoJobStatus.Queued, cancellationToken);

        if (job is null)
        {
            return false;
        }

        try
        {
            _logger.LogInformation("JOB_START VideoJobId={VideoJobId}, Title={Title}", job.VideoJobId, job.Title);

            job.Status = VideoJobStatus.Processing;
            job.CurrentStep = VideoPipelineStep.ScriptGenerating;
            job.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var script = await _scriptService.GenerateScriptAsync(job, cancellationToken);
            _logger.LogInformation("SCRIPT_DONE VideoJobId={VideoJobId}, Length={Length}", job.VideoJobId, script.Length);

            var scriptBlobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/script/script.txt";
            await _storageService.UploadTextAsync(scriptBlobPath, script, "text/plain", cancellationToken);

            var scriptAsset = new Asset
            {
                VideoJobId = job.VideoJobId,
                AssetType = AssetType.ScriptText,
                ProviderName = "OpenAI",
                FileName = "script.txt",
                BlobPath = scriptBlobPath,
                PublicUrl = null,
                MimeType = "text/plain",
                FileSize = script.Length,
                Status = VideoJobStatus.Completed,
                CreatedDate = DateTime.UtcNow
            };
            _db.Assets.Add(scriptAsset);

            job.CurrentStep = VideoPipelineStep.ScenePlanning;
            job.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var scenes = await _scenePlanner.BuildScenesAsync(job, script, cancellationToken);
            _logger.LogInformation("SCENES_DONE VideoJobId={VideoJobId}, Count={Count}", job.VideoJobId, scenes.Count);

            foreach (var scene in scenes)
            {
                scene.VideoJobId = job.VideoJobId;
                _db.VideoScenes.Add(scene);
            }
            await _db.SaveChangesAsync(cancellationToken);

            job = await _db.VideoJobs
                .Include(x => x.Scenes)
                .Include(x => x.Assets)
                .FirstAsync(x => x.VideoJobId == job.VideoJobId, cancellationToken);

            job.CurrentStep = VideoPipelineStep.ImagePromptGenerating;
            if (job.InputMode == InputModeType.UploadedSingleImage)
            {
                _logger.LogInformation("USING UPLOADED IMAGE - skipping AI image generation");

                var reused = await TryUseUploadedSourceImageAsync(job, cancellationToken);

                if (!reused)
                    throw new InvalidOperationException("Uploaded image reuse failed.");
            }
            else
            {
                job.CurrentStep = VideoPipelineStep.ImagePromptGenerating;
                job.UpdatedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                foreach (var scene in job.Scenes.OrderBy(x => x.SceneNo))
                {
                    scene.ScenePrompt = await _imagePromptService.GenerateScenePromptAsync(job, scene, cancellationToken);
                    scene.Status = VideoJobStatus.Processing;
                }
                await _db.SaveChangesAsync(cancellationToken);

                job.CurrentStep = VideoPipelineStep.ImageGenerating;
                job.UpdatedDate = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                foreach (var scene in job.Scenes.OrderBy(x => x.SceneNo))
                {
                    var imageAsset = await _imageGenerationService.GenerateImageAsync(job, scene, cancellationToken);
                    _db.Assets.Add(imageAsset);
                    await _db.SaveChangesAsync(cancellationToken);

                    scene.ImageAssetId = imageAsset.AssetId;
                    scene.Status = VideoJobStatus.Completed;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            job = await _db.VideoJobs
                .Include(x => x.Scenes)
                .Include(x => x.Assets)
                .FirstAsync(x => x.VideoJobId == job.VideoJobId, cancellationToken);

            job.CurrentStep = VideoPipelineStep.VoiceGenerating;
            job.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var voiceAsset = await _voiceGenerationService.GenerateVoiceAsync(job, cancellationToken);
            _db.Assets.Add(voiceAsset);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("VOICE_DONE VideoJobId={VideoJobId}, File={FileName}", job.VideoJobId, voiceAsset.FileName);

            job = await _db.VideoJobs
                .Include(x => x.Scenes)
                .Include(x => x.Assets)
                .FirstAsync(x => x.VideoJobId == job.VideoJobId, cancellationToken);

            job.CurrentStep = VideoPipelineStep.SubtitleGenerating;
            job.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var subtitleAsset = await _subtitleService.GenerateSubtitleAsync(job, cancellationToken);
            if (subtitleAsset is not null)
            {
                _db.Assets.Add(subtitleAsset);
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("SUBTITLE_DONE VideoJobId={VideoJobId}, File={FileName}", job.VideoJobId, subtitleAsset.FileName);
            }
            else
            {
                _logger.LogInformation("SUBTITLE_SKIPPED VideoJobId={VideoJobId}", job.VideoJobId);
            }

            job = await _db.VideoJobs
                .Include(x => x.Scenes)
                .Include(x => x.Assets)
                .FirstAsync(x => x.VideoJobId == job.VideoJobId, cancellationToken);

            job.CurrentStep = VideoPipelineStep.Rendering;
            job.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            var localRoot = _storageOptions.LocalRootPath;
            var jobRoot = Path.Combine(
                localRoot,
                "projects",
                job.ProjectId.ToString(),
                "jobs",
                job.VideoJobId.ToString());

            var scenesFolderPath = Path.Combine(jobRoot, "scenes");
            var audioFilePath = Path.Combine(jobRoot, "audio", "voice.mp3");
            var subtitleFilePath = Path.Combine(jobRoot, "subtitles", "subtitle.srt");
            var outputFolderPath = Path.Combine(jobRoot, "final");

            Directory.CreateDirectory(outputFolderPath);

            var orderedScenes = job.Scenes
                .OrderBy(x => x.SceneNo)
                .ToList();

            _logger.LogInformation(
                "RENDER_START VideoJobId={VideoJobId}, Scenes={ScenesCount}, ScenesFolder={ScenesFolder}, AudioExists={AudioExists}, SubtitleExists={SubtitleExists}, Output={Output}",
                job.VideoJobId,
                orderedScenes.Count,
                scenesFolderPath,
                File.Exists(audioFilePath),
                File.Exists(subtitleFilePath),
                outputFolderPath);

            var finalVideoPath = await _videoRenderService.RenderFinalVideoAsync(
                job,
                orderedScenes,
                scenesFolderPath,
                File.Exists(audioFilePath) ? audioFilePath : null,
                File.Exists(subtitleFilePath) ? subtitleFilePath : null,
                outputFolderPath,
                cancellationToken);

            _logger.LogInformation("RENDER_DONE VideoJobId={VideoJobId}, FinalVideoPath={FinalVideoPath}", job.VideoJobId, finalVideoPath);

            if (string.IsNullOrWhiteSpace(finalVideoPath) || !File.Exists(finalVideoPath))
            {
                throw new InvalidOperationException($"Render finished but final video was not found: {finalVideoPath}");
            }

            var finalFileInfo = new FileInfo(finalVideoPath);
            if (finalFileInfo.Length <= 0)
            {
                throw new InvalidOperationException($"Render finished but final video is empty: {finalVideoPath}");
            }

            var relativeFinalPath = Path.Combine(
                "projects",
                job.ProjectId.ToString(),
                "jobs",
                job.VideoJobId.ToString(),
                "final",
                "final.mp4").Replace("\\", "/");

            var finalVideoAsset = new Asset
            {
                VideoJobId = job.VideoJobId,
                AssetType = AssetType.FinalVideo,
                ProviderName = "FFmpeg",
                FileName = "final.mp4",
                BlobPath = relativeFinalPath,
                PublicUrl = finalVideoPath,
                MimeType = "video/mp4",
                FileSize = finalFileInfo.Length,
                Status = VideoJobStatus.Completed,
                CreatedDate = DateTime.UtcNow
            };

            _db.Assets.Add(finalVideoAsset);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("FINAL_ASSET_ADDED VideoJobId={VideoJobId}, BlobPath={BlobPath}, Size={Size}",
                job.VideoJobId, finalVideoAsset.BlobPath, finalVideoAsset.FileSize);

            try
            {
                var publishTask = await _publishService.CreateDraftAsync(job, cancellationToken);
                _db.PublishTasks.Add(publishTask);
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("PUBLISH_DRAFT_CREATED VideoJobId={VideoJobId}", job.VideoJobId);
            }
            catch (Exception publishEx)
            {
                _logger.LogWarning(publishEx, "PUBLISH_DRAFT_FAILED VideoJobId={VideoJobId}", job.VideoJobId);
            }

            job.Status = VideoJobStatus.Completed;
            job.CurrentStep = VideoPipelineStep.Completed;
            job.CompletedDate = DateTime.UtcNow;
            job.UpdatedDate = DateTime.UtcNow;
            job.ErrorMessage = null;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("JOB_COMPLETED VideoJobId={VideoJobId}", job.VideoJobId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JOB_FAILED VideoJobId={VideoJobId}", job.VideoJobId);

            var failedStep = job.CurrentStep;
            job.Status = VideoJobStatus.Failed;
            job.CurrentStep = VideoPipelineStep.Failed;
            job.ErrorMessage = ex.Message;
            job.UpdatedDate = DateTime.UtcNow;

            _db.ErrorLogs.Add(new ErrorLog
            {
                VideoJobId = job.VideoJobId,
                StepName = failedStep.ToString(),
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace,
                CreatedDate = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }
    }

    private async Task<bool> TryUseUploadedSourceImageAsync(VideoJob job, CancellationToken cancellationToken)
    {
        if (job.InputMode != InputModeType.UploadedSingleImage || job.PrimarySourceAssetId == null)
            return false;

        var sourceAsset = await _db.Assets
            .FirstOrDefaultAsync(x => x.AssetId == job.PrimarySourceAssetId.Value, cancellationToken);

        if (sourceAsset == null)
            return false;

        var scenes = await _db.VideoScenes
            .Where(x => x.VideoJobId == job.VideoJobId)
            .OrderBy(x => x.SceneNo)
            .ToListAsync(cancellationToken);

        if (scenes.Count == 0)
            return false;

        var localRoot = _storageOptions.LocalRootPath;

        var jobRoot = Path.Combine(
            localRoot,
            "projects",
            job.ProjectId.ToString(),
            "jobs",
            job.VideoJobId.ToString());

        var scenesFolderPath = Path.Combine(jobRoot, "scenes");
        Directory.CreateDirectory(scenesFolderPath);

        // Upload edilen dosyanın gerçek local path'ini bul
        string sourceFilePath;

        if (!string.IsNullOrWhiteSpace(sourceAsset.PublicUrl) && File.Exists(sourceAsset.PublicUrl))
        {
            sourceFilePath = sourceAsset.PublicUrl;
        }
        else
        {
            sourceFilePath = await _storageService.GetLocalPathAsync(sourceAsset.BlobPath, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Uploaded source image not found: {sourceFilePath}");
        }

        var fileInfo = new FileInfo(sourceFilePath);
        var extension = fileInfo.Extension;
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var index = 1;

        foreach (var scene in scenes)
        {
            if (scene.ImageAssetId.HasValue)
                continue;

            var targetFileName = $"scene_{index}.JPG";
            var targetFilePath = Path.Combine(scenesFolderPath, targetFileName);

            File.Copy(sourceFilePath, targetFilePath, true);

            var clonedAsset = new Asset
            {
                VideoJobId = job.VideoJobId,
                VideoSceneId = scene.VideoSceneId,
                AssetType = AssetType.SceneImage,
                ProviderName = "UploadedSourceReuse",
                FileName = targetFileName,
                BlobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/scenes/{targetFileName}",
                PublicUrl = targetFilePath,
                MimeType = sourceAsset.MimeType,
                FileSize = new FileInfo(targetFilePath).Length,
                Width = sourceAsset.Width,
                Height = sourceAsset.Height,
                Status = VideoJobStatus.Completed,
                CreatedDate = DateTime.UtcNow
            };

            _db.Assets.Add(clonedAsset);
            await _db.SaveChangesAsync(cancellationToken);

            scene.ImageAssetId = clonedAsset.AssetId;
            scene.Status = VideoJobStatus.Completed;
            scene.UpdatedDate = DateTime.UtcNow;

            index++;
        }

        job.CurrentStep = VideoPipelineStep.ImageGenerating;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}