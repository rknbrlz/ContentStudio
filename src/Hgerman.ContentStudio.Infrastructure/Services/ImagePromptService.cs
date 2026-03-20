using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class ImagePromptService : IImagePromptService
{
    public Task<string> GenerateScenePromptAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default)
    {
        var prompt =
            $"Create a HIGH-IMPACT viral short video scene. " +
            $"Vertical 9:16. " +
            $"Ultra realistic. " +
            $"Strong contrast. " +
            $"Dramatic lighting. " +
            $"Clean composition. " +
            $"Center focus. " +
            $"High detail. " +
            $"Dynamic composition. " +
            $"Attention grabbing. " +
            $"Add motion feeling even in still image. " +
            $"Topic: {job.Topic ?? job.Title}. " +
            $"Scene text: {scene.SceneText}. " +
            $"Style: TikTok / YouTube Shorts viral style.";

        return Task.FromResult(prompt);
    }
}