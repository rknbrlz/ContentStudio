namespace Hgerman.ContentStudio.Domain.Entities;

public class TitlePerformance
{
    public int TitlePerformanceId { get; set; }
    public int VideoJobId { get; set; }
    public int? AutomationProfileId { get; set; }

    public string OriginalTitle { get; set; } = string.Empty;
    public string CandidateTitle { get; set; } = string.Empty;
    public int VariantNo { get; set; }
    public string? HookType { get; set; }
    public string? PatternType { get; set; }

    public decimal PredictedScore { get; set; }
    public decimal? ActualScore { get; set; }

    public int? ViewCount { get; set; }
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
    public decimal? ClickThroughRate { get; set; }
    public decimal? AvgWatchSeconds { get; set; }
    public decimal? RetentionRate { get; set; }

    public bool IsWinner { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }

    public VideoJob? VideoJob { get; set; }
    public AutomationProfile? AutomationProfile { get; set; }
}