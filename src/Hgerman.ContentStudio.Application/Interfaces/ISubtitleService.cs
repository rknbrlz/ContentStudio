using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface ISubtitleService
{
    Task<Asset?> GenerateSubtitleAsync(
        VideoJob job,
        CancellationToken cancellationToken = default);
}