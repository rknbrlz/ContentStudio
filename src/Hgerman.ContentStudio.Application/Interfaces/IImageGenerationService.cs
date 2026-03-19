using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IImageGenerationService
{
    Task<Asset> GenerateImageAsync(VideoJob job, VideoScene scene, CancellationToken cancellationToken = default);
}
