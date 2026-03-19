using Hgerman.ContentStudio.Domain.Common;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Domain.Entities;

public class VideoScene : BaseEntity
{
    public int VideoSceneId { get; set; }
    public int VideoJobId { get; set; }
    public int SceneNo { get; set; }
    public string SceneText { get; set; } = string.Empty;
    public string? ScenePrompt { get; set; }
    public int? ImageAssetId { get; set; }
    public decimal StartSecond { get; set; }
    public decimal EndSecond { get; set; }
    public decimal DurationSecond { get; set; }
    public string? TransitionType { get; set; }
    public VideoJobStatus Status { get; set; } = VideoJobStatus.Draft;

    public VideoJob? VideoJob { get; set; }
    public Asset? ImageAsset { get; set; }
}
