using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class ImagePromptService : IImagePromptService
{
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly ILogger<ImagePromptService> _logger;

    public ImagePromptService(
        OpenAiApiClient openAiApiClient,
        ILogger<ImagePromptService> logger)
    {
        _openAiApiClient = openAiApiClient;
        _logger = logger;
    }

    public async Task<string> GenerateScenePromptAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = """
You create short, cinematic image prompts for vertical short-form videos.
Return only one single visual prompt.
Do not add explanation.
Do not use bullet points.
Do not mention copyrighted scenes or actor names.
Keep it concise, vivid, and highly visual.
""";

        var userPrompt = $"""
Create one cinematic visual prompt for a vertical 9:16 short video scene.

Video topic: {job.Topic}
Language: {job.LanguageCode}
Platform: {job.PlatformType}
Tone: {job.ToneType}

Scene text:
{scene.SceneText}

Rules:
- Make it feel cinematic and emotional
- No actor likeness
- No copyrighted scene recreation
- 1 sentence only
- Focus on setting, mood, lighting, composition, and atmosphere
- Suitable for AI image generation
""";

        try
        {
            var result = await _openAiApiClient.GenerateStructuredTextAsync(
                systemPrompt,
                userPrompt,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
            {
                return BuildFallbackPrompt(job, scene);
            }

            return result.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Prompt generation failed for VideoJobId {VideoJobId}, Scene {SceneNo}. Using fallback prompt.",
                job.VideoJobId,
                scene.SceneNo);

            return BuildFallbackPrompt(job, scene);
        }
    }

    private static string BuildFallbackPrompt(VideoJob job, VideoScene scene)
    {
        return $"Cinematic vertical 9:16 scene, emotional atmosphere, dramatic lighting, high detail, {job.ToneType} mood, inspired by: {scene.SceneText}";
    }
}