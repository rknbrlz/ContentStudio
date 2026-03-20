using Hgerman.ContentStudio.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class HookGenerationService : IHookGenerationService
{
    private readonly ILogger<HookGenerationService> _logger;

    public HookGenerationService(ILogger<HookGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateHookAsync(
        string topic,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedTopic = string.IsNullOrWhiteSpace(topic)
            ? "motivation"
            : topic.Trim();

        var hook = languageCode?.ToLowerInvariant() switch
        {
            "pl" => BuildPolishHook(normalizedTopic),
            "tr" => BuildTurkishHook(normalizedTopic),
            _ => BuildEnglishHook(normalizedTopic)
        };

        _logger.LogInformation("Hook generated for topic {Topic} in language {LanguageCode}", normalizedTopic, languageCode);

        return Task.FromResult(hook);
    }

    private static string BuildPolishHook(string topic)
    {
        var hooks = new[]
        {
            "Nie przewijaj. To jest o tobie.",
            "To może zmienić twój dzień.",
            "Prawda, której dziś potrzebujesz.",
            "Jeszcze nie skończyłeś swojej drogi.",
            "Twój moment zaczyna się teraz."
        };

        return PickHook(topic, hooks);
    }

    private static string BuildTurkishHook(string topic)
    {
        var hooks = new[]
        {
            "Dur. Bu tam sana göre.",
            "Bugün bunu duyman gerekiyordu.",
            "Henüz bitmedi, daha yeni başlıyor.",
            "Kendini küçümsemeyi bırak.",
            "Senin zamanın şimdi başlıyor."
        };

        return PickHook(topic, hooks);
    }

    private static string BuildEnglishHook(string topic)
    {
        var hooks = new[]
        {
            "Stop scrolling. This is about you.",
            "You needed this today.",
            "Your story is not over.",
            "You are closer than you think.",
            "Your time starts now."
        };

        return PickHook(topic, hooks);
    }

    private static string PickHook(string topic, IReadOnlyList<string> hooks)
    {
        if (hooks.Count == 0)
        {
            return "Your time starts now.";
        }

        var index = Math.Abs(topic.GetHashCode()) % hooks.Count;
        return hooks[index];
    }
}