using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class ScriptGenerationService : IScriptGenerationService
{
    private readonly IHookGenerationService _hookGenerationService;
    private readonly ILogger<ScriptGenerationService> _logger;

    public ScriptGenerationService(
        IHookGenerationService hookGenerationService,
        ILogger<ScriptGenerationService> logger)
    {
        _hookGenerationService = hookGenerationService;
        _logger = logger;
    }

    public async Task<string> GenerateScriptAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = string.IsNullOrWhiteSpace(job.Topic)
            ? job.Title
            : job.Topic;

        var hook = await _hookGenerationService.GenerateHookAsync(
            topic ?? "Motivation",
            job.LanguageCode ?? "en",
            cancellationToken);

        var lines = BuildLines(job.LanguageCode ?? "en", topic ?? "Motivation");

        var scriptLines = new List<string> { hook };
        scriptLines.AddRange(lines);

        var script = string.Join(Environment.NewLine, scriptLines);

        _logger.LogInformation("Script generated for VideoJobId {VideoJobId}", job.VideoJobId);

        return script;
    }

    private static IReadOnlyList<string> BuildLines(string languageCode, string topic)
    {
        return languageCode.ToLowerInvariant() switch
        {
            "pl" => new[]
            {
                $"Każda porażka czegoś cię uczy.",
                "To, co dziś boli, jutro da ci siłę.",
                "Dyscyplina tworzy mistrza.",
                "Nie zatrzymuj się, nawet gdy jest ciężko.",
                "Sukces przychodzi do tych, którzy wytrwają.",
                "Twój czas zaczyna się teraz."
            },
            "tr" => new[]
            {
                "Her düşüş sana bir şey öğretir.",
                "Bugün canını yakan şey yarın seni güçlendirir.",
                "Disiplin kazananları oluşturur.",
                "Zor olsa da yürümeye devam et.",
                "Başarı vazgeçmeyenlere gelir.",
                "Senin zamanın şimdi başlıyor."
            },
            _ => new[]
            {
                "Every fall teaches you something.",
                "What hurts today can make you stronger tomorrow.",
                "Discipline builds winners.",
                "Keep moving even when it feels hard.",
                "Success comes to those who stay.",
                "Your time starts now."
            }
        };
    }
}