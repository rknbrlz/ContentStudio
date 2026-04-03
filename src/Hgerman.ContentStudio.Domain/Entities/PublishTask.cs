using Hgerman.ContentStudio.Domain.Common;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Domain.Entities;

public class PublishTask : BaseEntity
{
    public int PublishTaskId { get; set; }
    public int VideoJobId { get; set; }
    public PlatformType PlatformType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public int? ThumbnailAssetId { get; set; }
    public PublishStatus PublishStatus { get; set; } = PublishStatus.Draft;
    public string? PlatformVideoId { get; set; }
    public string? PublishUrl { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? PublishedDate { get; set; }
    public string? ErrorMessage { get; set; }

    public VideoJob? VideoJob { get; set; }
    public Asset? ThumbnailAsset { get; set; }
}
