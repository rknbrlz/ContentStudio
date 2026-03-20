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
        if (string.IsNullOrWhiteSpace(scene.ScenePrompt))
        {
            throw new InvalidOperationException($"ScenePrompt is empty for scene {scene.SceneNo}.");
        }

        byte[] imageBytes;
        string providerName;

        try
        {
            if (_openAiApiClient.IsConfigured)
            {
                imageBytes = await _openAiApiClient.GenerateImageAsync(
                    scene.ScenePrompt,
                    "1024x1536",
                    "medium",
                    cancellationToken);

                providerName = "OpenAI";
            }
            else
            {
                throw new InvalidOperationException("OpenAI is not configured for image generation.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Image generation failed for VideoJobId {VideoJobId}, SceneNo {SceneNo}.",
                job.VideoJobId,
                scene.SceneNo);

            throw;
        }

        var fileName = $"scene_{scene.SceneNo:00}.png";
        var blobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/scenes/{fileName}";
        var publicUrl = await _storageService.UploadBytesAsync(
            blobPath,
            imageBytes,
            "image/png",
            cancellationToken);

        return new Asset
        {
            VideoJobId = job.VideoJobId,
            VideoSceneId = scene.VideoSceneId,
            AssetType = AssetType.SceneImage,
            ProviderName = providerName,
            FileName = fileName,
            BlobPath = blobPath,
            PublicUrl = publicUrl,
            MimeType = "image/png",
            FileSize = imageBytes.LongLength,
            Width = 1024,
            Height = 1536,
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
    }
}