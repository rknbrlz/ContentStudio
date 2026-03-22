using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Infrastructure.Data;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class TitleFeedbackService : ITitleFeedbackService
{
    private const int MaxTitleLength = 200;

    private readonly ContentStudioDbContext _db;

    public TitleFeedbackService(ContentStudioDbContext db)
    {
        _db = db;
    }

    public Task<List<TitleVariantResult>> GenerateVariantsAsync(
        AutomationProfile profile,
        string topic,
        string currentTitle,
        CancellationToken cancellationToken = default)
    {
        var hooks = new[]
        {
            "warning",
            "identity",
            "curiosity",
            "challenge",
            "emotional"
        };

        var patterns = new[]
        {
            "before-after",
            "mistake",
            "secret",
            "proof",
            "one-shift"
        };

        var results = new List<TitleVariantResult>();
        var count = Math.Max(1, profile.TitleTestVariants);

        var safeTopic = NormalizeTopic(topic);

        for (var i = 1; i <= count; i++)
        {
            var hook = hooks[(i - 1) % hooks.Length];
            var pattern = patterns[(i - 1) % patterns.Length];

            var candidate = BuildCandidateTitle(safeTopic, hook, pattern, i);
            candidate = TrimToLength(candidate, MaxTitleLength);

            var predicted = PredictScore(profile, hook, pattern, candidate);

            results.Add(new TitleVariantResult
            {
                VariantNo = i,
                Title = candidate,
                HookType = hook,
                PatternType = pattern,
                PredictedScore = predicted,
                IsWinner = false
            });
        }

        var winner = results.OrderByDescending(x => x.PredictedScore).First();
        winner.IsWinner = true;

        return Task.FromResult(results);
    }

    public async Task RecordSyntheticPerformanceAsync(
        int videoJobId,
        int automationProfileId,
        IEnumerable<TitleVariantResult> variants,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in variants)
        {
            var safeTitle = TrimToLength(item.Title, MaxTitleLength);

            _db.TitlePerformances.Add(new TitlePerformance
            {
                VideoJobId = videoJobId,
                AutomationProfileId = automationProfileId,
                OriginalTitle = safeTitle,
                CandidateTitle = safeTitle,
                VariantNo = item.VariantNo,
                HookType = TrimToLength(item.HookType, 100),
                PatternType = TrimToLength(item.PatternType, 100),
                PredictedScore = item.PredictedScore,
                IsWinner = item.IsWinner,
                CreatedDate = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeTopic(string topic)
    {
        var safeTopic = string.IsNullOrWhiteSpace(topic)
            ? "mindset"
            : topic.Trim();

        safeTopic = safeTopic
            .Replace("|", " ")
            .Replace("  ", " ");

        if (safeTopic.Length > 60)
        {
            safeTopic = safeTopic[..60].Trim();
            var lastSpace = safeTopic.LastIndexOf(' ');
            if (lastSpace > 20)
            {
                safeTopic = safeTopic[..lastSpace].Trim();
            }
        }

        return safeTopic;
    }

    private static string BuildCandidateTitle(string topic, string hook, string pattern, int no)
    {
        return hook switch
        {
            "warning" => $"Stop doing this if you want {topic}",
            "identity" => $"People with strong {topic} never ignore this",
            "curiosity" => $"The hidden reason your {topic} keeps failing",
            "challenge" => $"Try this 7-day {topic} reset",
            _ => $"This changed my {topic} more than motivation ever did"
        };
    }

    private static decimal PredictScore(AutomationProfile profile, string hook, string pattern, string title)
    {
        decimal score = 50m;

        score += profile.GrowthMode switch
        {
            "safe" => 4m,
            "aggressive" => 10m,
            _ => 7m
        };

        score += hook switch
        {
            "curiosity" => 12m,
            "warning" => 10m,
            "emotional" => 9m,
            "challenge" => 8m,
            _ => 7m
        };

        score += pattern switch
        {
            "secret" => 10m,
            "mistake" => 9m,
            "before-after" => 8m,
            _ => 6m
        };

        if (title.Length is >= 35 and <= 65)
            score += 6m;

        return Math.Max(1m, Math.Min(99m, Math.Round(score, 2)));
    }

    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = value.Trim();

        if (cleaned.Length <= maxLength)
            return cleaned;

        var shortened = cleaned[..maxLength].Trim();
        var lastSpace = shortened.LastIndexOf(' ');

        if (lastSpace > 20)
            shortened = shortened[..lastSpace].Trim();

        return shortened;
    }
}