using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Application.Services;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class VideoJobService : IVideoJobService
{
    private readonly ContentStudioDbContext _db;
    private readonly IStorageService _storageService;

    public VideoJobService(ContentStudioDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task<int> CreateJobAsync(CreateVideoJobRequest request, CancellationToken cancellationToken = default)
    {
        var job = new VideoJob
        {
            ProjectId = request.ProjectId,
            JobNo = JobNumberGenerator.Create(),
            Title = request.Title,
            Topic = request.Topic,
            SourcePrompt = request.SourcePrompt,
            LanguageCode = request.LanguageCode,
            PlatformType = request.PlatformType,
            ToneType = request.ToneType,
            DurationTargetSec = request.DurationTargetSec,
            AspectRatio = request.AspectRatio,
            VoiceProvider = request.VoiceProvider,
            VoiceName = request.VoiceName,
            SubtitleEnabled = request.SubtitleEnabled,
            ThumbnailEnabled = request.ThumbnailEnabled,
            InputMode = request.InputMode,
            Status = VideoJobStatus.Draft,
            CurrentStep = VideoPipelineStep.Draft,
            CreatedDate = DateTime.UtcNow
        };

        _db.VideoJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return job.VideoJobId;
    }

    public async Task<int> AttachUploadedSourceImageAsync(
        int videoJobId,
        string originalFileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs.FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var safeFileName = $"source_{videoJobId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var blobPath = $"video-jobs/{videoJobId}/uploads/{safeFileName}";

        await _storageService.UploadBytesAsync(blobPath, fileBytes, contentType, cancellationToken);

        var asset = new Asset
        {
            VideoJobId = videoJobId,
            AssetType = AssetType.SourceUploadImage,
            ProviderName = "UserUpload",
            FileName = safeFileName,
            BlobPath = blobPath,
            PublicUrl = null,
            MimeType = contentType,
            FileSize = fileBytes.LongLength,
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        job.InputMode = InputModeType.UploadedSingleImage;
        job.PrimarySourceAssetId = asset.AssetId;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return asset.AssetId;
    }

    public async Task<List<VideoJobListItemDto>> GetJobListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.VideoJobs
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
    }

    public async Task<VideoJob?> GetJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        return await _db.VideoJobs
            .Include(x => x.Project)
            .Include(x => x.PrimarySourceAsset)
            .Include(x => x.Scenes.OrderBy(s => s.SceneNo))
            .Include(x => x.Assets)
            .Include(x => x.PublishTasks)
            .Include(x => x.ErrorLogs.OrderByDescending(e => e.ErrorLogId))
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);
    }

    public async Task QueueJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs.FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);
        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.ScriptGenerating;
        job.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs.FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);
        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.ScriptGenerating;
        job.ErrorMessage = null;
        job.RetryCount += 1;
        job.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        return new DashboardSummaryDto
        {
            TotalJobs = await _db.VideoJobs.CountAsync(cancellationToken),
            CompletedJobs = await _db.VideoJobs.CountAsync(x => x.Status == VideoJobStatus.Completed, cancellationToken),
            FailedJobs = await _db.VideoJobs.CountAsync(x => x.Status == VideoJobStatus.Failed, cancellationToken),
            QueuedJobs = await _db.VideoJobs.CountAsync(
                x => x.Status == VideoJobStatus.Queued || x.Status == VideoJobStatus.Processing,
                cancellationToken)
        };
    }
}