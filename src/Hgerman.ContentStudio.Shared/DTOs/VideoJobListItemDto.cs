using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Shared.DTOs;

public class VideoJobListItemDto
{
    public int VideoJobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public PlatformType PlatformType { get; set; }
    public VideoJobStatus Status { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
