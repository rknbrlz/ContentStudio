namespace Hgerman.ContentStudio.Web.Models;

public class AutomationProfileDashboardItemViewModel
{
    public int AutomationProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ProjectId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public int DailyVideoLimit { get; set; }
    public string PreferredHoursCsv { get; set; } = string.Empty;
    public bool AutoPublishYouTube { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public int TodayCreatedCount { get; set; }
    public int CompletedUnpublishedCount { get; set; }
}