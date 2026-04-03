using Hgerman.ContentStudio.Domain.Common;

namespace Hgerman.ContentStudio.Domain.Entities;

public class AutomationProfile : BaseEntity
{
    public int AutomationProfileId { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int ProjectId { get; set; }

    public string LanguageCode { get; set; } = "en";
    public int PlatformType { get; set; }
    public int ToneType { get; set; }
    public int DurationTargetSec { get; set; } = 45;
    public int AspectRatio { get; set; }

    public bool SubtitleEnabled { get; set; } = true;
    public bool ThumbnailEnabled { get; set; } = true;

    public int DailyVideoLimit { get; set; } = 3;
    public string PreferredHoursCsv { get; set; } = "09,14,20";

    public string TopicPrompt { get; set; } = string.Empty;
    public string? HookTemplate { get; set; }
    public string? ViralPatternTemplate { get; set; }

    public bool AutoPublishYouTube { get; set; }

    public string? TrendKeywordsCsv { get; set; }
    public string? SeedTopicsCsv { get; set; }
    public string GrowthMode { get; set; } = "balanced";

    public int TitleTestVariants { get; set; } = 3;
    public decimal MinSuccessScore { get; set; } = 55m;

    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? LastGeneratedDateUtc { get; set; }

    public Project? Project { get; set; }

    public ICollection<TrendSnapshot> TrendSnapshots { get; set; } = new List<TrendSnapshot>();
    public ICollection<TitlePerformance> TitlePerformances { get; set; } = new List<TitlePerformance>();
    public ICollection<AutomationFeedback> FeedbackItems { get; set; } = new List<AutomationFeedback>();
    public ICollection<VideoJob> VideoJobs { get; set; } = new List<VideoJob>();
}