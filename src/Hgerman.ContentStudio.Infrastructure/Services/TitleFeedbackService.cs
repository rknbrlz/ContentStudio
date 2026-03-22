using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Infrastructure.Data;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class TitleFeedbackService : ITitleFeedbackService
{
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

        for (var i = 1; i <= count; i++)
        {
            var hook = hooks[(i - 1) % hooks.Length];
            var pattern = patterns[(i - 1) % patterns.Length];

            var candidate = BuildCandidateTitle(topic, hook, pattern, i);
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
            _db.TitlePerformances.Add(new TitlePerformance
            {
                VideoJobId = videoJobId,
                AutomationProfileId = automationProfileId,
                OriginalTitle = item.Title,
                CandidateTitle = item.Title,
                VariantNo = item.VariantNo,
                HookType = item.HookType,
                PatternType = item.PatternType,
                PredictedScore = item.PredictedScore,
                IsWinner = item.IsWinner,
                CreatedDate = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildCandidateTitle(string topic, string hook, string pattern, int no)
    {
        var safeTopic = string.IsNullOrWhiteSpace(topic) ? "mindset" : topic.Trim();

        return hook switch
        {
            "warning" => $"Stop doing this if you want {safeTopic}",
            "identity" => $"People with strong {safeTopic} never ignore this",
            "curiosity" => $"The hidden reason your {safeTopic} keeps failing",
            "challenge" => $"Try this 7-day {safeTopic} reset",
            _ => $"This changed my {safeTopic} more than motivation ever did"
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
}