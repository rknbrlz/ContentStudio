using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IVideoRenderService
{
    Task<string> RenderFinalVideoAsync(
        VideoJob job,
        IReadOnlyCollection<VideoScene> scenes,
        string scenesFolderPath,
        string? audioFilePath,
        string? subtitleFilePath,
        string outputFolderPath,
        CancellationToken cancellationToken = default);
}