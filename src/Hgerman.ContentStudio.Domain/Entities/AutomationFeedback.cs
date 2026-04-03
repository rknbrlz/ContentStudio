namespace Hgerman.ContentStudio.Domain.Entities;

public class AutomationFeedback
{
    public int AutomationFeedbackId { get; set; }
    public int AutomationProfileId { get; set; }
    public int? VideoJobId { get; set; }

    public string FeedbackType { get; set; } = string.Empty;
    public string Signal { get; set; } = string.Empty;
    public decimal? ScoreValue { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public AutomationProfile? AutomationProfile { get; set; }
    public VideoJob? VideoJob { get; set; }
}