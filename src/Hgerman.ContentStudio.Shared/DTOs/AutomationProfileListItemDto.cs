namespace Hgerman.ContentStudio.Shared.DTOs;

public class AutomationProfileListItemDto
{
    public int AutomationProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string LanguageCode { get; set; } = "en";
    public int DailyVideoLimit { get; set; }
    public string PreferredHoursCsv { get; set; } = string.Empty;
    public string GrowthMode { get; set; } = "balanced";
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class UpsertAutomationProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public int ProjectId { get; set; }
    public string LanguageCode { get; set; } = "en";
    public int PlatformType { get; set; }
    public int ToneType { get; set; }
    public int DurationTargetSec { get; set; }
    public int AspectRatio { get; set; }

    public bool SubtitleEnabled { get; set; }
    public bool ThumbnailEnabled { get; set; }

    public int DailyVideoLimit { get; set; }
    public string PreferredHoursCsv { get; set; } = string.Empty;

    public string TopicPrompt { get; set; } = string.Empty;
    public string? HookTemplate { get; set; }
    public string? ViralPatternTemplate { get; set; }

    public bool AutoPublishYouTube { get; set; }

    public string? TrendKeywordsCsv { get; set; }
    public string? SeedTopicsCsv { get; set; }
    public string GrowthMode { get; set; } = "balanced";
    public int TitleTestVariants { get; set; }
    public decimal MinSuccessScore { get; set; }
}

public class AutomationProfileDashboardDto
{
    public int AutomationProfileId { get; set; }
    public string Name { get; set; } = string.Empty;

    public int TrendSnapshotCount { get; set; }
    public int FeedbackCount { get; set; }
    public int TitlePerformanceCount { get; set; }

    public List<AutomationTrendDto> TopTrends { get; set; } = new();
    public List<AutomationFeedbackDto> RecentFeedback { get; set; } = new();
    public List<TitlePerformanceDto> BestTitles { get; set; } = new();
}

public class AutomationTrendDto
{
    public string Keyword { get; set; } = string.Empty;
    public string TrendTitle { get; set; } = string.Empty;
    public decimal TrendScore { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime SnapshotDateUtc { get; set; }
}

public class AutomationFeedbackDto
{
    public string FeedbackType { get; set; } = string.Empty;
    public string Signal { get; set; } = string.Empty;
    public decimal? ScoreValue { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class TitlePerformanceDto
{
    public string CandidateTitle { get; set; } = string.Empty;
    public decimal PredictedScore { get; set; }
    public decimal? ActualScore { get; set; }
    public bool IsWinner { get; set; }
    public DateTime CreatedDate { get; set; }
}