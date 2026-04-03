namespace Hgerman.ContentStudio.Domain.Entities;

public class TrendSnapshot
{
    public int TrendSnapshotId { get; set; }
    public int AutomationProfileId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string TrendTitle { get; set; } = string.Empty;
    public decimal TrendScore { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime SnapshotDateUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public AutomationProfile? AutomationProfile { get; set; }
}