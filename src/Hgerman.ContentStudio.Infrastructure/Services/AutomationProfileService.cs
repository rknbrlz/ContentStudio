using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class AutomationProfileService : IAutomationProfileService
{
    private readonly ContentStudioDbContext _db;

    public AutomationProfileService(ContentStudioDbContext db)
    {
        _db = db;
    }

    public async Task<List<AutomationProfileListItemDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.AutomationProfiles
            .OrderByDescending(x => x.AutomationProfileId)
            .Select(x => new AutomationProfileListItemDto
            {
                AutomationProfileId = x.AutomationProfileId,
                Name = x.Name,
                IsActive = x.IsActive,
                LanguageCode = x.LanguageCode,
                DailyVideoLimit = x.DailyVideoLimit,
                PreferredHoursCsv = x.PreferredHoursCsv,
                GrowthMode = x.GrowthMode,
                LastRunAtUtc = x.LastRunAtUtc,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);
    }

    public Task<AutomationProfile?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);
    }

    public async Task<int> CreateAsync(UpsertAutomationProfileRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new AutomationProfile
        {
            Name = request.Name.Trim(),
            IsActive = request.IsActive,
            ProjectId = request.ProjectId,
            LanguageCode = request.LanguageCode.Trim(),
            PlatformType = request.PlatformType,
            ToneType = request.ToneType,
            DurationTargetSec = request.DurationTargetSec,
            AspectRatio = request.AspectRatio,
            SubtitleEnabled = request.SubtitleEnabled,
            ThumbnailEnabled = request.ThumbnailEnabled,
            DailyVideoLimit = request.DailyVideoLimit,
            PreferredHoursCsv = NormalizeCsv(request.PreferredHoursCsv),
            TopicPrompt = request.TopicPrompt.Trim(),
            HookTemplate = NullIfEmpty(request.HookTemplate),
            ViralPatternTemplate = NullIfEmpty(request.ViralPatternTemplate),
            AutoPublishYouTube = request.AutoPublishYouTube,
            TrendKeywordsCsv = NullIfEmpty(NormalizeCsv(request.TrendKeywordsCsv)),
            SeedTopicsCsv = NullIfEmpty(NormalizeCsv(request.SeedTopicsCsv)),
            GrowthMode = NormalizeGrowthMode(request.GrowthMode),
            TitleTestVariants = request.TitleTestVariants,
            MinSuccessScore = request.MinSuccessScore,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.AutomationProfiles.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity.AutomationProfileId;
    }

    public async Task UpdateAsync(int id, UpsertAutomationProfileRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken)
            ?? throw new InvalidOperationException($"Automation profile not found: {id}");

        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.ProjectId = request.ProjectId;
        entity.LanguageCode = request.LanguageCode.Trim();
        entity.PlatformType = request.PlatformType;
        entity.ToneType = request.ToneType;
        entity.DurationTargetSec = request.DurationTargetSec;
        entity.AspectRatio = request.AspectRatio;
        entity.SubtitleEnabled = request.SubtitleEnabled;
        entity.ThumbnailEnabled = request.ThumbnailEnabled;
        entity.DailyVideoLimit = request.DailyVideoLimit;
        entity.PreferredHoursCsv = NormalizeCsv(request.PreferredHoursCsv);
        entity.TopicPrompt = request.TopicPrompt.Trim();
        entity.HookTemplate = NullIfEmpty(request.HookTemplate);
        entity.ViralPatternTemplate = NullIfEmpty(request.ViralPatternTemplate);
        entity.AutoPublishYouTube = request.AutoPublishYouTube;
        entity.TrendKeywordsCsv = NullIfEmpty(NormalizeCsv(request.TrendKeywordsCsv));
        entity.SeedTopicsCsv = NullIfEmpty(NormalizeCsv(request.SeedTopicsCsv));
        entity.GrowthMode = NormalizeGrowthMode(request.GrowthMode);
        entity.TitleTestVariants = request.TitleTestVariants;
        entity.MinSuccessScore = request.MinSuccessScore;
        entity.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken)
            ?? throw new InvalidOperationException($"Automation profile not found: {id}");

        entity.IsActive = !entity.IsActive;
        entity.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AutomationProfileDashboardDto?> GetDashboardAsync(int id, CancellationToken cancellationToken = default)
    {
        var profile = await _db.AutomationProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AutomationProfileId == id, cancellationToken);

        if (profile == null)
            return null;

        var dto = new AutomationProfileDashboardDto
        {
            AutomationProfileId = profile.AutomationProfileId,
            Name = profile.Name,
            TrendSnapshotCount = await _db.TrendSnapshots.CountAsync(x => x.AutomationProfileId == id, cancellationToken),
            FeedbackCount = await _db.AutomationFeedbacks.CountAsync(x => x.AutomationProfileId == id, cancellationToken),
            TitlePerformanceCount = await _db.TitlePerformances.CountAsync(x => x.AutomationProfileId == id, cancellationToken)
        };

        dto.TopTrends = await _db.TrendSnapshots
            .Where(x => x.AutomationProfileId == id)
            .OrderByDescending(x => x.TrendScore)
            .ThenByDescending(x => x.SnapshotDateUtc)
            .Take(8)
            .Select(x => new AutomationTrendDto
            {
                Keyword = x.Keyword,
                TrendTitle = x.TrendTitle,
                TrendScore = x.TrendScore,
                SourceName = x.SourceName,
                SnapshotDateUtc = x.SnapshotDateUtc
            })
            .ToListAsync(cancellationToken);

        dto.RecentFeedback = await _db.AutomationFeedbacks
            .Where(x => x.AutomationProfileId == id)
            .OrderByDescending(x => x.CreatedDate)
            .Take(10)
            .Select(x => new AutomationFeedbackDto
            {
                FeedbackType = x.FeedbackType,
                Signal = x.Signal,
                ScoreValue = x.ScoreValue,
                Summary = x.Summary,
                SuggestedAction = x.SuggestedAction,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        dto.BestTitles = await _db.TitlePerformances
            .Where(x => x.AutomationProfileId == id)
            .OrderByDescending(x => x.ActualScore ?? x.PredictedScore)
            .Take(10)
            .Select(x => new TitlePerformanceDto
            {
                CandidateTitle = x.CandidateTitle,
                PredictedScore = x.PredictedScore,
                ActualScore = x.ActualScore,
                IsWinner = x.IsWinner,
                CreatedDate = x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        return dto;
    }

    private static string NormalizeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(",",
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeGrowthMode(string? value)
    {
        var mode = (value ?? "balanced").Trim().ToLowerInvariant();
        return mode is "safe" or "balanced" or "aggressive" ? mode : "balanced";
    }
}