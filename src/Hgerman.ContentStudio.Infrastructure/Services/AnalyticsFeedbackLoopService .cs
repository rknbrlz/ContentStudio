using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class AnalyticsFeedbackLoopService : IAnalyticsFeedbackLoopService
{
    private readonly ContentStudioDbContext _db;

    public AnalyticsFeedbackLoopService(ContentStudioDbContext db)
    {
        _db = db;
    }

    public async Task<int> EvaluateAllAsync(CancellationToken cancellationToken = default)
    {
        var profileIds = await _db.AutomationProfiles
            .Where(x => x.IsActive)
            .Select(x => x.AutomationProfileId)
            .ToListAsync(cancellationToken);

        var total = 0;

        foreach (var profileId in profileIds)
        {
            total += await EvaluateProfileAsync(profileId, cancellationToken);
        }

        return total;
    }

    public async Task<int> EvaluateProfileAsync(int automationProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == automationProfileId, cancellationToken);

        if (profile == null)
            return 0;

        var recentTitles = await _db.TitlePerformances
            .Where(x => x.AutomationProfileId == automationProfileId)
            .OrderByDescending(x => x.CreatedDate)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (recentTitles.Count == 0)
            return 0;

        var avgPredicted = recentTitles.Average(x => x.PredictedScore);
        var created = 0;

        if (avgPredicted < profile.MinSuccessScore)
        {
            _db.AutomationFeedbacks.Add(new AutomationFeedback
            {
                AutomationProfileId = automationProfileId,
                FeedbackType = "title",
                Signal = "weak_hook",
                ScoreValue = avgPredicted,
                Summary = $"Average predicted title score is {avgPredicted:F2}, below threshold {profile.MinSuccessScore:F2}.",
                SuggestedAction = "Strengthen curiosity/warning hooks and shorten title length."
            });
            created++;
        }
        else
        {
            _db.AutomationFeedbacks.Add(new AutomationFeedback
            {
                AutomationProfileId = automationProfileId,
                FeedbackType = "title",
                Signal = "healthy_pattern",
                ScoreValue = avgPredicted,
                Summary = $"Average predicted title score is {avgPredicted:F2}, above threshold.",
                SuggestedAction = "Keep current hook pattern and increase trend exploration."
            });
            created++;
        }

        var bestTrend = await _db.TrendSnapshots
            .Where(x => x.AutomationProfileId == automationProfileId)
            .OrderByDescending(x => x.TrendScore)
            .FirstOrDefaultAsync(cancellationToken);

        if (bestTrend != null)
        {
            _db.AutomationFeedbacks.Add(new AutomationFeedback
            {
                AutomationProfileId = automationProfileId,
                FeedbackType = "trend",
                Signal = "top_trend",
                ScoreValue = bestTrend.TrendScore,
                Summary = $"Top trend keyword is '{bestTrend.Keyword}' with score {bestTrend.TrendScore:F2}.",
                SuggestedAction = $"Produce more variations around '{bestTrend.Keyword}'."
            });
            created++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return created;
    }
}