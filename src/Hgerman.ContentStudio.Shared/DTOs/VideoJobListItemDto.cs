namespace Hgerman.ContentStudio.Shared.DTOs;

public class VideoJobListItemDto
{
    public int VideoJobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string PlatformType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsPublished { get; set; }

    public string ProfileName { get; set; } = "Manual";
    public int? AutomationProfileId { get; set; }
}