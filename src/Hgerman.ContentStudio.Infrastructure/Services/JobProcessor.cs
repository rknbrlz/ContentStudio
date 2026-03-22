using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class JobProcessor : IJobProcessor
{
    private readonly ContentStudioDbContext _db;
    private readonly IScriptGenerationService _scriptService;
    private readonly IScenePlannerService _scenePlannerService;
    private readonly IImagePromptService _imagePromptService;
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IVoiceGenerationService _voiceGenerationService;
    private readonly ISubtitleService _subtitleService;
    private readonly IVideoRenderService _videoRenderService;
    private readonly IStorageService _storageService;
    private readonly ILogger<JobProcessor> _logger;

    private const int LockMinutes = 15;

    public JobProcessor(
        ContentStudioDbContext db,
        IScriptGenerationService scriptService,
        IScenePlannerService scenePlannerService,
        IImagePromptService imagePromptService,
        IImageGenerationService imageGenerationService,
        IVoiceGenerationService voiceGenerationService,
        ISubtitleService subtitleService,
        IVideoRenderService videoRenderService,
        IStorageService storageService,
        ILogger<JobProcessor> logger)
    {
        _db = db;
        _scriptService = scriptService;
        _scenePlannerService = scenePlannerService;
        _imagePromptService = imagePromptService;
        _imageGenerationService = imageGenerationService;
        _voiceGenerationService = voiceGenerationService;
        _subtitleService = subtitleService;
        _videoRenderService = videoRenderService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<int> RecoverTimedOutJobsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        var staleJobs = await _db.VideoJobs
            .Where(x =>
                x.Status == VideoJobStatus.Processing &&
                x.LockExpiresAt != null &&
                x.LockExpiresAt < utcNow)
            .ToListAsync(cancellationToken);

        foreach (var job in staleJobs)
        {
            job.Status = VideoJobStatus.Queued;
            job.CurrentStep = VideoPipelineStep.Recovering;
            job.WorkerLockId = null;
            job.LockedBy = null;
            job.LockExpiresAt = null;
            job.LastHeartbeatAt = null;
            job.ErrorMessage = null;
            job.UpdatedDate = utcNow;
        }

        if (staleJobs.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Recovered {Count} stale jobs.", staleJobs.Count);
        }

        return staleJobs.Count;
    }

    public async Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var workerLockId = Guid.NewGuid();

        var claimedJobId = await TryClaimNextJobAsync(workerLockId, utcNow, cancellationToken);
        if (!claimedJobId.HasValue)
        {
            return false;
        }

        try
        {
            await ProcessJobInternalAsync(claimedJobId.Value, workerLockId, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {VideoJobId} failed.", claimedJobId.Value);

            var failedJob = await _db.VideoJobs
                .FirstAsync(x => x.VideoJobId == claimedJobId.Value, cancellationToken);

            var failedStep = failedJob.CurrentStep;

            failedJob.Status = VideoJobStatus.Failed;
            failedJob.CurrentStep = VideoPipelineStep.Failed;
            failedJob.ErrorMessage = ex.Message;
            failedJob.WorkerLockId = null;
            failedJob.LockedBy = null;
            failedJob.LockExpiresAt = null;
            failedJob.LastHeartbeatAt = null;
            failedJob.UpdatedDate = DateTime.UtcNow;
            failedJob.RetryCount += 1;

            _db.ErrorLogs.Add(new ErrorLog
            {
                VideoJobId = failedJob.VideoJobId,
                StepName = failedStep.ToString(),
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.ToString(),
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    private async Task<int?> TryClaimNextJobAsync(
        Guid workerLockId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var job = await _db.VideoJobs
            .Where(x =>
                x.Status == VideoJobStatus.Queued &&
                (x.LockExpiresAt == null || x.LockExpiresAt < utcNow))
            .OrderBy(x => x.VideoJobId)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            await tx.CommitAsync(cancellationToken);
            return null;
        }

        job.Status = VideoJobStatus.Processing;
        job.CurrentStep = VideoPipelineStep.Queued;
        job.WorkerLockId = workerLockId;
        job.LockedBy = Environment.MachineName;
        job.LockExpiresAt = utcNow.AddMinutes(LockMinutes);
        job.LastHeartbeatAt = utcNow;
        job.UpdatedDate = utcNow;
        job.ErrorMessage = null;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Claimed VideoJobId={VideoJobId} with WorkerLockId={WorkerLockId}.",
            job.VideoJobId,
            workerLockId);

        return job.VideoJobId;
    }

    private async Task ProcessJobInternalAsync(
        int videoJobId,
        Guid workerLockId,
        CancellationToken cancellationToken)
    {
        var job = await _db.VideoJobs
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        await EnsureLockAsync(job, workerLockId, cancellationToken);

        await UpdateStepAsync(job, VideoPipelineStep.ScriptGenerating, 5, cancellationToken);

        var script = await _scriptService.GenerateScriptAsync(job, cancellationToken);

        var scriptBlobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/script/script.txt";
        var scriptPublicUrl = await _storageService.UploadTextAsync(
            scriptBlobPath,
            script,
            "text/plain",
            cancellationToken);

        var scriptAsset = new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.ScriptText,
            ProviderName = "OpenAI",
            FileName = "script.txt",
            BlobPath = scriptBlobPath,
            PublicUrl = scriptPublicUrl,
            MimeType = "text/plain",
            FileSize = System.Text.Encoding.UTF8.GetByteCount(script),
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.Assets.Add(scriptAsset);
        await _db.SaveChangesAsync(cancellationToken);

        await UpdateStepAsync(job, VideoPipelineStep.ScriptReady, 15, cancellationToken);

        var oldScenes = await _db.VideoScenes
            .Where(x => x.VideoJobId == job.VideoJobId)
            .ToListAsync(cancellationToken);

        if (oldScenes.Count > 0)
        {
            _db.VideoScenes.RemoveRange(oldScenes);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await UpdateStepAsync(job, VideoPipelineStep.ScenePlanning, 20, cancellationToken);

        var scenes = await _scenePlannerService.BuildScenesAsync(job, script, cancellationToken);

        foreach (var scene in scenes)
        {
            scene.VideoJobId = job.VideoJobId;
            scene.Status = VideoJobStatus.Queued;
            scene.CreatedDate = DateTime.UtcNow;
            scene.UpdatedDate = DateTime.UtcNow;
            _db.VideoScenes.Add(scene);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await UpdateStepAsync(job, VideoPipelineStep.SceneReady, 35, cancellationToken);

        job = await _db.VideoJobs
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        var orderedScenes = job.Scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        for (var i = 0; i < orderedScenes.Count; i++)
        {
            var scene = orderedScenes[i];

            await UpdateStepAsync(
                job,
                VideoPipelineStep.ImagePromptGenerating,
                35 + (i * 15 / Math.Max(1, orderedScenes.Count)),
                cancellationToken);

            scene.ScenePrompt = await _imagePromptService.GenerateScenePromptAsync(job, scene, cancellationToken);
            scene.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await UpdateStepAsync(
                job,
                VideoPipelineStep.ImageGenerating,
                40 + (i * 20 / Math.Max(1, orderedScenes.Count)),
                cancellationToken);

            var imageAsset = await _imageGenerationService.GenerateImageAsync(job, scene, cancellationToken);

            _db.Assets.Add(imageAsset);
            await _db.SaveChangesAsync(cancellationToken);

            scene.ImageAssetId = imageAsset.AssetId;
            scene.Status = VideoJobStatus.Completed;
            scene.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await UpdateStepAsync(job, VideoPipelineStep.ImagesReady, 60, cancellationToken);

        await UpdateStepAsync(job, VideoPipelineStep.VoiceGenerating, 65, cancellationToken);

        var voiceAsset = await _voiceGenerationService.GenerateVoiceAsync(job, cancellationToken);
        if (voiceAsset is not null)
        {
            _db.Assets.Add(voiceAsset);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await UpdateStepAsync(job, VideoPipelineStep.VoiceReady, 75, cancellationToken);

        Asset? subtitleAsset = null;
        if (job.SubtitleEnabled)
        {
            await UpdateStepAsync(job, VideoPipelineStep.SubtitleGenerating, 78, cancellationToken);

            subtitleAsset = await _subtitleService.GenerateSubtitleAsync(job, cancellationToken);
            if (subtitleAsset is not null)
            {
                _db.Assets.Add(subtitleAsset);
                await _db.SaveChangesAsync(cancellationToken);
            }

            await UpdateStepAsync(job, VideoPipelineStep.SubtitleReady, 82, cancellationToken);
        }

        await UpdateStepAsync(job, VideoPipelineStep.Rendering, 85, cancellationToken);

        job = await _db.VideoJobs
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        var sceneAssets = await _db.Assets
            .Where(x => x.VideoJobId == job.VideoJobId && x.AssetType == AssetType.SceneImage)
            .ToListAsync(cancellationToken);

        var orderedSceneAssets = job.Scenes
            .OrderBy(x => x.SceneNo)
            .Select(x => sceneAssets.FirstOrDefault(a => a.AssetId == x.ImageAssetId))
            .Where(x => x is not null)
            .Cast<Asset>()
            .ToList();

        if (orderedSceneAssets.Count == 0)
        {
            throw new InvalidOperationException("No scene image assets found for render.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "contentstudio", $"job_{job.VideoJobId}");
        var scenesFolderPath = Path.Combine(tempRoot, "scenes");
        var outputFolderPath = Path.Combine(tempRoot, "output");

        Directory.CreateDirectory(scenesFolderPath);
        Directory.CreateDirectory(outputFolderPath);

        foreach (var asset in orderedSceneAssets)
        {
            var localPath = await _storageService.GetLocalPathAsync(asset.BlobPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException(
                    $"Scene image file not found for asset {asset.AssetId}. BlobPath={asset.BlobPath}");
            }

            var targetPath = Path.Combine(scenesFolderPath, asset.FileName);
            File.Copy(localPath, targetPath, true);
        }

        string? audioFilePath = null;
        if (voiceAsset is not null && !string.IsNullOrWhiteSpace(voiceAsset.BlobPath))
        {
            audioFilePath = await _storageService.GetLocalPathAsync(voiceAsset.BlobPath, cancellationToken);
        }

        string? subtitleFilePath = null;
        if (subtitleAsset is not null && !string.IsNullOrWhiteSpace(subtitleAsset.BlobPath))
        {
            subtitleFilePath = await _storageService.GetLocalPathAsync(
    subtitleAsset.BlobPath,
    cancellationToken);
        }

        var finalVideoPath = await _videoRenderService.RenderFinalVideoAsync(
            job,
            job.Scenes.OrderBy(x => x.SceneNo).ToList(),
            scenesFolderPath,
            audioFilePath,
            subtitleFilePath,
            outputFolderPath,
            cancellationToken);

        if (!File.Exists(finalVideoPath))
        {
            throw new FileNotFoundException("Final video file was not created.", finalVideoPath);
        }

        var finalFileInfo = new FileInfo(finalVideoPath);
        if (finalFileInfo.Length <= 0)
        {
            throw new InvalidOperationException("Final video file is empty.");
        }

        var finalBlobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/final/final.mp4";
        var finalBytes = await File.ReadAllBytesAsync(finalVideoPath, cancellationToken);
        var finalPublicUrl = await _storageService.UploadBytesAsync(
            finalBlobPath,
            finalBytes,
            "video/mp4",
            cancellationToken);

        var finalAsset = new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.FinalVideo,
            ProviderName = "FFmpeg",
            FileName = "final.mp4",
            BlobPath = finalBlobPath,
            PublicUrl = finalPublicUrl,
            MimeType = "video/mp4",
            FileSize = finalBytes.LongLength,
            DurationMs = job.DurationTargetSec * 1000,
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.Assets.Add(finalAsset);
        await _db.SaveChangesAsync(cancellationToken);

        job.Status = VideoJobStatus.Completed;
        job.CurrentStep = VideoPipelineStep.Completed;
        job.ProgressPercent = 100;
        job.CompletedDate = DateTime.UtcNow;
        job.WorkerLockId = null;
        job.LockedBy = null;
        job.LockExpiresAt = null;
        job.LastHeartbeatAt = null;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {VideoJobId} completed successfully.", job.VideoJobId);
    }

    private async Task UpdateStepAsync(
        VideoJob job,
        VideoPipelineStep step,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        job.CurrentStep = step;
        job.ProgressPercent = progressPercent;
        job.LastHeartbeatAt = DateTime.UtcNow;
        job.LockExpiresAt = DateTime.UtcNow.AddMinutes(LockMinutes);
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLockAsync(
        VideoJob job,
        Guid workerLockId,
        CancellationToken cancellationToken)
    {
        if (job.WorkerLockId != workerLockId)
        {
            throw new InvalidOperationException($"Job lock mismatch for VideoJobId={job.VideoJobId}");
        }

        job.LastHeartbeatAt = DateTime.UtcNow;
        job.LockExpiresAt = DateTime.UtcNow.AddMinutes(LockMinutes);
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}