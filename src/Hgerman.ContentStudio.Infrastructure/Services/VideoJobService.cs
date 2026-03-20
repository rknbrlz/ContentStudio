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

    public VideoJobService(ContentStudioDbContext db)
    {
        _db = db;
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
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.VideoJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return job.VideoJobId;
    }

    public async Task<IReadOnlyList<VideoJobListItemDto>> GetJobListAsync(CancellationToken cancellationToken = default)
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
            .Include(x => x.Scenes)
            .Include(x => x.Assets)
            .Include(x => x.PublishTasks)
            .Include(x => x.ErrorLogs)
            .Include(x => x.PrimarySourceAsset)
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);
    }

    public async Task QueueJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs.FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.Queued;
        job.ErrorMessage = null;
        job.NextRetryDate = null;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryJobAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs.FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        job.Status = VideoJobStatus.Queued;
        job.CurrentStep = VideoPipelineStep.Queued;
        job.ErrorMessage = null;
        job.NextRetryDate = null;
        job.WorkerLockId = null;
        job.LockedBy = null;
        job.LockExpiresAt = null;
        job.LastHeartbeatAt = null;
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

    public async Task AttachUploadedSourceImageAsync(
        int videoJobId,
        string fileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs.FirstAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "source.jpg" : fileName;
        var ext = Path.GetExtension(safeFileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".jpg";
            safeFileName += ext;
        }

        var folder = Path.Combine(AppContext.BaseDirectory, "storage", "projects", job.ProjectId.ToString(), "jobs", job.VideoJobId.ToString(), "source");
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, safeFileName);
        await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);

        var asset = new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.SourceImage,
            ProviderName = "Upload",
            FileName = safeFileName,
            BlobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/source/{safeFileName}",
            PublicUrl = fullPath,
            MimeType = contentType,
            FileSize = fileBytes.LongLength,
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        job.PrimarySourceAssetId = asset.AssetId;
        job.InputMode = InputModeType.UploadedSingleImage;
        job.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}