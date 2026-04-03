using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class YouTubeUploadService : IYouTubeUploadService
{
    private readonly YouTubeOptions _options;
    private readonly ILogger<YouTubeUploadService> _logger;

    public YouTubeUploadService(
        IOptions<YouTubeOptions> options,
        ILogger<YouTubeUploadService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadVideoAsync(
        string filePath,
        string title,
        string description,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("YouTube upload file not found.", filePath);
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecretsFilePath))
        {
            throw new InvalidOperationException("YouTube ClientSecretsFilePath is not configured.");
        }

        if (!File.Exists(_options.ClientSecretsFilePath))
        {
            throw new FileNotFoundException("YouTube client secrets file not found.", _options.ClientSecretsFilePath);
        }

        var dataStorePath = string.IsNullOrWhiteSpace(_options.CredentialDataStorePath)
            ? Path.Combine(AppContext.BaseDirectory, "youtube-token-store")
            : _options.CredentialDataStorePath;

        await using var stream = new FileStream(_options.ClientSecretsFilePath, FileMode.Open, FileAccess.Read);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { YouTubeService.Scope.YoutubeUpload },
            "user",
            cancellationToken,
            new FileDataStore(dataStorePath, true));

        var youtubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Hgerman Content Studio"
        });

        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = title,
                Description = description,
                Tags = tags.ToList(),
                CategoryId = string.IsNullOrWhiteSpace(_options.DefaultCategoryId) ? "22" : _options.DefaultCategoryId
            },
            Status = new VideoStatus
            {
                PrivacyStatus = string.IsNullOrWhiteSpace(_options.DefaultPrivacyStatus) ? "private" : _options.DefaultPrivacyStatus,
                SelfDeclaredMadeForKids = false
            }
        };

        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

        var insertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
        insertRequest.ChunkSize = ResumableUpload.MinimumChunkSize;

        var progress = await insertRequest.UploadAsync(cancellationToken);

        if (progress.Status != UploadStatus.Completed)
        {
            throw new InvalidOperationException($"YouTube upload failed. Status: {progress.Status}. Exception: {progress.Exception?.Message}");
        }

        var uploadedVideo = insertRequest.ResponseBody;
        if (uploadedVideo == null || string.IsNullOrWhiteSpace(uploadedVideo.Id))
        {
            throw new InvalidOperationException("YouTube upload completed but video id was not returned.");
        }

        _logger.LogInformation("YouTube upload completed. VideoId: {VideoId}", uploadedVideo.Id);

        return $"https://www.youtube.com/watch?v={uploadedVideo.Id}";
    }
}