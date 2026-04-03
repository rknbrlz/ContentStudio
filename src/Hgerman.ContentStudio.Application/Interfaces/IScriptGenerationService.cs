using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IScriptGenerationService
{
    Task<string> GenerateScriptAsync(VideoJob job, CancellationToken cancellationToken = default);
}