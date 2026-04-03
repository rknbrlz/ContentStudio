using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class TrendAnalysisService : ITrendAnalysisService
{
    public Task<List<TrendIdeaResult>> BuildTrendIdeasAsync(
        AutomationProfile profile,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TrendIdeaResult>();
        var keywords = SplitCsv(profile.TrendKeywordsCsv);
        var seeds = SplitCsv(profile.SeedTopicsCsv);

        if (keywords.Count == 0 && seeds.Count == 0)
        {
            seeds.Add(profile.TopicPrompt);
        }

        var index = 1;

        foreach (var keyword in keywords.DefaultIfEmpty("mindset"))
        {
            foreach (var seed in seeds.DefaultIfEmpty(profile.TopicPrompt).Take(5))
            {
                var score = Score(profile.GrowthMode, keyword, seed, index);

                results.Add(new TrendIdeaResult
                {
                    Keyword = keyword,
                    Title = $"{seed} - {keyword} angle",
                    Score = score,
                    SourceName = "internal-growth-engine",
                    Notes = $"growth:{profile.GrowthMode}; seed:{seed}"
                });

                index++;
            }
        }

        return Task.FromResult(results
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToList());
    }

    private static List<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new List<string>();

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Where(x => !string.IsNullOrWhiteSpace(x))
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToList();
    }

    private static decimal Score(string growthMode, string keyword, string seed, int index)
    {
        decimal baseScore = growthMode switch
        {
            "safe" => 58,
            "aggressive" => 72,
            _ => 64
        };

        baseScore += Math.Min(keyword.Length, 12);
        baseScore += Math.Min(seed.Length / 4m, 12m);
        baseScore -= index;

        return Math.Max(1, Math.Min(99, Math.Round(baseScore, 2)));
    }
}