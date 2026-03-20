using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IScenePlannerService
{
    Task<List<VideoScene>> BuildScenesAsync(
        VideoJob job,
        string script,
        CancellationToken cancellationToken = default);
}