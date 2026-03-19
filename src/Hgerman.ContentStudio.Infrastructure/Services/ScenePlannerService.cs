using System.Text.Json;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ScenePlannerService : IScenePlannerService
{
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly ILogger<ScenePlannerService> _logger;

    public ScenePlannerService(
        OpenAiApiClient openAiApiClient,
        ILogger<ScenePlannerService> logger)
    {
        _openAiApiClient = openAiApiClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VideoScene>> BuildScenesAsync(VideoJob job, string script, CancellationToken cancellationToken = default)
    {
        if (!_openAiApiClient.IsConfigured)
        {
            return BuildFallbackScenes(job, script);
        }

        var systemPrompt = """
Break a short-form narration into video scenes.
Return pure JSON only.
Schema:
[
  {
    "sceneNo": 1,
    "text": "narration for this scene",
    "durationSec": 6,
    "transition": "fade"
  }
]
Rules:
- 5 to 8 scenes.
- durationSec must be integer.
- Sum should be close to requested target duration.
- text should be concise and natural.
""";

        var userPrompt = $"""
Requested duration: {job.DurationTargetSec} seconds
Language: {job.LanguageCode}
Title: {job.Title}
Topic: {job.Topic}
Prompt: {job.SourcePrompt}
Script:
{script}
""";

        if (job.InputMode == InputModeType.UploadedSingleImage)
        {
            userPrompt += """

Additional instruction:
- This video will use one uploaded product image as the visual source.
- Do not assume multiple different visuals.
- Plan scenes that can work with the same image reused using zoom, crop, pan, and text overlays.
- Keep scenes suitable for premium ecommerce / product showcase style.
- Make narration concise and visually reusable.
""";
        }

        try
        {
            var json = await _openAiApiClient.GenerateStructuredTextAsync(systemPrompt, userPrompt, cancellationToken);
            var scenes = ParseScenesFromJson(json, job.DurationTargetSec);
            if (scenes.Count > 0)
            {
                return scenes;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scene planning with OpenAI failed. Falling back to deterministic split.");
        }

        return BuildFallbackScenes(job, script);
    }

    private static List<VideoScene> ParseScenesFromJson(string json, int targetDurationSec)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var scenes = new List<VideoScene>();
        decimal cursor = 0m;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var sceneNo = item.TryGetProperty("sceneNo", out var sceneNoElement) ? sceneNoElement.GetInt32() : scenes.Count + 1;
            var text = item.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
            var duration = item.TryGetProperty("durationSec", out var durationElement) ? durationElement.GetInt32() : Math.Max(4, targetDurationSec / 6);
            var transition = item.TryGetProperty("transition", out var transitionElement) ? transitionElement.GetString() : "fade";

            var start = cursor;
            var end = cursor + duration;
            scenes.Add(new VideoScene
            {
                SceneNo = sceneNo,
                SceneText = text.Trim(),
                StartSecond = start,
                EndSecond = end,
                DurationSecond = duration,
                TransitionType = transition,
                Status = VideoJobStatus.Queued,
                CreatedDate = DateTime.UtcNow
            });

            cursor = end;
        }

        return scenes.OrderBy(x => x.SceneNo).ToList();
    }

    private static IReadOnlyList<VideoScene> BuildFallbackScenes(VideoJob job, string script)
    {
        var parts = script
            .Split(new[] { '.', '!', '?', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (parts.Count == 0)
        {
            parts.Add(job.Title);
        }

        var desiredCount = Math.Clamp(parts.Count, 4, 7);
        if (parts.Count > desiredCount)
        {
            parts = parts.Take(desiredCount).ToList();
        }

        while (parts.Count < desiredCount)
        {
            parts.Add(parts.Last());
        }

        var sceneDuration = Math.Max(4, job.DurationTargetSec / desiredCount);
        var scenes = new List<VideoScene>();
        decimal cursor = 0m;

        for (var i = 0; i < parts.Count; i++)
        {
            var start = cursor;
            var end = cursor + sceneDuration;
            scenes.Add(new VideoScene
            {
                SceneNo = i + 1,
                SceneText = parts[i].Trim(),
                StartSecond = start,
                EndSecond = end,
                DurationSecond = sceneDuration,
                TransitionType = "fade",
                Status = VideoJobStatus.Queued,
                CreatedDate = DateTime.UtcNow
            });
            cursor = end;
        }

        return scenes;
    }
}