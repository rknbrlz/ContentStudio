using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ScriptGenerationService : IScriptGenerationService
{
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly ILogger<ScriptGenerationService> _logger;

    public ScriptGenerationService(
        OpenAiApiClient openAiApiClient,
        ILogger<ScriptGenerationService> logger)
    {
        _openAiApiClient = openAiApiClient;
        _logger = logger;
    }

    public async Task<string> GenerateScriptAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        var topic = string.IsNullOrWhiteSpace(job.Topic) ? job.Title : job.Topic;
        if (!_openAiApiClient.IsConfigured)
        {
            _logger.LogWarning("OpenAI key not configured. Returning fallback script for VideoJobId {VideoJobId}", job.VideoJobId);
            return BuildFallbackScript(topic!);
        }

        var systemPrompt = """
You are a short-form video scriptwriter.
Return only the final narration text.
Requirements:
- Write for vertical short-form video.
- Use short, punchy lines.
- Include a strong first 3-second hook.
- Keep it natural for voiceover.
- No markdown.
- No scene labels.
- No bullet points.
- End with a soft CTA.
""";

        var userPrompt = $"""
Create a {job.DurationTargetSec}-second script in language '{job.LanguageCode}'.
Title: {job.Title}
Topic: {topic}
Tone: {job.ToneType}
Platform: {job.PlatformType}
Extra instructions: {job.SourcePrompt ?? "none"}
Keep total narration concise and emotionally engaging.
""";

        return await _openAiApiClient.GenerateStructuredTextAsync(systemPrompt, userPrompt, cancellationToken);
    }

    private static string BuildFallbackScript(string topic)
    {
        return $"""
{topic} is not built in a single day.
Most people wait to feel ready.
But readiness comes after the first step.
Every small action proves to your mind that change is possible.
Start before you feel perfect.
Stay consistent when nobody is watching.
A year from now, you will thank yourself for beginning today.
Follow for more.
""";
    }
}
