using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class PublishService : IPublishService
{
    private readonly ContentStudioDbContext _db;
    private readonly IStorageService _storageService;
    private readonly IUploadMetadataService _uploadMetadataService;
    private readonly IYouTubeUploadService _youTubeUploadService;
    private readonly ILogger<PublishService> _logger;

    public PublishService(
        ContentStudioDbContext db,
        IStorageService storageService,
        IUploadMetadataService uploadMetadataService,
        IYouTubeUploadService youTubeUploadService,
        ILogger<PublishService> logger)
    {
        _db = db;
        _storageService = storageService;
        _uploadMetadataService = uploadMetadataService;
        _youTubeUploadService = youTubeUploadService;
        _logger = logger;
    }

    public Task<PublishTask> CreateDraftAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        var tags = string.Join(",", new[]
        {
            job.LanguageCode,
            job.PlatformType.ToString(),
            job.ToneType.ToString(),
            "AI Short"
        });

        var description = new StringBuilder()
            .AppendLine(job.Title)
            .AppendLine()
            .AppendLine($"Language: {job.LanguageCode}")
            .AppendLine($"Tone: {job.ToneType}")
            .AppendLine($"Generated from Hgerman Content Studio job {job.JobNo}.")
            .ToString();

        var draft = new PublishTask
        {
            VideoJobId = job.VideoJobId,
            PlatformType = job.PlatformType,
            Title = job.Title,
            Description = description,
            Tags = tags,
            PublishStatus = PublishStatus.Draft,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        return Task.FromResult(draft);
    }

    public async Task<string> PublishToYouTubeAsync(int videoJobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.VideoJobs
            .Include(x => x.Assets)
            .FirstOrDefaultAsync(x => x.VideoJobId == videoJobId, cancellationToken);

        if (job is null)
        {
            throw new InvalidOperationException($"Video job not found. Id={videoJobId}");
        }

        var finalAsset = job.Assets
            .Where(x => x.AssetType == AssetType.FinalVideo)
            .OrderByDescending(x => x.AssetId)
            .FirstOrDefault();

        if (finalAsset is null)
        {
            throw new InvalidOperationException("Final video asset not found.");
        }

        if (string.IsNullOrWhiteSpace(finalAsset.BlobPath))
        {
            throw new InvalidOperationException("Final video BlobPath is empty.");
        }

        var localFilePath = await _storageService.GetLocalPathAsync(finalAsset.BlobPath, cancellationToken);

        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            throw new FileNotFoundException("Final video file not found for YouTube upload.", localFilePath);
        }

        var metadata = await _uploadMetadataService.GenerateYouTubeMetadataAsync(job, cancellationToken);

        var youtubeUrl = await _youTubeUploadService.UploadVideoAsync(
            localFilePath,
            metadata.Title,
            metadata.Description,
            metadata.Tags,
            cancellationToken);

        var publishTask = await CreateDraftAsync(job, cancellationToken);
        publishTask.Title = metadata.Title;
        publishTask.Description = metadata.Description;
        publishTask.Tags = string.Join(",", metadata.Tags);
        publishTask.PublishStatus = PublishStatus.Published;
        publishTask.CreatedDate = DateTime.UtcNow;
        publishTask.UpdatedDate = DateTime.UtcNow;

        _db.PublishTasks.Add(publishTask);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Video job {VideoJobId} uploaded to YouTube successfully. Url: {YouTubeUrl}",
            videoJobId,
            youtubeUrl);

        return youtubeUrl;
    }
}