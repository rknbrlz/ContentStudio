using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class ScriptGenerationService : IScriptGenerationService
{
    private readonly IHookGenerationService _hookGenerationService;
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly ILogger<ScriptGenerationService> _logger;

    public ScriptGenerationService(
        IHookGenerationService hookGenerationService,
        OpenAiApiClient openAiApiClient,
        ILogger<ScriptGenerationService> logger)
    {
        _hookGenerationService = hookGenerationService;
        _openAiApiClient = openAiApiClient;
        _logger = logger;
    }

    public async Task<string> GenerateScriptAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = string.IsNullOrWhiteSpace(job.Topic) ? job.Title : job.Topic;
        var voiceLanguageCode = NormalizeVoiceLanguageCode(job.LanguageCode);

        if (_openAiApiClient.IsConfigured)
        {
            try
            {
                var systemPrompt = BuildSystemPrompt(voiceLanguageCode);
                var userPrompt = BuildUserPrompt(job, topic ?? "Motivation", voiceLanguageCode);

                var aiScript = await _openAiApiClient.GenerateStructuredTextAsync(
                    systemPrompt,
                    userPrompt,
                    cancellationToken);

                var normalized = NormalizeScript(aiScript);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI script generation failed. Using fallback.");
            }
        }

        var hook = await _hookGenerationService.GenerateHookAsync(
            topic ?? "Motivation",
            voiceLanguageCode,
            cancellationToken);

        var lines = BuildFallbackLines(voiceLanguageCode);
        return string.Join(Environment.NewLine, new[] { hook }.Concat(lines));
    }

    private static string BuildSystemPrompt(string languageCode)
    {
        var languageName = LanguageName(languageCode);

        return $"""
You are writing a short-form motivational spoken script.

STRICT RULES:
- Output MUST be only in {languageName}
- No English if language is Polish or Turkish
- No bilingual output
- 6 to 8 short spoken lines
- Strong hook first
- Calm but motivating
- Student relatable
- No subtitle instructions
- No formatting
- Return plain lines only
""";
    }

    private static string BuildUserPrompt(VideoJob job, string topic, string languageCode)
    {
        return $"""
Create the VOICE script only.

Topic: {topic}
Duration target: {job.DurationTargetSec} seconds
Platform: {job.PlatformType}
Tone: {job.ToneType}
Voice language: {LanguageName(languageCode)}

The spoken script must be natural, short, emotional and mobile-friendly.
""";
    }

    private static IReadOnlyList<string> BuildFallbackLines(string languageCode)
    {
        return languageCode switch
        {
            "pl" => new[]
            {
                "Nie czekaj na idealny moment.",
                "Masz czas tylko teraz.",
                "Jedna godzina skupienia zmienia wszystko.",
                "Dyscyplina wygrywa z wymówkami.",
                "Zrób pierwszy krok dzisiaj.",
                "Twoja przyszłość zaczyna się teraz."
            },
            _ => new[]
            {
                "Do not wait for the perfect moment.",
                "Your time is now.",
                "One focused hour changes everything.",
                "Discipline beats excuses.",
                "Take the first step today.",
                "Your future starts now."
            }
        };
    }

    private static string NormalizeScript(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lines = value
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeVoiceLanguageCode(string? languageCode)
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
}