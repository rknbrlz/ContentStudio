using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class ImageGenerationService : IImageGenerationService
{
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly IStorageService _storageService;
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(
        OpenAiApiClient openAiApiClient,
        IStorageService storageService,
        ILogger<ImageGenerationService> logger)
    {
        _openAiApiClient = openAiApiClient;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Asset> GenerateImageAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"scene-{scene.SceneNo:00}.png";
        var blobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/scenes/{fileName}";

        try
        {
            _logger.LogInformation(
                "IMAGE_START VideoJobId={VideoJobId}, SceneNo={SceneNo}, Prompt={Prompt}",
                job.VideoJobId,
                scene.SceneNo,
                scene.ScenePrompt);

            var imageBytes = await _openAiApiClient.GenerateImageAsync(
                scene.ScenePrompt,
                "1024x1536",
                "medium",
                cancellationToken);

            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"OpenAI image generation returned empty bytes for scene {scene.SceneNo}.");
            }

            var publicUrl = await _storageService.UploadBytesAsync(
                blobPath,
                imageBytes,
                "image/png",
                cancellationToken);

            _logger.LogInformation(
                "IMAGE_DONE VideoJobId={VideoJobId}, SceneNo={SceneNo}, Bytes={Bytes}, BlobPath={BlobPath}",
                job.VideoJobId,
                scene.SceneNo,
                imageBytes.Length,
                blobPath);

            return new Asset
            {
                VideoJobId = job.VideoJobId,
                VideoSceneId = scene.VideoSceneId,
                AssetType = AssetType.SceneImage,
                ProviderName = "OpenAI",
                FileName = fileName,
                BlobPath = blobPath,
                PublicUrl = publicUrl,
                MimeType = "image/png",
                FileSize = imageBytes.Length,
                Status = VideoJobStatus.Completed,
                CreatedDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "IMAGE_FAILED VideoJobId={VideoJobId}, SceneNo={SceneNo}",
                job.VideoJobId,
                scene.SceneNo);

            throw;
        }
    }
}