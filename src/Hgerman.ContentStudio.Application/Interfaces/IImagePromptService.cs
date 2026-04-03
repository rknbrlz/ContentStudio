using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IImagePromptService
{
    Task<string> GenerateScenePromptAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default);
}