using System.Globalization;
using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class SubtitleService : ISubtitleService
{
    private readonly IStorageService _storageService;
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly ILogger<SubtitleService> _logger;

    public SubtitleService(
        IStorageService storageService,
        OpenAiApiClient openAiApiClient,
        ILogger<SubtitleService> logger)
    {
        _storageService = storageService;
        _openAiApiClient = openAiApiClient;
        _logger = logger;
    }

    public async Task<Asset?> GenerateSubtitleAsync(
        VideoJob job,
        CancellationToken cancellationToken = default)
    {
        if (!job.SubtitleEnabled || job.Scenes.Count == 0)
        {
            return null;
        }

        var orderedScenes = job.Scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        foreach (var scene in orderedScenes)
        {
            var voiceText = GetVoiceText(scene);

            if (string.IsNullOrWhiteSpace(scene.SubtitleText))
            {
                scene.SubtitleText = await BuildSubtitleBurstAsync(
                    voiceText,
                    NormalizeLanguageCode(job.LanguageCode),
                    "en",
                    cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(scene.SubtitleText))
            {
                scene.SubtitleText = "Start now";
            }

            scene.SceneText = voiceText;
            scene.UpdatedDate = DateTime.UtcNow;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < orderedScenes.Count; i++)
        {
            var scene = orderedScenes[i];
            var subtitleText = CleanSubtitleText(scene.SubtitleText);

            if (string.IsNullOrWhiteSpace(subtitleText))
            {
                subtitleText = "Start now";
            }

            builder.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            builder.AppendLine($"{ToSrtTime(scene.StartSecond)} --> {ToSrtTime(scene.EndSecond)}");
            builder.AppendLine(subtitleText);
            builder.AppendLine();
        }

        var content = builder.ToString();
        var blobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/subtitles/subtitles.srt";
        var publicUrl = await _storageService.UploadTextAsync(
            blobPath,
            content,
            "application/x-subrip",
            cancellationToken);

        return new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.SubtitleSrt,
            ProviderName = _openAiApiClient.IsConfigured ? "OpenAIShortSubtitleBuilder" : "FallbackShortSubtitleBuilder",
            FileName = "subtitles.srt",
            BlobPath = blobPath,
            PublicUrl = publicUrl,
            MimeType = "application/x-subrip",
            FileSize = Encoding.UTF8.GetByteCount(content),
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
    }

    private async Task<string> BuildSubtitleBurstAsync(
        string voiceText,
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voiceText))
        {
            return string.Empty;
        }

        if (_openAiApiClient.IsConfigured)
        {
            try
            {
                var systemPrompt = """
You are generating ultra-short TikTok subtitle bursts.

STRICT RULES:
- English only
- MAX 2 words
- Prefer 1 or 2 words
- NEVER output a full sentence
- NEVER explain
- NO punctuation unless absolutely necessary
- Use only punchy key words
- Mobile-first subtitle style
- Return only the subtitle burst

Examples:
Start now
No excuses
Stay focused
Keep going
One step
Your time
""";

                var userPrompt = $"""
Source language: {LanguageName(sourceLanguageCode)}
Target language: {LanguageName(targetLanguageCode)}

Convert this spoken line into a TikTok-style subtitle burst:
{voiceText}
""";

                var translated = await _openAiApiClient.GenerateStructuredTextAsync(
                    systemPrompt,
                    userPrompt,
                    cancellationToken);

                return CleanSubtitleText(translated);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Subtitle AI generation failed.");
            }
        }

        return BuildFallbackBurst(voiceText, sourceLanguageCode);
    }

    private static string GetVoiceText(VideoScene scene)
    {
        if (!string.IsNullOrWhiteSpace(scene.VoiceText))
        {
            return scene.VoiceText.Trim();
        }

        return scene.SceneText?.Trim() ?? string.Empty;
    }

    private static string CleanSubtitleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        var words = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToArray();

        return string.Join(" ", words).Trim();
    }

    private static string BuildFallbackBurst(string voiceText, string sourceLanguageCode)
    {
        var key = voiceText.ToLowerInvariant();

        if (sourceLanguageCode == "pl")
        {
            if (key.Contains("zacznij")) return "Start now";
            if (key.Contains("teraz")) return "Right now";
            if (key.Contains("dyscypl")) return "Stay disciplined";
            if (key.Contains("skup")) return "Stay focused";
            if (key.Contains("przysz")) return "Your future";
            if (key.Contains("nie czekaj")) return "No waiting";
        }

        return "Keep going";
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "en";
        }

        return languageCode.Trim().ToLowerInvariant() switch
        {
            "pl" or "pl-pl" => "pl",
            _ => "en"
        };
    }

    private static string LanguageName(string languageCode) => languageCode switch
    {
        "pl" => "Polish",
        _ => "English"
    };

    private static string ToSrtTime(decimal seconds)
    {
        var wholeMilliseconds = (int)Math.Round(seconds * 1000m, MidpointRounding.AwayFromZero);
        var ts = TimeSpan.FromMilliseconds(wholeMilliseconds);
        return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00},{ts.Milliseconds:000}";
    }
}