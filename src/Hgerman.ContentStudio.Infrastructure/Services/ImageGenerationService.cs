using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ImageGenerationService : IImageGenerationService
{
    public Task<Asset> GenerateImageAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default)
    {
        var asset = new Asset
        {
            VideoJobId = job.VideoJobId,
            VideoSceneId = scene.VideoSceneId,
            AssetType = AssetType.SceneImage,
            ProviderName = "Placeholder",
            FileName = $"scene_{scene.SceneNo}.jpg",
            BlobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/scenes/scene_{scene.SceneNo}.jpg",
            PublicUrl = null,
            MimeType = "image/jpeg",
            FileSize = 0,
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        return Task.FromResult(asset);
    }
}