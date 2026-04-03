using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IVoiceGenerationService
{
    Task<Asset?> GenerateVoiceAsync(VideoJob job, CancellationToken cancellationToken = default);
}