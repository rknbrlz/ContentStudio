using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class TitleOptimizationService : ITitleOptimizationService
{
    private readonly IScriptGenerationService _scriptGenerationService;
    private readonly ILogger<TitleOptimizationService> _logger;

    public TitleOptimizationService(
        IScriptGenerationService scriptGenerationService,
        ILogger<TitleOptimizationService> logger)
    {
        _scriptGenerationService = scriptGenerationService;
        _logger = logger;
    }

    public async Task<string> GenerateTitleAsync(
        string topic,
        string languageCode,
        string? hookTemplate,
        string? viralPatternTemplate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = BuildPromptJob(
                title: "TITLE_GENERATION",
                topic: topic,
                languageCode: languageCode,
                sourcePrompt: $"""
Generate one high-conversion short video title.

Language: {languageCode}
Topic: {topic}
Hook Template: {hookTemplate}
Viral Pattern: {viralPatternTemplate}

Rules:
- Maximum 70 characters
- Curiosity-driven
- Emotional but not spammy
- Suitable for YouTube Shorts / TikTok style
- Return only the title text
""");

            var result = await _scriptGenerationService.GenerateScriptAsync(job, cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
                return BuildFallbackTitle(topic);

            var clean = NormalizeSingleLine(result);

            return string.IsNullOrWhiteSpace(clean)
                ? BuildFallbackTitle(topic)
                : clean;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GenerateTitleAsync failed. Fallback title will be used.");
            return BuildFallbackTitle(topic);
        }
    }

    public async Task<string> GenerateDescriptionAsync(
        string topic,
        string title,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var job = BuildPromptJob(
                title: "DESCRIPTION_GENERATION",
                topic: topic,
                languageCode: languageCode,
                sourcePrompt: $"""
Write a short YouTube Shorts description.

Language: {languageCode}
Topic: {topic}
Title: {title}

Rules:
- 2 short lines
- add 3 to 5 relevant hashtags
- return plain text only
""");

            var result = await _scriptGenerationService.GenerateScriptAsync(job, cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
                return BuildFallbackDescription(title);

            return result.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GenerateDescriptionAsync failed. Fallback description will be used.");
            return BuildFallbackDescription(title);
        }
    }

    private static VideoJob BuildPromptJob(
        string title,
        string topic,
        string languageCode,
        string sourcePrompt)
    {
        return new VideoJob
        {
            ProjectId = 0,
            JobNo = $"TMP-{Guid.NewGuid():N}".Substring(0, 20),
            Title = title,
            Topic = topic,
            SourcePrompt = sourcePrompt,
            LanguageCode = languageCode,
            PlatformType = PlatformType.YouTubeShorts,
            ToneType = ToneType.Inspirational,
            DurationTargetSec = 30,
            AspectRatio = AspectRatioType.Vertical916,
            SubtitleEnabled = false,
            ThumbnailEnabled = false
        };
    }

    private static string NormalizeSingleLine(string value)
    {
        var cleaned = value.Trim().Trim('"').Trim();
        cleaned = cleaned.Replace("\r", " ").Replace("\n", " ").Trim();

        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");

        return cleaned;
    }

    private static string BuildFallbackTitle(string topic)
    {
        var safeTopic = string.IsNullOrWhiteSpace(topic) ? "motivation" : topic.Trim();
        return $"Watch this before you quit | {safeTopic}";
    }

    private static string BuildFallbackDescription(string title)
    {
        return $"{title}{Environment.NewLine}{Environment.NewLine}#shorts #motivation #mindset";
    }
}