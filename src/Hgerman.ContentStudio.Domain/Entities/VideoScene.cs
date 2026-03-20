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

    public string? SourceType { get; set; }
    public string? VisualType { get; set; }
    public string? CameraMotion { get; set; }
    public decimal? CropFocusX { get; set; }
    public decimal? CropFocusY { get; set; }
    public decimal? MotionIntensity { get; set; }

    public string? OverlayText { get; set; }
    public decimal? OverlayStartSecond { get; set; }
    public decimal? OverlayEndSecond { get; set; }

    public string? RenderInstructionsJson { get; set; }

    public VideoJob? VideoJob { get; set; }
    public Asset? ImageAsset { get; set; }
}