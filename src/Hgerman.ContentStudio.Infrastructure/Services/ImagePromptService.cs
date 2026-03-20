using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ImagePromptService : IImagePromptService
{
    public Task<string> GenerateScenePromptAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default)
    {
        var prompt =
            $"Create a cinematic vertical 9:16 scene for a short video. " +
            $"Topic: {job.Topic ?? job.Title}. " +
            $"Scene text: {scene.SceneText}. " +
            $"Style: realistic, high detail, dramatic lighting, clean composition, social media ready.";

        return Task.FromResult(prompt);
    }
}